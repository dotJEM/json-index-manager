using System;
using System.Linq;
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
        
        this.scheduler = scheduler;
        this.schedule = schedule;

        this.strategy = snapshotStrategy;
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
        scheduler.Schedule(nameof(JsonIndexSnapshotManager), _ => this.TakeSnapshotAsync(tracker.IngestState), schedule);
    }

    public async Task<bool> TakeSnapshotAsync(StorageIngestState state)
    {
        try
        {
            JObject json = JObject.FromObject(state);
            ISnapshotStorage target = strategy.Storage;
            
            index.Commit();
            ISnapshot snapshot = await index.TakeSnapshotAsync(target);

            ISnapshotWriter writer = snapshot.OpenWriter();
            


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

    public async Task<RestoreSnapshotResult>  RestoreSnapshotAsync()
    {
        try
        {
            ISnapshotStorage source = strategy.Storage;
            if (source == null)
            {
                infoStream.WriteInfo($"No snapshots found to restore");
                return new RestoreSnapshotResult(false, default);
            }

            int count = 0;
            foreach (ISnapshot snapshot in source.LoadSnapshots())
            {
                count++;
                try
                {
                    if (snapshot.Verify() && await index.RestoreSnapshotAsync(snapshot))
                        return new RestoreSnapshotResult(true, new StorageIngestState());

                    snapshot.Delete();
                }
                catch
                {
                    snapshot.Delete();
                }
            }
            infoStream.WriteInfo($"No snapshots restored. {count} was found to be corrupt and was deleted.");
            return new RestoreSnapshotResult(false, new StorageIngestState());

            //infoStream.WriteInfo($"Trying to restore snapshot {source.Name}");
            //ISnapshot snapshot = source.LoadSnapshots().FirstOrDefault();
            // if(snapshot is null)
            //ISnapshot restored = await index.RestoreSnapshotAsync(snapshot);
            //if (source.Metadata["storageGenerations"] is not JObject generations) continue;
            //if (generations["Areas"] is not JArray areas) continue;

            //                return new RestoreSnapshotResult(true, new StorageIngestState(
            // areas.ToObject<StorageAreaIngestState[]>()
            //               ));

        }
        catch (Exception ex)
        {
            infoStream.WriteError("Failed to restore snapshot.", ex);
            return new RestoreSnapshotResult(false, default);
        }
    }
}