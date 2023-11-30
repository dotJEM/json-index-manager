using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Index.Manager.Snapshots.Zip.Meta;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.Json.Index2.Snapshots.Zip;
using DotJEM.Json.Index2.Util;
using DotJEM.ObservableExtensions.InfoStreams;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class ZipSnapshotStrategy : ISnapshotStrategy
{
    private readonly InfoStream<ZipSnapshotStrategy> infoStream = new();
    public IInfoStream InfoStream => infoStream;

    private readonly int maxSnapshots;
    private readonly MetaZipSnapshotStorage storage;
    public ISnapshotStorage Storage => storage;

    public ZipSnapshotStrategy(string path, int maxSnapshots = 2)
    {
        this.maxSnapshots = maxSnapshots;
        this.storage = new MetaZipSnapshotStorage(path);
        this.storage.InfoStream.Subscribe(infoStream);
    }

    public void CleanOldSnapshots()
    {
        foreach (ISnapshot snapshot in Storage.LoadSnapshots().Skip(maxSnapshots))
        {
            try
            {
                snapshot.Delete();
                infoStream.WriteInfo($"Deleted snapshot: {snapshot}");
            }
            catch (Exception ex)
            {
                infoStream.WriteError($"Failed to delete snapshot: {snapshot}", ex);
            }
        }
    }
}


public class MetaZipSnapshotStorage : ISnapshotStorage
{
    private readonly InfoStream<MetaZipSnapshotStorage> infoStream = new();
    public IInfoStream InfoStream => infoStream;

    private readonly string path;

    public MetaZipSnapshotStorage(string path)
    {
        this.path = path;
    }

    public ISnapshot CreateSnapshot(IndexCommit commit)
    {
        string snapshotPath = Path.Combine(path, $"{commit.Generation:x8}.zip");
        return CreateSnapshot(snapshotPath);
    }

    public IEnumerable<ISnapshot> LoadSnapshots()
    {
        return Directory.GetFiles(path, "*.zip")
            .Select(CreateSnapshot)
            .OrderByDescending(f => f.Generation);
    }

    private ISnapshot CreateSnapshot(string path)
    {
        MetaZipFileSnapshot snapshot = new MetaZipFileSnapshot(path);
        snapshot.InfoStream.Subscribe(infoStream);
        return snapshot;
    }
}
public class MetaZipFileSnapshot : ISnapshot
{
    private readonly InfoStream<MetaZipFileSnapshot> infoStream = new();
    public IInfoStream InfoStream => infoStream;


    public long Generation { get; }
    public string FilePath { get; }

    public ISnapshotReader OpenReader()
    {
        infoStream.WriteSnapshotOpenEvent(this, "");
        MetaZipSnapshotReader reader = new(this);
        reader.InfoStream.Subscribe(infoStream);
        return reader;
    }

    public ISnapshotWriter OpenWriter()
    {
       MetaZipSnapshotWriter writer= new(this);
       writer.InfoStream.Subscribe(infoStream);
       return writer;
    }

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

    public bool Verify()
    {
        return true;
    }

}

public class MetaZipSnapshotReader : Disposable, ISnapshotReader
{
    private readonly InfoStream<MetaZipSnapshotReader> infoStream = new();
    public IInfoStream InfoStream => infoStream;

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
        return archive.Entries.Select(entry =>
        {
            MetaSnapshotFile file = new (entry);
            file.InfoStream.Subscribe(infoStream);
            infoStream.WriteFileOpenEvent(file, $"Restoring file {entry.Name}.", new FileProgress(entry.Length, 0));
            return file;
        });
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
    private readonly InfoStream<MetaZipSnapshotWriter> infoStream = new();
    public IInfoStream InfoStream => infoStream;

    private readonly ZipArchive archive;

    public ISnapshot Snapshot { get; }

    private List<string> files = new List<string>();

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
        files.Add(fileName);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) archive?.Dispose();
        base.Dispose(disposing);
    }
}


public class MetaSnapshotFile : ISnapshotFile
{
    private readonly InfoStream<MetaSnapshotFile> infoStream = new();
    public IInfoStream InfoStream => infoStream;

    private readonly ZipArchiveEntry entry;

    public string Name => entry.Name;

    public MetaSnapshotFile(ZipArchiveEntry entry)
    {
        this.entry = entry;
    }

    public Stream Open()
    {
        ZipStreamWrapper wrapper = new ZipStreamWrapper(entry, this);
        wrapper.InfoStream.Subscribe(infoStream);
        infoStream.WriteFileProgressEvent(this, $"Restoring file {Name}.", new FileProgress(entry.Length, 0));
        return wrapper;
    }

    private class ZipStreamWrapper : Stream
    {
        private readonly InfoStream<ZipStreamWrapper> infoStream = new();
        public IInfoStream InfoStream => infoStream;

        private readonly ZipArchiveEntry entry;
        private readonly Stream inner;
        private readonly MetaSnapshotFile file;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanTimeout => inner.CanTimeout;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int ReadTimeout
        {
            get => inner.ReadTimeout;
            set => inner.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => inner.WriteTimeout;
            set => inner.WriteTimeout = value;
        }

        public ZipStreamWrapper(ZipArchiveEntry entry, MetaSnapshotFile file)
        {
            this.entry = entry;
            this.file = file;
            this.inner = entry.Open();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            inner.Dispose();
            infoStream.WriteFileCloseEvent(file, $"File {file.Name} restored.", new FileProgress(entry.Length, entry.Length));
        }

        public override void Close()
        {
            inner.Close();
            base.Close();
        }

        public override object InitializeLifetimeService() => inner.InitializeLifetimeService();
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            long size = entry.Length;
            long copied = 0;

            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = await ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                copied += bytesRead;
                infoStream.WriteFileProgressEvent(file, $"File {file.Name} is being restored.", new FileProgress(size, copied));
            }
        }

        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => inner.BeginRead(buffer, offset, count, callback, state);
        public override int EndRead(IAsyncResult asyncResult) => inner.EndRead(asyncResult);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => inner.BeginWrite(buffer, offset, count, callback, state);
        public override void EndWrite(IAsyncResult asyncResult) => inner.EndWrite(asyncResult);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int ReadByte() => inner.ReadByte();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void WriteByte(byte value) => inner.WriteByte(value);
    }
}