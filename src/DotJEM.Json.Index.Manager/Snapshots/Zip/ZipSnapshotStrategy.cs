using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.Json.Index2.Util;
using DotJEM.ObservableExtensions.InfoStreams;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotStrategy : ISnapshotStrategy
{
    private readonly int maxSnapshots;
    private readonly IInfoStream<ZipSnapshotStrategy> infoStream = new InfoStream<ZipSnapshotStrategy>();
    private readonly MetaZipSnapshotStorage storage;

    public IInfoStream InfoStream => infoStream;
    public ISnapshotStorage Storage => storage;

    public ZipSnapshotStrategy(string path, int maxSnapshots = 2)
    {
        this.maxSnapshots = maxSnapshots;
        this.storage = new MetaZipSnapshotStorage(path);
    }

    public void CleanOldSnapshots()
    {
        foreach (ISnapshot file in Storage.loadSnapshots().Skip(maxSnapshots))
        {
            try
            {
                File.Delete(file);
                infoStream.WriteInfo($"Deleted snapshot: {file}");
            }
            catch (Exception ex)
            {
                infoStream.WriteError($"Failed to delete snapshot: {file}", ex);
            }
        }
    }


}


public class MetaZipSnapshotStorage : ISnapshotStorage
{
    private readonly string path;

    public MetaZipSnapshotStorage(string path)
    {
        this.path = path;
    }

    public ISnapshot CreateSnapshot(IndexCommit commit)
    {
        string snapshotPath = Path.Combine(path, $"{commit.Generation:x8}.zip");
        ZipFileSnapshot snapshot = new ZipFileSnapshot(snapshotPath);
        return snapshot;
    }

    public IEnumerable<ISnapshot> LoadSnapshots()
    {
        return Directory.GetFiles(path, "*.zip")
            .Select(file => new ZipFileSnapshot(file))
            .OrderByDescending(f => f.Generation);
    }
}
public class MetaZipFileSnapshot : ISnapshot
{
    public long Generation { get; }
    public string FilePath { get; }

    public ISnapshotReader OpenReader() => new MetaZipSnapshotReader(this);

    public ISnapshotWriter OpenWriter() => new MetaZipSnapshotWriter(this);
    public MetaZipFileSnapshot(string path)
        : this(path, long.Parse(Path.GetFileNameWithoutExtension(path), NumberStyles.AllowHexSpecifier))
    {
    }

    public MetaZipFileSnapshot(string path, long generation)
    {
        FilePath = path;
        Generation = generation;
    }

    public void Delete()
    {
        File.Delete(FilePath);
    }

}

public class MetaZipSnapshotReader : Disposable, ISnapshotReader
{
    private readonly ZipArchive archive;
    public ISnapshot Snapshot { get; }

    public MetaZipSnapshotReader(string path)
        : this(new MetaZipFileSnapshot(path))
    { }

    public MetaZipSnapshotReader(MetaZipFileSnapshot snapshot)
    {
        this.Snapshot = snapshot;
        this.archive = ZipFile.Open(snapshot.FilePath, ZipArchiveMode.Read);
    }

    public IEnumerable<ISnapshotFile> ReadFiles()
    {
        EnsureNotDisposed();
        return archive.Entries.Select(entry => new MetaSnapshotFile(entry.Name, entry.Open));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            archive.Dispose();
        base.Dispose(disposing);
    }
}
public class MetaZipSnapshotWriter : Disposable, ISnapshotWriter
{
    private readonly ZipArchive archive;

    public ISnapshot Snapshot { get; }

    public MetaZipSnapshotWriter(string path)
        : this(new MetaZipFileSnapshot(path))
    {
    }

    public MetaZipSnapshotWriter(MetaZipFileSnapshot snapshot)
    {
        this.archive = ZipFile.Open(snapshot.FilePath, File.Exists(snapshot.FilePath) ? ZipArchiveMode.Update : ZipArchiveMode.Create);
        this.Snapshot = snapshot;
    }

    public async Task WriteFileAsync(string fileName, Stream stream)
    {
        EnsureNotDisposed();
        using Stream target = archive.CreateEntry(fileName).Open();
        await stream.CopyToAsync(target);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) archive?.Dispose();
        base.Dispose(disposing);
    }
}