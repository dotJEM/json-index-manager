using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Snapshots;
using DotJEM.Json.Index.Manager.WriteContexts;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;

namespace DotJEM.Json.Index.Manager;

public interface IWriteContextFactory
{
    WriteContexts.ILuceneWriteContext Create();
}

public class WriteContextFactory : IWriteContextFactory
{
    private readonly IStorageIndex index;
    public WriteContextFactory(IStorageIndex index)
    {
        this.index = index;
    }
    public WriteContexts.ILuceneWriteContext Create()
        => new SequentialLuceneWriteContext(index);
}

public interface IIndexManager
{
    IInfoStream InfoStream { get; }
}

public class IndexManager : IIndexManager
{
    private readonly IIndexSnapshotManager snapshots;
    private readonly IIndexIngestProgressTracker tracker;

    private readonly WriteContexts.ILuceneWriteContext context;
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;

    public IndexManager(IStorageManager storage, IIndexSnapshotManager snapshots, IWriteContextFactory writeContextFactory)
    {
        this.snapshots = snapshots;
        context = writeContextFactory.Create();
        storage.Observable.ForEachAsync(CaptureChange);

        storage.InfoStream.Forward(infoStream);
        snapshots.InfoStream.Forward(infoStream);

        tracker = new IndexIngestProgressTracker();
        storage.InfoStream.Subscribe(tracker);
        storage.Observable.Subscribe(tracker);
        snapshots.InfoStream.Subscribe(tracker);

        tracker.InfoStream.Forward(infoStream);
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
}

public class IndexIngestProgressTracker : IIndexIngestProgressTracker
{
    private readonly ConcurrentDictionary<string, IngestStateInfo> trackers = new();
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;


    public void OnNext(IStorageChange value)
    {
        trackers.AddOrUpdate(value.Area,
            _ => throw new InvalidDataException(),
            (_, state) => state.UpdateState(value.Generation));

        infoStream.WriteStorageIngestStateEvent(CreateState());
    }

    public void OnNext(IInfoStreamEvent value)
    {
        if (value is not StorageObserverInfoStreamEvent soe) return;
        
        switch (soe.EventType)
        {
            case StorageObserverEventType.Starting:
                trackers.GetOrAdd(soe.Area, new IngestStateInfo(soe.EventType));
                break;
            case StorageObserverEventType.Initializing:
            case StorageObserverEventType.Initialized:
            case StorageObserverEventType.Updating:
            case StorageObserverEventType.Updated:
                trackers.AddOrUpdate(soe.Area, 
                    _ => new IngestStateInfo(soe.EventType),
                    (_, state) => state.UpdateState(soe.EventType)
                );
                break;
            case StorageObserverEventType.Stopped:
                trackers.AddOrUpdate(soe.Area, 
                    _ => new IngestStateInfo(soe.EventType),
                    (_, state) => state.UpdateState(soe.EventType)
                );
                break;
        }

        infoStream.WriteStorageIngestStateEvent(CreateState());
    }

    private StorageIngestState CreateState()
    {
        return new StorageIngestState(
            trackers.Select(kv => new AreaIngestState(kv.Key, kv.Value.StartTime, kv.Value.Timer.Elapsed, kv.Value.Counter, kv.Value.GenerationInfo, kv.Value.State)).ToArray()
        );
    }

    void IObserver<IInfoStreamEvent>.OnError(Exception error)
    {
    }

    void IObserver<IInfoStreamEvent>.OnCompleted()
    {
    }

    void IObserver<IStorageChange>.OnError(Exception error)
    {
    }

    void IObserver<IStorageChange>.OnCompleted()
    {
    }

    public class IngestStateInfo
    {
        public DateTime StartTime { get; } = DateTime.Now;
        public Stopwatch Timer { get; } = Stopwatch.StartNew();

        public long Counter { get; private set; }
        public GenerationInfo GenerationInfo { get;  private set;}
        public StorageObserverEventType State { get; private set; }

        public IngestStateInfo(StorageObserverEventType state)
        {
            State = state;
        }

        public IngestStateInfo UpdateState(StorageObserverEventType state)
        {
            if(state is StorageObserverEventType.Initialized or StorageObserverEventType.Updated or StorageObserverEventType.Stopped)
                Timer.Stop();

            State = state;
            return this;
        }

        public IngestStateInfo UpdateState(GenerationInfo valueGeneration)
        {
            Counter++;
            GenerationInfo = valueGeneration;
            return this;
        }
    }
}