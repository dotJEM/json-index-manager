using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Observable;
using DotJEM.Json.Storage.Adapter;
using DotJEM.ObservableExt;
using DotJEM.TaskScheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;

public interface IStorageAreaObserver
{
    IInfoStream InfoStream { get; }
    IForwarderObservable<IStorageChange> Observable { get; }
    Task RunAsync();
}
public class StorageAreaObserver : IStorageAreaObserver
{
    private readonly IStorageArea area;
    private readonly ITaskScheduler scheduler;
    private readonly IStorageAreaLog log;
    private readonly StorageObservable observable = new();

    private IScheduledTask task;
    private long generation = 0;
    private bool initialized = false;
    private readonly IInfoStream<StorageAreaObserver> infoStream = new DefaultInfoStream<StorageAreaObserver>();

    public IInfoStream InfoStream => infoStream;
    public IForwarderObservable<IStorageChange> Observable => observable;

    public StorageAreaObserver(IStorageArea area, ITaskScheduler scheduler)
    {
        this.area = area;
        this.scheduler = scheduler;
        this.log = area.Log;
    }
    
    public async Task RunAsync()
    {
        infoStream.WriteInfo($"Ingesting {area.Name}");
        task = scheduler.Schedule($"StorageAreaObserver:{area.Name}", _ => RunUpdateCheck(), "10sec");
        task.InfoStream.Forward(infoStream);
        await task;
    }

    public async Task StopAsync()
    {
        task.Dispose();
        await task;
        infoStream.WriteStorageObserverEvent(StorageObserverEventType.Stopped, $"Initializing for area '{area.Name}'.");
    }

    public void Initialize(long generation = 0)
    {
        this.generation = generation;
    }

    public void RunUpdateCheck()
    { 
        long latestGeneration = log.LatestGeneration;
        if (!initialized)
        {
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initializing, $"Initializing for area '{area.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            foreach (IChangeLogRow change in changes)
            {
                generation = change.Generation;
                if (change.Type != ChangeType.Faulty)
                    observable.Publish(new StorageChange(change) { Type = ChangeType.Create, LatestGeneration = latestGeneration });
            }
            initialized = true;
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initialized, $"Initialization complete for area '{area.Name}'.");
        }
        else
        {
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updating, $"Checking updates for area '{area.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            foreach (IChangeLogRow change in changes)
            {
                generation = change.Generation;
                if (change.Type != ChangeType.Faulty)
                    observable.Publish(new StorageChange(change) { LatestGeneration = latestGeneration });
            }
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updated, $"Done checking updates for area '{area.Name}'.");
        }
    }
}



public interface IStorageChange
{
    string Area { get; }
    long Generation { get; }
    long LatestGeneration { get; }
    ChangeType Type { get; set; }
    JObject Entity { get; }
}

public class StorageChange : IStorageChange
{
    public long Generation { get; }
    public long LatestGeneration { get;set; }
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
