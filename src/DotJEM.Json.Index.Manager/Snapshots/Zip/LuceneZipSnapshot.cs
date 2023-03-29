using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using DotJEM.Json.Index.Storage.Snapshot;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;
using ILuceneFile = DotJEM.Json.Index.Storage.Snapshot.ILuceneFile;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class LuceneZipSnapshot : ISnapshot
{
    private readonly IInfoStream<LuceneZipSnapshot> infoStream = new InfoStream<LuceneZipSnapshot>();
    private readonly ZipArchive archive;

    public string Name { get; }
    public long Generation { get; }
    public ILuceneFile SegmentsFile { get; }
    public ILuceneFile SegmentsGenFile { get; }
    public IEnumerable<ILuceneFile> Files { get; }
    public IInfoStream InfoStream => infoStream;

    public LuceneZipSnapshot(string name, ZipArchive archive, JObject metadata)
    {
        this.Name = name;
        this.archive = archive;

        Files = metadata["files"] is JArray arr 
            ? arr.Select(fileName => CreateLuceneZipFile((string)fileName, archive)).ToList()
            : new List<ILuceneFile>();
        SegmentsFile = CreateLuceneZipFile((string)metadata["segmentsFile"], archive) ;
        SegmentsGenFile = CreateLuceneZipFile((string)metadata["segmentsGenFile"], archive);
        Generation = (long)metadata["generation"];
        infoStream.WriteSnapshotOpenEvent(this, "");
        LuceneZipFile CreateLuceneZipFile(string fileName, ZipArchive archive)
        {
            LuceneZipFile file = new(fileName, archive);
            file.InfoStream.Subscribe(infoStream);
            return file;
        }
    }
        
    public void Dispose()
    {
        archive.Dispose();
        infoStream.WriteSnapshotCloseEvent(this, $"Closing snapshot '{Name}'.");
    }

}