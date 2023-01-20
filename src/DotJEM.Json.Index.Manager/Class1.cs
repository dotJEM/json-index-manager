using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;
using DotJEM.Json.Index.Manager.Diagnostics;
using DotJEM.Json.Index.Manager.Observable;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager
{
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

            await Task.WhenAll(context
                .AreaInfos
                .Select(info => {
                    StorageAreaObserver observer = new StorageAreaObserver(context.Area(info.Name));
                    observer.Observeable.Forward(observeable);
                    return observer.Run();
                }));
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

        public async Task Run()
        {
            Process process = Process.GetCurrentProcess();
            while (true)
            {
                IStorageChangeCollection changes = log.Get(false, 10000);

                foreach (IChangeLogRow change in changes.Partitioned)
                    observeable.Publish(new StorageChange(change));

                process.Refresh();
                if (process.PrivateMemorySize64 > (long)int.MaxValue * 4)
                {
                    observeable.Publish(new CommitStorageChange());
                    while (process.PrivateMemorySize64 > (long)int.MaxValue * 2)
                    {
                        Console.WriteLine("Working set above treshold, pausing fetch");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect();
                        process.Refresh();
                    }
                }

                if (changes.Count < 50000)
                    await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
    }


    public class StorageObserveable : AbstractObserveable<IQueuedItem>
    {
        public IInfoStream InfoStream { get; } = new DefaultInfoStream<StorageObserveable>();
    }

    public interface IIndexManager
    {

    }

    public class IndexManager : IIndexManager
    {
        private readonly IStorageManager storageManager;
        private readonly IStorageIndex index;
        public IInfoStream InfoStream { get; } = new DefaultInfoStream<IndexManager>();

        public IndexManager(IStorageManager storage, IStorageIndex index)
        {
            this.storageManager = storage;

            storage.Observeable.ForEachAsync(CaptureChange);

            context = index.Writer.WriteContext();
        }

        private long counter = 0;
        private Stopwatch watch = Stopwatch.StartNew();
        private readonly ILuceneWriteContext context;

        private void CaptureChange(IQueuedItem obj)
        {
            if (obj is CommitStorageChange)
            {
                context.Commit();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                return;
            }

            if (obj is IStorageChange chn)
            {
                context.Write(chn.Entity);
                counter++;
                if(counter % 10000 != 0)
                    return;
            }


            string buffer = $"[{watch.Elapsed}] {counter} ({counter / watch.Elapsed.TotalSeconds} / sec)";
            Console.WriteLine(buffer);

        }
    }
}
