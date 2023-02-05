using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Observable;
using DotJEM.ObservableExt;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;

public interface IQueuedItem
{

}

public interface IStorageChange : IQueuedItem
{
    long Generation { get; }
    ChangeType Type { get; set; }
    string Area { get; }
    JObject Entity { get; }
}

public class StorageChange : IStorageChange
{
    public long Generation { get; }
    public ChangeType Type { get; set; }
    public string Area { get; }
    public JObject Entity { get; }

    public StorageChange(IChangeLogRow change)
    {
        Generation = change.Generation;
        Type = change.Type;
        Area = change.Area;
        Entity = change.CreateEntity();
    }
}


public interface IStorageObservable: IObservable<IStorageChange>
{

}

public interface IStorageManager
{
    IInfoStream InfoStream { get; }
    IForwarderObservable<IQueuedItem> Observable { get; }
    Task Run();
}

public class StorageManager : IStorageManager
{
    private readonly IStorageContext context;
    private readonly StorageObservable observable = new StorageObservable();

    public IForwarderObservable<IQueuedItem> Observable => observable;

    public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageManager>();

    public StorageManager(IStorageContext context)
    {
        this.context = context;
    }

    public async Task Run()
    {
        Console.WriteLine("Starting Storage Manger");
        await Task.WhenAll(context
            .AreaInfos
            .Select(info => {
                StorageAreaObserver observer = new StorageAreaObserver(context.Area(info.Name));
                observer.Observable.Forward(observable);
                return observer.Run();
            })).ConfigureAwait(false);;
    }
}


public class StorageAreaObserver 
{
    private readonly IStorageArea area;
    private readonly IStorageAreaLog log;
    private readonly StorageObservable observable = new StorageObservable();

    public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageAreaObserver>();

    public IForwarderObservable<IQueuedItem> Observable => observable;

    public StorageAreaObserver(IStorageArea area)
    {
        this.area = area;
        this.log = area.Log;
    }

    public Task Run()
    {
        // TODO: Use a scheduler instead.
        return Task.Run(async () =>
        {
            Console.WriteLine($"Ingesting {area.Name}");
            while (true)
            {
                RunUpdateCheck();
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        });
    }

    private long generation = 0;
    private bool initialized = false;

    public void Initialize(long generation = 0)
    {
        this.generation = generation;
    }

    public void RunUpdateCheck()
    {
        if (initialized)
        {
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            foreach (IChangeLogRow change in changes)
            {
                generation = change.Generation;
                observable.Publish(new StorageChange(change));
            }
        }
        else
        {
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            foreach (IChangeLogRow change in changes)
            {
                generation = change.Generation;
                if(change.Type != ChangeType.Faulty)
                    observable.Publish(new StorageChange(change) { Type = ChangeType.Create });
            }
            initialized = true;
        }

    }
}


public class StorageObservable : AbstractObservable<IQueuedItem>
{
    public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageObservable>();
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
        context = new ParallelLuceneWriteContext(index);
        //context = new SequentialLuceneWriteContext(index);
    }

    public void Flush()
    {
        Console.WriteLine("Flushing buffers!");
        Reporter.Report();

        context.Flush(true, true, true);
        context.Commit();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        //context.Commit();
    }

    private readonly ILuceneWriteContext context;

    private void CaptureChange(IQueuedItem obj)
    {
        if (obj is IStorageChange chn)
        {
            if(chn.Type == ChangeType.Faulty)
                return;

            try
            {
                switch (chn.Type)
                {
                    case ChangeType.Create:
                        context.Create(chn.Entity);
                        //Reporter.Increment("Create");
                        break;
                    case ChangeType.Update:
                        context.Write(chn.Entity);
                        //Reporter.Increment("Update");
                        break;
                    case ChangeType.Delete:
                        context.Delete(chn.Entity);
                        //Reporter.Increment("Delete");
                        break;
                    case ChangeType.Faulty:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            }
            catch (Exception e)
            {
                Directory.CreateDirectory($"app_data/exceptions/{chn.Area}");
                File.WriteAllText($"app_data/exceptions/{chn.Area}/{chn.Entity["id"]}.json", JObject.FromObject(
                    new
                    {
                        exception = e,
                        change = chn
                    }
                    ).ToString());
                Reporter.IncrementExceptions();
            }
            //Reporter.Increment("LOADED");
        }
    }
}

public static class Reporter
{
    private static long exceptions;
    private static Stopwatch watch = Stopwatch.StartNew();
    private static ConcurrentDictionary<string, long> counters = new();

    public static void IncrementExceptions()
    {
        Interlocked.Increment(ref exceptions);
    }

    public static void Increment(string counter)
    {
        long count = counters.AddOrUpdate(counter, _ => 1, (_, v) => v + 1);
        if(count % 25000 == 0) Console.WriteLine($"{counter} [{watch.Elapsed}] {count} ({count / watch.Elapsed.TotalSeconds} / sec) => {exceptions}");
    }

    public static void Report()
    {
        Console.WriteLine($"COUNTERS:");
        foreach (var counter in counters)
        {
            Console.WriteLine($"{counter.Key} [{watch.Elapsed}] {counter.Value} ({counter.Value / watch.Elapsed.TotalSeconds} / sec) => {exceptions}");
        }
    }
}

internal interface ILuceneWriteContext
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

    public SequentialLuceneWriteContext(IStorageIndex index)
    {
        this.index = index;
        this.writer = index.Storage.Writer;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;

        var buffer = writer.GetRAMBufferSizeMB();
        writer.SetRAMBufferSizeMB(1000);

    }

    public void Write(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Document doc = mapper.Create(entity);
        writer.UpdateDocument(term, doc);
        Reporter.Increment("WRITTEN");
    }


