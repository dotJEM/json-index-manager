using System;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    Task<bool> TakeSnapshotAsync(StorageIngestState state);
    Task<bool> RestoreSnapshotAsync();
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
    }

    public Task<bool> TakeSnapshotAsync(StorageIngestState state)
    {
        return Task.Run(() => TakeSnapshot(state));
    }

    public Task<bool> RestoreSnapshotAsync()
    {
        return Task.FromResult(false);
    }

    public bool TakeSnapshot(StorageIngestState state)
    {
        //if(paused || maxSnapshots <= 0 || strategy == null) return false;
            
        JObject json = JObject.FromObject(state);

        //JObject generations = storage.AreaInfos
        //    .Aggregate(new JObject(), (x, info) => {
        //        x[info.Name] = storage.Area(info.Name).Log.CurrentGeneration;
        //        return x;
        //    });

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

    public bool RestoreSnapshot()
    {
        //if(maxSnapshots <= 0 || strategy == null) return false;

        //int offset = 0;
        //while (true)
        //{
        //    try
        //    {
        //        ISnapshotSourceWithMetadata source = strategy.CreateSource(offset++);
        //        if (source == null)
        //            return false;

        //        if (!source.Verify())
        //        {
        //            source.Delete();
        //            continue;
        //        }

        //        index.Storage.Restore(source);
        //        if (source.Metadata["storageGenerations"] is not JObject metadata) continue;
                    
        //        foreach (JProperty property in metadata.Properties())
        //            storage.Area(property.Name).Log.Get(property.Value.ToObject<long>(), count: 0);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        InfoStream.WriteError("Failed to restore snapshot.", ex);
        //    }
        //}
        throw new NotImplementedException("");
    }
}