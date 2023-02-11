using System.Reactive.Linq;
using DotJEM.Json.Index.Manager.Snapshots;
using static Lucene.Net.Documents.Field;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Storage;
using DotJEM.TaskScheduler;
using System.Collections.Concurrent;
using System.Diagnostics;
using System;
using System.Linq;
using DotJEM.Json.Index.Manager.Snapshots.Zip;

namespace DotJEM.Json.Index.Manager;
public interface IStorageHost
{
    IInfoStream InfoStream { get; }
}

public class StorageHost : IStorageHost
{
    private readonly InfoStream<StorageHost> infoStream = new ();
    private readonly IStorageManager storage;
    private readonly IIndexManager index;

    public IInfoStream InfoStream => infoStream;

    public StorageHost(IStorageContext storage, IStorageIndex index, ITaskScheduler scheduler)
    {
        this.storage = new StorageManager(storage, scheduler);
        this.index = new IndexManager(this.storage, new IndexSnapshotManager(new ZipSnapshotStrategy("")), new WriteContextFactory(index));

    }

    public async Task RunAsync()
    {
        await Task.WhenAll(
            storage.Observable.ForEachAsync(Reporter.Capture),
            storage.InfoStream.ForEachAsync(Reporter.CaptureInfo),
            index.InfoStream.ForEachAsync(Reporter.CaptureInfo),
            Task.Run(storage.RunAsync)
        );
    }
}




//IStorageManager storageManager = new StorageManager(storage, new DotJEM.TaskScheduler.TaskScheduler());
//IIndexManager manager = new IndexManager(storageManager, new IndexSnapshotManager(), new WriteContextFactory(index));

//Task run = Task.WhenAll(
//    storageManager.Observable.ForEachAsync(Reporter.Capture),
//    storageManager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
//    manager.InfoStream.ForEachAsync(Reporter.CaptureInfo),
//    Task.Run(() => storageManager.RunAsync())
//);

public static class Reporter
{
    private static Stopwatch watch = Stopwatch.StartNew();
    private static ConcurrentDictionary<string, long> counters = new();
    private static ConcurrentDictionary<string, GenerationInfo> generations = new();

    public static void Increment(string counter, long currentGen, long latestGen)
    {
        long count = counters.AddOrUpdate(counter, _ => 1, (_, v) => v + 1);
        
        if (count % 25000 != 0) return;
        Console.WriteLine($"{counter} [{watch.Elapsed}] {currentGen:N0} of {latestGen:N0} changes processed, {count:N0} objects indexed. ({count / watch.Elapsed.TotalSeconds:F} / sec)");
    }

    public static void Report()
    {
        Console.WriteLine($"COUNTERS:");
        foreach (var kv in counters)
        {
            var counter = kv.Key;
            var count = kv.Value;
            var gen = generations.GetOrAdd(counter, _ => new GenerationInfo());
            var currentGen = gen.Current;
            var latestGen = gen.Latest;
            Console.WriteLine($"{counter} [{watch.Elapsed}] {currentGen:N0} of {latestGen:N0} changes processed, {count:N0} objects indexed. ({count / watch.Elapsed.TotalSeconds:F} / sec)");
        }
    }

    public static void CaptureInfo(IInfoStreamEvent evt)
    {
        if (evt is StorageObserverInfoStreamEvent sevt)
        {
            switch (sevt.EventType)
            {
                case StorageObserverEventType.Initializing:
                    Console.WriteLine(evt);
                    break;
                case StorageObserverEventType.Initialized:
                    Console.WriteLine(evt);
                    break;
                case StorageObserverEventType.Updating:
                    break;
                case StorageObserverEventType.Updated:
                    break;
                case StorageObserverEventType.Stopped:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

    }

    public static void Capture(IStorageChange change)
    {
        generations.AddOrUpdate(change.Area, 
            _ => change.Generation,
            (_, _) => change.Generation);
        GenerationInfo sum = generations.Values.Aggregate((x, y) => x + y);
        Increment("LOADED", sum.Current, sum.Latest);
        Increment(change.Area, change.Generation.Current, change.Generation.Latest);
    }
    
}
