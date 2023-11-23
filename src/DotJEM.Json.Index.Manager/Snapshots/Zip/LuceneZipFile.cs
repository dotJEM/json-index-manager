using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Index2.Snapshots;
using DotJEM.ObservableExtensions.InfoStreams;

namespace DotJEM.Json.Index.Manager.Snapshots.Zip;

public class LuceneZipFile : ISnapshotFile
{
    private readonly ZipArchive archive;
    private readonly IInfoStream<LuceneZipFile> infoStream = new InfoStream<LuceneZipFile>();
    
    public IInfoStream InfoStream => infoStream;
    public string Name { get; }

    public LuceneZipFile(string fileName, ZipArchive archive)
    {
        this.Name = fileName;
        this.archive = archive;
    }

    public Stream Open()
    {
        infoStream.WriteFileOpenEvent(this, $"Restoring file {Name}.");
        ZipStreamWrapper wrapper = new ZipStreamWrapper(archive.GetEntry(Name)?.Open(), this);
        wrapper.InfoStream.Subscribe(infoStream);
        return wrapper;
    }

    private class ZipStreamWrapper : Stream
    {
        private readonly Stream inner;
        private readonly LuceneZipFile file;
        private readonly IInfoStream<ZipStreamWrapper> info = new InfoStream<ZipStreamWrapper>();

        public IInfoStream InfoStream => info;
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

        public ZipStreamWrapper(Stream inner, LuceneZipFile file)
        {
            this.inner = inner;
            this.file = file;
        }

        protected override void Dispose(bool disposing)
        {
            info.WriteFileCloseEvent(file, $"File {file.Name} restored.");
            base.Dispose(disposing);
            inner.Dispose();
        }
        public override void Close()
        {

            inner.Close();
            base.Close();
        }

        public override object InitializeLifetimeService() => inner.InitializeLifetimeService();
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => inner.CopyToAsync(destination, bufferSize, cancellationToken);
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