using System;
using System.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    Task<bool> TakeSnapshotAsync(StorageIngestState state);
    Task<RestoreSnapshotResult> RestoreSnapshotAsync();
}

public readonly record struct RestoreSnapshotResult(bool RestoredFromSnapshot, StorageIngestState State)
{
    public bool RestoredFromSnapshot { get; } = RestoredFromSnapshot;
    public StorageIngestState State { get; } = State;
}

public class IndexSnapshotManager : IIndexSnapshotManager
{
    private readonly IStorageIndex index;
    private readonly ISnapshotStrategy strategy;
    
    private readonly IInfoStream<IndexSnapshotManager> infoStream = new InfoStream<IndexSnapshotManager>();
    public IInfoStream InfoStream => infoStream;

    public IndexSnapshotManager(IStorageIndex index, ISnapshotStrategy snapshotStrategy)
    {
        this.index = index;
        this.strategy = snapshotStrategy;
        this.strategy.InfoStream.Forward(infoStream);
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