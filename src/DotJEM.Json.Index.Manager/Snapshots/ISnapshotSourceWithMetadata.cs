using DotJEM.Json.Index.Storage.Snapshot;
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