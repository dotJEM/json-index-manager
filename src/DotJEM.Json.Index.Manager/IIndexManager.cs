using System;
using System.Reactive.Linq;
using System.Runtime;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.WriteContext;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;

namespace DotJEM.Json.Index.Manager;

public interface IWriteContextFactory
{
    WriteContext.ILuceneWriteContext Create();
}

public class WriteContextFactory : IWriteContextFactory
{
    private readonly IStorageIndex index;
    public WriteContextFactory(IStorageIndex index)
    {
        this.index = index;
    }
    public WriteContext.ILuceneWriteContext Create()
        => new SequentialLuceneWriteContext(index);
}

public interface IIndexManager
{
    IInfoStream InfoStream { get; }
}

public class IndexManager : IIndexManager
{
    private readonly IIndexSnapshotManager snapshots;

    private readonly WriteContext.ILuceneWriteContext context;
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;

    public IndexManager(IStorageManager storage, IIndexSnapshotManager snapshots, IWriteContextFactory writeContextFactory)
    {
        this.snapshots = snapshots;
        context = writeContextFactory.Create();
        storage.Observable.ForEachAsync(CaptureChange);

        storage.InfoStream.Forward(infoStream);
        snapshots.InfoStream.Forward(infoStream);
    }

    private void CaptureChange(IStorageChange change)
    {
        try
        {
            switch (change.Type)
            {
                case ChangeType.Create:
                    context.Create(change.Entity);
                    break;
                case ChangeType.Update:
                    context.Write(change.Entity);
                    break;
                case ChangeType.Delete:
                    context.Delete(change.Entity);
                    break;
                case ChangeType.Faulty:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            infoStream.WriteError($"Failed to ingest change from {change.Area}", ex);
        }
    }
}
