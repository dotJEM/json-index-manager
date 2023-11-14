using System;
using System.Threading.Tasks;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index2;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Web.Scheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IJsonIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    Task<bool> TakeSnapshotAsync(StorageIngestState state);
    Task<RestoreSnapshotResult> RestoreSnapshotAsync();
    Task RunAsync(IIngestProgressTracker ingestProgressTracker, bool restoredFromSnapshot);
}

public readonly record struct RestoreSnapshotResult(bool RestoredFromSnapshot, StorageIngestState State)
{
    public bool RestoredFromSnapshot { get; } = RestoredFromSnapshot;
    public StorageIngestState State { get; } = State;
}

public class NullIndexSnapshotManager : IJsonIndexSnapshotManager
{
    public IInfoStream InfoStream { get; } = new InfoStream<JsonIndexSnapshotManager>();

    public Task<bool> TakeSnapshotAsync(StorageIngestState state) => Task.FromResult(true);

    public Task<RestoreSnapshotResult> RestoreSnapshotAsync() => Task.FromResult(default(RestoreSnapshotResult));

    public Task RunAsync(IIngestProgressTracker ingestProgressTracker, bool restoredFromSnapshot) => Task.CompletedTask;
}

public class JsonIndexSnapshotManager : IJsonIndexSnapshotManager
{
    private readonly IJsonIndex index;
    private readonly ISnapshotStrategy strategy;
    private readonly IWebTaskScheduler scheduler;
    private readonly IInfoStream<JsonIndexSnapshotManager> infoStream = new InfoStream<JsonIndexSnapshotManager>();

    private readonly string schedule;

    public IInfoStream InfoStream => infoStream;

    public JsonIndexSnapshotManager(IJsonIndex index, ISnapshotStrategy snapshotStrategy, IWebTaskScheduler scheduler, string schedule)
    {
        this.index = index;
        this.strategy = snapshotStrategy;
        this.scheduler = scheduler;
        this.schedule = schedule;
        this.strategy.InfoStream.Subscribe(infoStream);
    }

    public async Task RunAsync(IIngestProgressTracker tracker, bool restoredFromSnapshot)
    {
        await Initialization.WhenInitializationComplete(tracker).ConfigureAwait(false);
        if (!restoredFromSnapshot)
        {
            infoStream.WriteInfo("Taking snapshot after initialization.");
            await TakeSnapshotAsync(tracker.IngestState).ConfigureAwait(false);
        }
        scheduler.Schedule(nameof(JsonIndexSnapshotManager), b => this.TakeSnapshot(tracker.IngestState), schedule);
    }

    public Task<bool> TakeSnapshotAsync(StorageIngestState state)
    {
        return Task.Run(() => TakeSnapshot(state));
    }

    public Task<RestoreSnapshotResult> RestoreSnapshotAsync()
    {
        return Task.Run(RestoreSnapshot);
    }

    public bool TakeSnapshot(StorageIngestState state)
    {
        JObject json = JObject.FromObject(state);
        try
        {
            ISnapshotTarget target = strategy.CreateTarget(new JObject { ["storageGenerations"] = json });
            
            index.Commit();
            index.Snapshot(target);
            infoStream.WriteInfo($"Created snapshot");
            return true;
        }
        catch (Exception exception)
        {
            infoStream.WriteError("Failed to take snapshot.", exception);
            return false;
        }
        finally
        {
            strategy.CleanOldSnapshots();
        }
    }

    public RestoreSnapshotResult RestoreSnapshot()
    {
        int offset = 0;
        while (true)
        {
            try
            {
                var source = strategy.CreateSource(offset++);
                if (source == null)
                {
                    infoStream.WriteInfo($"No snapshots found to restore");
                    return new RestoreSnapshotResult(false, default);
                }

                //if (!source.Verify())
                //{
                //    infoStream.WriteWarning($"Deleting corrupt snapshot {source.Name}.");
                //    source.Delete();
                //    continue;
                //}

                //infoStream.WriteInfo($"Trying to restore snapshot {source.Name}");
                ISnapshot restored = index.Restore(source);
                //if (source.Metadata["storageGenerations"] is not JObject generations) continue;
                //if (generations["Areas"] is not JArray areas) continue;

                return new RestoreSnapshotResult(true, new StorageIngestState(
                   // areas.ToObject<StorageAreaIngestState[]>()
                ));

            }
            catch (Exception ex)
            {
                infoStream.WriteError("Failed to restore snapshot.", ex);
                return new RestoreSnapshotResult(false, default);
            }
        }
    }
}