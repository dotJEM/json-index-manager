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
    IJsonIndexWriteer Create();
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
    public IJsonIndexWriteer Create()
        => new SequentialJsonIndexWriteer(index, configuration);
}

public interface IJsonIndexManager
{
    IInfoStream InfoStream { get; }

    Task RunAsync();
    Task<bool> TakeSnapshotAsync();
}



public class JsonIndexManager : IJsonIndexManager
{
    private readonly IStorageManager storage;
    private readonly IJsonIndexSnapshotManager snapshots;
    private readonly IIndexIngestProgressTracker tracker;

    private readonly IJsonIndexWriteer context;
    private readonly IInfoStream<JsonIndexManager> infoStream = new InfoStream<JsonIndexManager>();

    public IInfoStream InfoStream => infoStream;

    public JsonIndexManager(IStorageContext context, IStorageIndex index, ISnapshotStrategy snapshotStrategy,
        IWebBackgroundTaskScheduler scheduler, IJsonIndexManagerConfiguration configuration)
      : this(new StorageManager(context, scheduler, configuration.StorageConfiguration),
          new JsonIndexSnapshotManager(index, snapshotStrategy, scheduler, configuration.SnapshotConfiguration),
          new WriteContextFactory(index, configuration.WriterConfiguration))
    { }

    public JsonIndexManager(IStorageManager storage, IJsonIndexSnapshotManager snapshots, IWriteContextFactory writeContextFactory)
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
        tracker.ForEachAsync(state => infoStream.WriteTrackerStateEvent(state));
    }

    public async Task RunAsync()
    {
        bool restoredFromSnapshot = await RestoreSnapshotAsync();
        infoStream.WriteInfo($"Index restored from a snapshot: {restoredFromSnapshot}.");
        await Task.WhenAll(
            snapshots.RunAsync(tracker, restoredFromSnapshot), 
            storage.RunAsync()).ConfigureAwait(false);
    }

    public async Task<bool> TakeSnapshotAsync()
    {
        StorageIngestState state = tracker.IngestState;
        return await snapshots.TakeSnapshotAsync(state);
    }

    public async Task<bool> RestoreSnapshotAsync()
    {
        RestoreSnapshotResult restoreResult = await snapshots.RestoreSnapshotAsync();
        foreach (StorageAreaIngestState state in restoreResult.State.Areas)
        {
            storage.UpdateGeneration(state.Area, state.Generation.Current);
            tracker.UpdateState(state);
        }
        return restoreResult.RestoredFromSnapshot;
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
            if(state is not StorageIngestState ingestState)
                return;

            StorageObserverEventType[] states = ingestState.Areas
                .Select(x => x.LastEvent)
                .ToArray();
            if (states.All(state => state is StorageObserverEventType.Updated or StorageObserverEventType.Initialized))
                completionSource.SetResult(true);
        }, CancellationToken.None);
        return completionSource.Task;
    }

}

