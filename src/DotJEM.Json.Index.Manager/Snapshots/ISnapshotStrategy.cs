using DotJEM.Json.Index.Storage.Snapshot;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface ISnapshotStrategy
{
    IInfoStream InfoStream { get; }
    ISnapshotTarget CreateTarget(JObject metaData);
    ISnapshotSourceWithMetadata CreateSource(int offset);
    void CleanOldSnapshots();
}