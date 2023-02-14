using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Storage.Snapshot;
using Newtonsoft.Json.Linq;
using ILuceneFile = DotJEM.Json.Index.Storage.Snapshot.ILuceneFile;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class LuceneZipSnapshot : ISnapshot
{
    private readonly JObject metadata;
    private readonly IInfoStream<LuceneZipSnapshot> infoStream = new InfoStream<LuceneZipSnapshot>();
    private readonly ZipArchive archive;

    public long Generation { get; }
    public ILuceneFile SegmentsFile { get; }
    public ILuceneFile SegmentsGenFile { get; }
    public IEnumerable<ILuceneFile> Files { get; }
    public IInfoStream InfoStream => infoStream;

    public LuceneZipSnapshot(ZipArchive archive, JObject metadata)
    {
     
        this.archive = archive;
        this.metadata = metadata;

        Files = new List<ILuceneFile>();
        if (metadata["files"] is JArray arr)
            Files = arr.Select(fileName => CreateLuceneZipFile((string)fileName, archive));
        SegmentsFile = CreateLuceneZipFile((string)metadata["segmentsFile"], archive) ;
        SegmentsGenFile = CreateLuceneZipFile((string)metadata["segmentsGenFile"], archive);
        Generation = (long)metadata["generation"];
        infoStream.WriteSnapshotOpenEvent(this, "");

        LuceneZipFile CreateLuceneZipFile(string fileName, ZipArchive archive)
        {
            LuceneZipFile file = new(fileName, archive);
            file.InfoStream.Forward(infoStream);
            return file;
        }
    }
        
    public void Dispose()
    {
        archive.Dispose();
        infoStream.WriteSnapshotCloseEvent(this, "");
    }

}