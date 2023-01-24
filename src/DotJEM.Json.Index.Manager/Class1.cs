using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using DotJEM.Json.Index.Manager.Diagnostics;
using DotJEM.Json.Index.Manager.Observable;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Observable;
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

public class CommitStorageChange : IQueuedItem
{
}

public interface IStorageObserveable: IObservable<IStorageChange>
{

}

public interface IStorageManager
{
    IInfoStream InfoStream { get; }
    AbstractObserveable<IQueuedItem> Observeable { get; }
    Task Run();
}

public class StorageManager : IStorageManager
{
    private readonly IStorageContext context;
    private readonly StorageObserveable observeable = new StorageObserveable();
    public AbstractObserveable<IQueuedItem> Observeable => observeable;
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
                observer.Observeable.Forward(observeable);
                return observer.Run();
            })).ConfigureAwait(false);;
    }
}


public class StorageAreaObserver 
{
    private readonly IStorageArea area;
    private readonly IStorageAreaLog log;
    private readonly StorageObserveable observeable = new StorageObserveable();
    public AbstractObserveable<IQueuedItem> Observeable => observeable;

    public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageAreaObserver>();

    public StorageAreaObserver(IStorageArea area)
    {
        this.area = area;
        this.log = area.Log;
    }

    public Task Run()
    {
        return Task.Run(async () =>
        {
            long gen = 0;
            bool init = true;
            Console.WriteLine($"Ingesting {area.Name}");
            while (true)
            {
                using IStorageAreaLogReader changes = log.OpenLogReader(gen, !init);
                foreach (IChangeLogRow change in changes)
                {
                    gen = change.Generation;
                    observeable.Publish(new StorageChange(change));
                }

                init = false;
                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        });
    }
}


public class StorageObserveable : AbstractObserveable<IQueuedItem>
{
    public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageObserveable>();
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

        storage.Observeable
            .ForEachAsync(CaptureChange);

        context = new LuceneWriteContext(index);
    }

    public void Flush()
    {
        Console.WriteLine("Flushing buffers!");
        string buffer = $"[{watch.Elapsed}] {counter} ({counter / watch.Elapsed.TotalSeconds} / sec) => {exceptions}";
        Console.WriteLine(buffer);

        context.Flush(true, true, true);
        context.Commit();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        //context.Commit();
    }

    private long counter = 0;
    private long exceptions = 0;
    private Stopwatch watch = Stopwatch.StartNew();
    private readonly LuceneWriteContext context;

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
                        break;
                    case ChangeType.Update:
                        context.Write(chn.Entity);
                        break;
                    case ChangeType.Delete:
                        context.Delete(chn.Entity);
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
                exceptions++;
            }
            counter++;
            if(counter % 25000 != 0)
                return;
        }

        string buffer = $"[{watch.Elapsed}] {counter} ({counter / watch.Elapsed.TotalSeconds} / sec) => {exceptions}";
        Console.WriteLine(buffer);

    }
}

internal class LuceneWriteContext
{
    private readonly IStorageIndex index;
    private readonly IndexWriter writer;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;


    public LuceneWriteContext(IStorageIndex index)
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
}