    public void Create(JObject entity)
    {
        Document doc = mapper.Create(entity);
        writer.AddDocument(doc);
        Reporter.Increment("WRITTEN");
    }


    public void Delete(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        writer.DeleteDocuments(term);
        Reporter.Increment("WRITTEN");
    }

    public void Commit()
    {
        writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes)
    {
        writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    }
}


internal class ParallelLuceneWriteContext : ILuceneWriteContext
{
    private readonly IStorageIndex index;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;

    private readonly IndexWriter writer;
    private readonly IngestPipeline<IWriterCommand> pipeline = new IngestPipeline<IWriterCommand>();

    public ParallelLuceneWriteContext(IStorageIndex index)
    {
        this.index = index;
        this.writer = index.Storage.Writer;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;

        var buffer = writer.GetRAMBufferSizeMB();
        writer.SetRAMBufferSizeMB(1000);

        pipeline.Observable
            .Buffer(TimeSpan.FromSeconds(2))
            .ForEachAsync(OnNext2);
    }

    private void OnNext2(IList<IWriterCommand> obj)
    {
        foreach (IWriterCommand command in obj)
        {
            command.Apply(writer);
        }
    }


    public interface IWriterCommand 
    {
        void Apply(IndexWriter indexWriter);
    }
    public class WriteCommand : IWriterCommand
    {
        private readonly Term term;
        private readonly Document document;

        public WriteCommand(Term term, Document document)
        {
            this.term = term;
            this.document = document;
        }

        public void Apply(IndexWriter writer)
        {
            writer.UpdateDocument(term, document);
            Reporter.Increment("WRITTEN");
        }
    }

    public class WriteTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IIdentityResolver resolver;
        private readonly IDocumentFactory mapper;

        public WriteTask(JObject entity, IIdentityResolver resolver, IDocumentFactory mapper)
        {
            this.entity = entity;
            this.resolver = resolver;
            this.mapper = mapper;
        }

        public bool Completed { get; private set;  }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Term term = resolver.CreateTerm(entity);
            Document doc = mapper.Create(entity);
            Value = new WriteCommand(term, doc);
            Completed = true;
            entity = null;
        }
    }

    public void Write(JObject entity)
    {
        pipeline.Enqueue(new WriteTask(entity, resolver, mapper));
    }

    public class CreateCommand : IWriterCommand
    {
        private readonly Document document;

        public CreateCommand(Document document)
        {
            this.document = document;
        }

        public void Apply(IndexWriter writer)
        {
            writer.AddDocument(document);
            Reporter.Increment("WRITTEN");
        }
    }

    public class CreateTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IDocumentFactory mapper;

        public CreateTask(JObject entity, IDocumentFactory mapper)
        {
            this.entity = entity;
            this.mapper = mapper;
        }

        public bool Completed { get; private set; }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Document doc =  mapper.Create(entity);
            Value = new CreateCommand(doc);
            Completed = true;
            entity = null;
        }
    }

    public void Create(JObject entity)
    {
        pipeline.Enqueue(new CreateTask(entity, mapper));
    }

    public class DeleteCommand : IWriterCommand
    {
        private readonly Term term;

        public DeleteCommand(Term term)
        {
            this.term = term;
        }

        public void Apply(IndexWriter writer)
        {
            writer.DeleteDocuments(term);
            Reporter.Increment("WRITTEN");
        }
    }

    public class DeleteTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IIdentityResolver resolver;

        public DeleteTask(JObject entity, IIdentityResolver resolver)
        {
            this.entity = entity;
            this.resolver = resolver;
        }


        public bool Completed { get;private set; }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Term term = resolver.CreateTerm(entity);
            Value = new DeleteCommand(term);
            Completed = true;
            entity = null;
        }
    }

    public void Delete(JObject entity)
    {
        pipeline.Enqueue(new DeleteTask(entity, resolver));
    }

    public void Commit()
    {
        writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes)
    {
        writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    }
}

internal class IngestPipeline<TOutput> : IDisposable
{
    private readonly int capacity;
    private readonly ConcurrentQueue<IIngestTask<TOutput>> items = new();
    private readonly AutoResetEvent enqueueGate = new AutoResetEvent(false);
    private readonly AutoResetEvent completionGate = new AutoResetEvent(false);
    private bool disposed;

    public IForwarderObservable<TOutput> Observable { get; } = new Publisher();

    public IngestPipeline(int capacity = 256 * 1024)
    {
        this.capacity = capacity;
        //Thread transferThread = new(TransferCompleted);
        //transferThread.Start();
        Task.Run(TransferCompleted);
    }

    public void Enqueue(IIngestTask<TOutput> item)
    {
        if (items.Count >= capacity)
            enqueueGate.WaitOne();

        items.Enqueue(item);
        Task.Run(item.Execute);
    }

    private void MaybeSetEnqueueGate()
    {
        if (items.Count < capacity / 2)
            enqueueGate.Set();
    }

    private async Task TransferCompleted()
    {
        while (!disposed)
        {
            if (!items.TryDequeue(out IIngestTask<TOutput> task))
            {
                MaybeSetEnqueueGate();
                await Task.Delay(2000);
                continue;
            }

            while (!task.Completed)
            {
                MaybeSetEnqueueGate();
                await Task.Delay(2000);
            }

            Observable.Publish(task.Value);
        }
    }

    public void Dispose()
    {
        enqueueGate?.Dispose();
        completionGate?.Dispose();
        disposed = true;
    }

    private class Publisher : AbstractObservable<TOutput> { }
}

internal interface IIngestTask<out TOutput>
{
    bool Completed { get; }

    TOutput Value { get; }
    public void Execute();
}
