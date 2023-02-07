using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Observable;
using DotJEM.ObservableExt;
using DotJEM.TaskScheduler;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;

public interface IStorageObservable : IObservable<IStorageChange> { }


public static class IndexManagerInfoStreamExtensions
{
    public static void WriteObjectLoaded<TSource>(this IInfoStream<TSource> self, string message, [CallerMemberName] string callerMemberName = null, [CallerFilePath] string callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        self.WriteEvent(new StorageObjectsLoadedInfoStreamEvent<TSource>("INFO", message, callerMemberName, callerFilePath, callerLineNumber));
    }

}

public class StorageObjectsLoadedInfoStreamEvent<TSource> : InfoStreamEvent<TSource>
{
    public StorageObjectsLoadedInfoStreamEvent(string level, string message, string callerMemberName, string callerFilePath, int callerLineNumber)
        : base(level, message, callerMemberName, callerFilePath, callerLineNumber)
    {
    }
}

public class StorageObservable : AbstractObservable<IStorageChange>
{
}

public interface IIndexManager
{
    void Flush();
}

public class IndexManager : IIndexManager
{
    private readonly IStorageManager storageManager;
    private readonly IStorageIndex index;

    public IInfoStream InfoStream { get; } = new DefaultInfoStream<IndexManager>();

    public IndexManager(IStorageManager storage, IStorageIndex index)
    {
        this.storageManager = storage;
        this.index = index;

        storage.Observable.ForEachAsync(CaptureChange);
        //context = new ParallelLuceneWriteContext(index);
        context = new SequentialLuceneWriteContext(index);
    }

    public void Flush()
    {
        Console.WriteLine("Flushing buffers!");

        context.Flush(true, true, true);
        context.Commit();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        //context.Commit();
    }

    private readonly ILuceneWriteContext context;

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
        catch (Exception e)
        {

        }
    }
}

internal interface ILuceneWriteContext : IDisposable
{
    void Write(JObject entity);
    void Create(JObject entity);
    void Delete(JObject entity);
    void Commit();
    void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes);
}

internal class SequentialLuceneWriteContext : ILuceneWriteContext
{
    private readonly IStorageIndex index;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;

    private readonly IndexWriter writer;
    private readonly double originalBufferSize;

    public SequentialLuceneWriteContext(IStorageIndex index)
    {
        this.index = index;
        this.writer = index.Storage.Writer;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;

        originalBufferSize = writer.GetRAMBufferSizeMB();
        writer.SetRAMBufferSizeMB(1000);
    }

    public void Write(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Document doc = mapper.Create(entity);
        writer.UpdateDocument(term, doc);
    }


    public void Create(JObject entity)
    {
        Document doc = mapper.Create(entity);
        writer.AddDocument(doc);
    }

    public void Delete(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        writer.DeleteDocuments(term);
    }

    public void Commit()
    {
        writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes)
    {
        writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    }

    public void Dispose()
    {
        writer?.Dispose();
        writer?.SetRAMBufferSizeMB(originalBufferSize);
    }
}