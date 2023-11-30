using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface ISnapshotStrategy
{
    IInfoStream InfoStream { get; }
    ISnapshotStorage Storage { get; }
    void CleanOldSnapshots();
}