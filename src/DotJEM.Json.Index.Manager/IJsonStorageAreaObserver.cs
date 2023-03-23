using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.Json.Storage.Adapter.Observable;
using DotJEM.Json.Storage.Adapter;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;

public interface IJsonStorageAreaObserver
{
    string AreaName { get; }
    IInfoStream InfoStream { get; }
    IObservable<IStorageChange> Observable { get; }
    Task RunAsync();
    void UpdateGeneration(long generation);
}

public class JsonStorageAreaObserver : IJsonStorageAreaObserver
{
    private readonly string pollInterval;
    private readonly IWebTaskScheduler scheduler;
    private readonly IStorageAreaLog log;
    private readonly Subject<IStorageChange> observable = new();
    private readonly IInfoStream<JsonStorageAreaObserver> infoStream = new InfoStream<JsonStorageAreaObserver>();

    private long generation = 0;
    private bool initialized = false;
    private IScheduledTask task;
    public IStorageArea StorageArea { get; }

    public string AreaName => StorageArea.Name;
    public IInfoStream InfoStream => infoStream;
    public IObservable<IStorageChange> Observable => observable;

    public JsonStorageAreaObserver(IStorageArea storageArea, IWebTaskScheduler scheduler, string pollInterval = "10s")
    {
        this.StorageArea = storageArea;
        this.scheduler = scheduler;
        this.pollInterval = pollInterval;
        this.log = storageArea.Log;
    }
    
    public async Task RunAsync()
    {
        infoStream.WriteStorageObserverEvent(StorageObserverEventType.Starting, StorageArea.Name, $"Ingest starting for storageArea '{StorageArea.Name}'.");
        task = scheduler.Schedule($"JsonStorageAreaObserver:{StorageArea.Name}", _ => RunUpdateCheck(), pollInterval);
        task.InfoStream.Subscribe(infoStream);
        await task;
    }

    public async Task StopAsync()
    {
        task.Dispose();
        await task;
        infoStream.WriteStorageObserverEvent(StorageObserverEventType.Stopped, StorageArea.Name, $"Initializing for storageArea '{StorageArea.Name}'.");
    }

    public void UpdateGeneration(long value)
    {
        this.generation = value;
        this.initialized = true;
    }

    public void RunUpdateCheck()
    { 
        long latestGeneration = log.LatestGeneration;
        if (!initialized)
        {
            BeforeInitialize();
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initializing, StorageArea.Name, $"Initializing for storageArea '{StorageArea.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            PublishChanges(changes, _ => ChangeType.Create);
            initialized = true;
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Initialized, StorageArea.Name, $"Initialization complete for storageArea '{StorageArea.Name}'.");
            AfterInitialize();
        }
        else
        {
            BeforeUpdate();
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updating, StorageArea.Name, $"Checking updates for storageArea '{StorageArea.Name}'.");
            using IStorageAreaLogReader changes = log.OpenLogReader(generation, initialized);
            PublishChanges(changes, row => row.Type);
            infoStream.WriteStorageObserverEvent(StorageObserverEventType.Updated, StorageArea.Name, $"Done checking updates for storageArea '{StorageArea.Name}'.");
            AfterUpdate();
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
    public virtual void BeforeInitialize() { }
    public virtual void AfterInitialize() { }

    public virtual void BeforeUpdate() {}
    public virtual void AfterUpdate() {}
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
