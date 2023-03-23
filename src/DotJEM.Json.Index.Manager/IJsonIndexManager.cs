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
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Manager.Writer;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager;

public interface IJsonIndexManager
{
    IInfoStream InfoStream { get; }
    Task RunAsync();
    Task<bool> TakeSnapshotAsync();
}

public class JsonIndexManager : IJsonIndexManager
{
    private readonly IJsonStorageManager jsonStorage;
    private readonly IJsonIndexSnapshotManager snapshots;
    private readonly IIngestProgressTracker tracker;
    private readonly IJsonIndexWriter writer;
    
    private readonly IInfoStream<JsonIndexManager> infoStream = new InfoStream<JsonIndexManager>();

    public IInfoStream InfoStream => infoStream;

    public JsonIndexManager(IJsonStorageManager jsonStorage, IJsonIndexSnapshotManager snapshots, IJsonIndexWriter writer)
    {
        this.jsonStorage = jsonStorage;
        this.snapshots = snapshots;
        this.writer = writer;
        
        jsonStorage.Observable.ForEachAsync(CaptureChange);
        jsonStorage.InfoStream.Subscribe(infoStream);
        snapshots.InfoStream.Subscribe(infoStream);

        tracker = new IngestProgressTracker();
        jsonStorage.InfoStream.Subscribe(tracker);
        jsonStorage.Observable.Subscribe(tracker);
        snapshots.InfoStream.Subscribe(tracker);

        tracker.InfoStream.Subscribe(infoStream);
        tracker.ForEachAsync(state => infoStream.WriteTrackerStateEvent(state));
    }

    public async Task RunAsync()
    {
        bool restoredFromSnapshot = await RestoreSnapshotAsync();
        infoStream.WriteInfo($"Index restored from a snapshot: {restoredFromSnapshot}.");
        await Task.WhenAll(
            snapshots.RunAsync(tracker, restoredFromSnapshot), 
            jsonStorage.RunAsync()).ConfigureAwait(false);
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
            jsonStorage.UpdateGeneration(state.Area, state.Generation.Current);
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
                    writer.Create(change.Entity);
                    break;
                case ChangeType.Update:
                    writer.Write(change.Entity);
                    break;
                case ChangeType.Delete:
                    writer.Delete(change.Entity);
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
    public static Task WhenInitializationComplete(IIngestProgressTracker tracker)
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

