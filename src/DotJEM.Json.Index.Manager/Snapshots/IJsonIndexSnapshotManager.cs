using System;
using System.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Index.Manager.Tracking;
using DotJEM.Json.Index.Storage.Snapshot;
using DotJEM.TaskScheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IJsonIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    Task<bool> TakeSnapshotAsync(StorageIngestState state);
    Task<RestoreSnapshotResult> RestoreSnapshotAsync();
    Task RunAsync(IIndexIngestProgressTracker indexIngestProgressTracker, bool restoredFromSnapshot);
}

public readonly record struct RestoreSnapshotResult(bool RestoredFromSnapshot, StorageIngestState State)
{
    public bool RestoredFromSnapshot { get; } = RestoredFromSnapshot;
    public StorageIngestState State { get; } = State;
}

public class JsonIndexSnapshotManager : IJsonIndexSnapshotManager
{
    private readonly IStorageIndex index;
    private readonly ISnapshotStrategy strategy;
    private readonly IWebBackgroundTaskScheduler scheduler;
    private readonly ISnapshotConfiguration configuration;

    private readonly IInfoStream<JsonIndexSnapshotManager> infoStream = new InfoStream<JsonIndexSnapshotManager>();
    public IInfoStream InfoStream => infoStream;

    public JsonIndexSnapshotManager(IStorageIndex index, ISnapshotStrategy snapshotStrategy, IWebBackgroundTaskScheduler scheduler, ISnapshotConfiguration configuration)
    {
        this.index = index;
        this.strategy = snapshotStrategy;
        this.scheduler = scheduler;
        this.configuration = configuration;
        this.strategy.InfoStream.Forward(infoStream);
    }

    public async Task RunAsync(IIndexIngestProgressTracker tracker, bool restoredFromSnapshot)
    {
        await Initialization.WhenInitializationComplete(tracker).ConfigureAwait(false);
        if (!restoredFromSnapshot)
        {
            infoStream.WriteInfo("Taking snapshot after initialization.");
            await TakeSnapshotAsync(tracker.IngestState).ConfigureAwait(false);
        }
        scheduler.Schedule(nameof(JsonIndexSnapshotManager), b => this.TakeSnapshot(tracker.IngestState), configuration.Schedule);
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
            index.Storage.Snapshot(target);
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
            strategy.CleanOldSnapshots(2);
        }
    }

    public RestoreSnapshotResult RestoreSnapshot()
    {
        int offset = 0;
        while (true)
        {
            try
            {
                ISnapshotSourceWithMetadata source = strategy.CreateSource(offset++);
                if (source == null)
                {
                    infoStream.WriteInfo($"No snapshots found to restore");
                    return new RestoreSnapshotResult(false, default);
                }

                if (!source.Verify())
                {
                    infoStream.WriteWarning($"Deleting corrupt snapshot {source.Name}.");
                    source.Delete();
                    continue;
                }

                infoStream.WriteInfo($"Trying to restore snapshot {source.Name}");
                bool restored = index.Storage.Restore(source);
                if (source.Metadata["storageGenerations"] is not JObject generations) continue;
                if (generations["Areas"] is not JArray areas) continue;

                return new RestoreSnapshotResult(restored, new StorageIngestState(
                    areas.ToObject<StorageAreaIngestState[]>()
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