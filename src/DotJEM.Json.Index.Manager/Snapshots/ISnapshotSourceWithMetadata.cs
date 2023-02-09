using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots;

public interface ISnapshotSourceWithMetadata : ISnapshotSource
{
    IInfoStream InfoStream { get; }

    JObject Metadata { get; }

    bool Verify();
    bool Delete();
}