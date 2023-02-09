using System;
using System.IO;
using DotJEM.Json.Index.Storage.Snapshot;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotTarget : ISnapshotTarget
{
    private readonly string path;
    private readonly JObject metaData;

    public ZipSnapshotTarget(string path, JObject metaData)
    {
        this.path = path;
        this.metaData = metaData;
    }

    public ISnapshotWriter Open(IndexCommit commit)
    {
        Directory.CreateDirectory(path);
        return new ZipSnapshotWriter(metaData, Path.Combine(path, $"{DateTime.Now:yyyy-MM-ddTHHmmss}.{commit.Generation:D8}.zip"))
            .WriteMetaData(commit);
    }
}