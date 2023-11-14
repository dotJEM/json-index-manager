using DotJEM.Json.Index2.Snapshots;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface ISnapshotSourceWithMetadata : ISnapshotSource
{
    string Name { get; }

    IInfoStream InfoStream { get; }

    JObject Metadata { get; }

    bool Verify();
    bool Delete();
}