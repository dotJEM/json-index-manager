using System.IO;
using System.IO.Compression;
using DotJEM.Json.Index.Storage.Snapshot;
using Lucene.Net.Index;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotWriter : ISnapshotWriter
{
    private readonly ZipArchive archive;
    private readonly JObject metadata;

    public ZipSnapshotWriter(JObject metadata, string file)
    {
        if (metadata["files"] is not JArray)
            metadata["files"] = new JArray();
        this.metadata = metadata;

        archive = ZipFile.Open(file, ZipArchiveMode.Create);
    }

    public void WriteFile(IndexInputStream stream)
    {
        using Stream target = archive.CreateEntry(stream.FileName).Open();
        stream.CopyTo(target);

        JArray filesArr = (JArray)metadata["files"]!;
        filesArr.Add(JToken.FromObject(stream.FileName));
    }

    public void WriteSegmentsFile(IndexInputStream stream)
    {
        using Stream target = archive.CreateEntry(stream.FileName).Open();
        stream.CopyTo(target);
        metadata["segmentsFile"] = stream.FileName;
    }

    public void WriteSegmentsGenFile(IndexInputStream stream)
    {
        using Stream target = archive.CreateEntry(stream.FileName).Open();
        stream.CopyTo(target);
        metadata["segmentsGenFile"] = stream.FileName;
    }

    public ZipSnapshotWriter WriteMetaData(IndexCommit commit)
    {
        metadata["generation"] = commit.Generation;
        metadata["version"] = commit.Version;
        return this;
    }

    public void Dispose()
    {
        using (Stream metaStream = archive.CreateEntry("metadata.json").Open())
        {
            using JsonWriter writer = new JsonTextWriter(new StreamWriter(metaStream));
            metadata.WriteTo(writer);
        }
        archive?.Dispose();
    }
}