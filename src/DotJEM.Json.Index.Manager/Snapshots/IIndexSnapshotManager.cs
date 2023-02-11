using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using static Lucene.Net.Documents.Field;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface IIndexSnapshotManager
{
    IInfoStream InfoStream { get; }

    bool TakeSnapshot();
    bool RestoreSnapshot();
}

public class IndexSnapshotManager : IIndexSnapshotManager
{
    private ISnapshotStrategy strategy;
    
    private readonly IInfoStream<IndexSnapshotManager> infoStream = new InfoStream<IndexSnapshotManager>();
    public IInfoStream InfoStream => infoStream;

    public IndexSnapshotManager(ISnapshotStrategy snapshotStrategy)
    {
        this.strategy = snapshotStrategy;
    }


    public bool TakeSnapshot()
    {
        //if(paused || maxSnapshots <= 0 || strategy == null) return false;
            
        //JObject generations = storage.AreaInfos
        //    .Aggregate(new JObject(), (x, info) => {
        //        x[info.Name] = storage.Area(info.Name).Log.CurrentGeneration;
        //        return x;
        //    });

        //try
        //{
        //    ISnapshotTarget target = strategy.CreateTarget(new JObject { ["storageGenerations"] = generations });
        //    index.Commit();
        //    index.Storage.Snapshot(target);
        //    InfoStream.WriteInfo($"Created snapshot");
        //}
        //catch (Exception exception)
        //{
        //    logger.Log("failure", Severity.Error, "Failed to take snapshot.", new { exception });
        //}
        //strategy.CleanOldSnapshots(maxSnapshots);
        return true;
    }

    public bool RestoreSnapshot()
    {
        throw new NotImplementedException();
    }
}