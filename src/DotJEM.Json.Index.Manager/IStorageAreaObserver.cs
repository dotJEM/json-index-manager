using System;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Configuration;
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
    private readonly IWebBackgroundTaskScheduler scheduler;
    private readonly IStorageAreaWatchConfiguration configuration;
    private readonly IStorageAreaLog log;
    private readonly ForwarderObservable<IStorageChange> observable = new();

    private IScheduledTask task;
    private long generation = 0;
    private bool initialized = false;
    private readonly IInfoStream<StorageAreaObserver> infoStream = new InfoStream<StorageAreaObserver>();

    public IInfoStream InfoStream => infoStream;
    public IForwarderObservable<IStorageChange> Observable => observable;

    public StorageAreaObserver(IStorageArea area, IWebBackgroundTaskScheduler scheduler, IStorageAreaWatchConfiguration configuration)
    {
        this.area = area;
        this.scheduler = scheduler;
        this.configuration = configuration;
        this.log = area.Log;
    }
    
    public async Task RunAsync()
    {
        infoStream.WriteStorageObserverEvent(StorageObserverEventType.Starting, area.Name, $"Ingest starting for area '{area.Name}'.");
        task = scheduler.Schedule($"StorageAreaObserver:{area.Name}", _ => RunUpdateCheck(), configuration.Interval);
        task.InfoStream.Forward(infoStream);
        await task;
    }

    public async Task StopAsync()
    {
        task.Dispose();
        await task;
        infoStream.WriteStorageObserverEvent(StorageObserverEventType.Stopped, area.Name, $"Initializing for area '{area.Name}'.");
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
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initializing, area.Name, $"Initializing for area '{area.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            PublishChanges(changes, _ => ChangeType.Create);
            initialized = true;
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initialized, area.Name, $"Initialization complete for area '{area.Name}'.");
        }
        else
        {
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updating, area.Name, $"Checking updates for area '{area.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            PublishChanges(changes, row => row.Type);
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updated, area.Name, $"Done checking updates for area '{area.Name}'.");
        }

        void PublishChanges(IStorageAreaLogReader changes, Func<IChangeLogRow, ChangeType> changeTypeGetter) 
        {
            foreach (IChangeLogRow change in changes)
            {
                generation = change.Generation;
                if (change.Type == ChangeType.Faulty)
                    continue;

                observable.Publish(new StorageChange(change.Area, changeTypeGetter(change), change.CreateEntity(), new GenerationInfo(change.Generation, latestGeneration)));
            }
        }
    }
}

public struct GenerationInfo
{
    public long Current { get; }
    public long Latest { get; }

    public GenerationInfo(long current, long latest)
    {
        Current = current;
        Latest = latest;
    }

    public static GenerationInfo operator + (GenerationInfo left, GenerationInfo right)
    {
        return new GenerationInfo(left.Current + right.Current, left.Latest + right.Latest);
    }
}

public interface IStorageChange
{
    string Area { get; }
    GenerationInfo Generation { get; }
    ChangeType Type { get; }
    JObject Entity { get; }
}

public struct StorageChange : IStorageChange
{
    public GenerationInfo Generation { get; }
    public ChangeType Type { get; }
    public string Area { get; }
    public JObject Entity { get; }

    public StorageChange(string area, ChangeType type, JObject entity, GenerationInfo generationInfo)
    {
        Generation = generationInfo;
        Type = type;
        Area = area;
        Entity = entity;
    }
}
