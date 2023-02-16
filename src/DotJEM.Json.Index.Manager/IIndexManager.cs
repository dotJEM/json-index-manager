using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Manager.WriteContexts;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.TaskScheduler;

namespace DotJEM.Json.Index.Manager;

public interface IWriteContextFactory
{
    WriteContexts.ILuceneWriteContext Create();
}

public class WriteContextFactory : IWriteContextFactory
{
    private readonly IStorageIndex index;
    private readonly IWriteContextConfiguration configuration;

    public WriteContextFactory(IStorageIndex index, IWriteContextConfiguration configuration = null)
    {
        this.index = index;
        this.configuration = configuration ?? new DefaultWriteContextConfiguration();
    }
    public WriteContexts.ILuceneWriteContext Create()
        => new SequentialLuceneWriteContext(index, configuration.RamBufferSize);
}

public interface IIndexManager
{
    IInfoStream InfoStream { get; }

    Task RunAsync();
    Task<bool> TakeSnapshotAsync();
}



public class IndexManager : IIndexManager
{
    private readonly IStorageManager storage;
    private readonly IIndexSnapshotManager snapshots;
    private readonly IIndexIngestProgressTracker tracker;

    private readonly WriteContexts.ILuceneWriteContext context;
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;

    public IndexManager(IStorageContext context, IStorageIndex index, ISnapshotStrategy snapshotStrategy,
        IWebBackgroundTaskScheduler scheduler, IIndexManagerConfiguration configuration)
      : this(new StorageManager(context, scheduler, configuration.StorageConfiguration),
          new IndexSnapshotManager(index, snapshotStrategy),
          new WriteContextFactory(index, configuration.WriterConfiguration))
    { }

    public IndexManager(IStorageManager storage, IIndexSnapshotManager snapshots, IWriteContextFactory writeContextFactory)
    {
        this.storage = storage;
        this.snapshots = snapshots;

        context = writeContextFactory.Create();
        storage.Observable.ForEachAsync(CaptureChange);
        storage.InfoStream.Forward(infoStream);
        snapshots.InfoStream.Forward(infoStream);

        tracker = new IndexIngestProgressTracker();
        storage.InfoStream.Subscribe(tracker);
        storage.Observable.Subscribe(tracker);
        snapshots.InfoStream.Subscribe(tracker);

        tracker.InfoStream.Forward(infoStream);
        tracker.ForEachAsync(state => infoStream.WriteStorageIngestStateEvent(state));
    }

    public async Task RunAsync()
    {
        bool restoredSnapshotAsync = await snapshots.RestoreSnapshotAsync();
        infoStream.WriteInfo($"Index restored from a snapshot: {restoredSnapshotAsync}.");

        Task snapshot = Task.Run(async () =>
        {
            await Initialization.WhenInitializationComplete(tracker).ConfigureAwait(false);
            if (!restoredSnapshotAsync)
            {
                infoStream.WriteInfo("Taking snapshot after initialization.");
                await TakeSnapshotAsync().ConfigureAwait(false);
            }
        });
        Task fetcher = storage.RunAsync();
        await Task.WhenAll(snapshot, fetcher).ConfigureAwait(false);
    }

    public async Task<bool> TakeSnapshotAsync()
    {
        StorageIngestState state = tracker.CurrentState;
        return await snapshots.TakeSnapshotAsync(state);
    }

    public async Task<bool> RestoreSnapshotAsync()
    {
        return await RestoreSnapshotAsync();
    }

    private void CaptureChange(IStorageChange change)
    {
        try
        {
            switch (change.Type)
            {
                case ChangeType.Create:
                    context.Create(change.Entity);
                    break;
                case ChangeType.Update:
                    context.Write(change.Entity);
                    break;
                case ChangeType.Delete:
                    context.Delete(change.Entity);
                    break;
                case ChangeType.Faulty:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            infoStream.WriteError($"Failed to ingest change from {change.Area}", ex);
        }
    }
}


public class Initialization
{

    public static Task WhenInitializationComplete(IIndexIngestProgressTracker tracker)
    {
        TaskCompletionSource<bool> completionSource = new ();

        tracker.ForEachAsync(state => {
            StorageObserverEventType[] states = state.Areas
                .Select(x => x.LastEvent)
                .ToArray();
            if (states.All(state => state is StorageObserverEventType.Updated or StorageObserverEventType.Initialized))
                completionSource.SetResult(true);
            
        }, CancellationToken.None);

        return completionSource.Task;
    }

}

