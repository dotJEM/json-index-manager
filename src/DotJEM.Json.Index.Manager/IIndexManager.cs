using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.WriteContexts;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.TaskScheduler;

namespace DotJEM.Json.Index.Manager;

public interface IWriteContextFactory
{
    WriteContexts.ILuceneWriteContext Create();
}

public class WriteContextFactory : IWriteContextFactory
{
    private readonly IStorageIndex index;
    private readonly IWriteContextConfiguration configuration;

    public WriteContextFactory(IStorageIndex index, IWriteContextConfiguration configuration = null)
    {
        this.index = index;
        this.configuration = configuration ?? new DefaultWriteContextConfiguration();
    }
    public WriteContexts.ILuceneWriteContext Create()
        => new SequentialLuceneWriteContext(index, configuration.RamBufferSize);
}

public interface IIndexManager
{
    IInfoStream InfoStream { get; }

    Task RunAsync();
    Task<bool> TakeSnapshotAsync();
}



public class IndexManager : IIndexManager
{
    private readonly IStorageManager storage;
    private readonly IIndexSnapshotManager snapshots;
    private readonly IIndexIngestProgressTracker tracker;

    private readonly WriteContexts.ILuceneWriteContext context;
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;

    public IndexManager(IStorageContext context, IStorageIndex index, ISnapshotStrategy snapshotStrategy,
        IWebBackgroundTaskScheduler scheduler, IIndexManagerConfiguration configuration)
      : this(new StorageManager(context, scheduler, configuration.StorageConfiguration),
          new IndexSnapshotManager(index, snapshotStrategy),
          new WriteContextFactory(index, configuration.WriterConfiguration))
    { }

    public IndexManager(IStorageManager storage, IIndexSnapshotManager snapshots, IWriteContextFactory writeContextFactory)
    {
        this.storage = storage;
        this.snapshots = snapshots;
        context = writeContextFactory.Create();
        storage.Observable.ForEachAsync(CaptureChange);
        storage.InfoStream.Forward(infoStream);
        snapshots.InfoStream.Forward(infoStream);

        tracker = new IndexIngestProgressTracker();
        storage.InfoStream.Subscribe(tracker);
        storage.Observable.Subscribe(tracker);
        snapshots.InfoStream.Subscribe(tracker);

        tracker.InfoStream.ForEachAsync(CheckComplete);
        tracker.InfoStream.Forward(infoStream);
    }

    
    private void CheckComplete(IInfoStreamEvent ise)
    {
        if (ise is not StorageIngestStateInfoStreamEvent evt)
            return;

        StorageIngestState state = evt.State;
        StorageObserverEventType[] states = state.Areas
            .Select(x => x.LastEvent)
            .ToArray();

        if (states.All(state => state is StorageObserverEventType.Updated or StorageObserverEventType.Initialized))
        {
            
        }
    }

    public async Task RunAsync()
    {
        bool resturedFromSnapshot = await snapshots.RestoreSnapshotAsync();
        await storage.RunAsync();
    }

    public async Task<bool> TakeSnapshotAsync()
    {
        StorageIngestState state = tracker.CurrentState;
        return await snapshots.TakeSnapshotAsync(state);
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

public interface IIndexIngestProgressTracker : IObserver<IStorageChange>, IObserver<IInfoStreamEvent>
{
    IInfoStream InfoStream { get; }
    StorageIngestState CurrentState { get; }
}

public class IndexIngestProgressTracker : IIndexIngestProgressTracker
{
    //TODO: Along with the Todo later down, this should be changed so that we can compute the state quicker.
    //      It's fine that data-in is guarded by a ConcurrentDictionary, but for data out it shouldn't matter.
    private readonly ConcurrentDictionary<string, StorageAreaIngestStateTracker> trackers = new();
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();
    
    public IInfoStream InfoStream => infoStream;
    public StorageIngestState CurrentState => new StorageIngestState(trackers.Select(kv => kv.Value.State).ToArray());

    public void OnNext(IStorageChange value)
    {
        trackers.AddOrUpdate(value.Area, _ => throw new InvalidDataException(), (_, state) => state.UpdateState(value.Generation));
        PublishState();
    }

    public void OnNext(IInfoStreamEvent value)
    {
        if (value is not StorageObserverInfoStreamEvent soe) return;
        
        switch (soe.EventType)
        {
            case StorageObserverEventType.Starting:
                trackers.GetOrAdd(soe.Area, new StorageAreaIngestStateTracker(soe.Area, soe.EventType));
                break;
            case StorageObserverEventType.Initializing:
            case StorageObserverEventType.Updating:
            case StorageObserverEventType.Initialized:
            case StorageObserverEventType.Updated:
            case StorageObserverEventType.Stopped:
            default:
                trackers.AddOrUpdate(soe.Area, _ => new StorageAreaIngestStateTracker(soe.Area, soe.EventType), (_, state) => state.UpdateState(soe.EventType));
                break;
        }
        PublishState();
    }

    // TODO: We are adding a number of computational cycles here on each single update, this should be improved as well.
    //       So we don't have to do a loop on each turn, but later with that.
    private void PublishState()
        => infoStream.WriteStorageIngestStateEvent(CurrentState);

    void IObserver<IInfoStreamEvent>.OnError(Exception error) { }
    void IObserver<IInfoStreamEvent>.OnCompleted() { }
    void IObserver<IStorageChange>.OnError(Exception error) { }
    void IObserver<IStorageChange>.OnCompleted() { }

    public class StorageAreaIngestStateTracker
    {
        private readonly object padlock = new();
        private readonly Stopwatch timer = Stopwatch.StartNew();

        public StorageAreaIngestState State { get; private set; }

        public StorageAreaIngestStateTracker(string area, StorageObserverEventType state)
        {
            State = new StorageAreaIngestState(area, DateTime.Now, TimeSpan.Zero, 0, new GenerationInfo(-1,-1), state);
        }

        public StorageAreaIngestStateTracker UpdateState(StorageObserverEventType state)
        {
            if(state is StorageObserverEventType.Initialized or StorageObserverEventType.Updated or StorageObserverEventType.Stopped)
                timer.Stop();

            lock (padlock)
            {
                State = State with { LastEvent = state, Duration = timer.Elapsed};
            }
            return this;
        }

        public StorageAreaIngestStateTracker UpdateState(GenerationInfo generation)
        {
            lock (padlock)
            {
                State = State with { IngestedCount = State.IngestedCount+1, Generation = generation };
            }
            return this;
        }
    }
}