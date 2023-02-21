using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.ObservableExt;

namespace DotJEM.Json.Index.Manager.Tracking;

// ReSharper disable once PossibleInterfaceMemberAmbiguity -> Just dictates implementation must be explicit which is OK.
public interface IIndexIngestProgressTracker : IObserver<IStorageChange>, IObserver<IInfoStreamEvent>, IObservable<StorageIngestState>
{
    IInfoStream InfoStream { get; }
    StorageIngestState CurrentState { get; }
}

public class IndexIngestProgressTracker : ForwarderObservable<StorageIngestState>, IIndexIngestProgressTracker
{
    //TODO: Along with the Todo later down, this should be changed so that we can compute the state quicker.
    //      It's fine that data-in is guarded by a ConcurrentDictionary, but for data out it shouldn't matter.
    private readonly ConcurrentDictionary<string, StorageAreaIngestStateTracker> trackers = new();
    private readonly IInfoStream<IndexManager> infoStream = new InfoStream<IndexManager>();

    public IInfoStream InfoStream => infoStream;

    // TODO: We are adding a number of computational cycles here on each single update, this should be improved as well.
    //       So we don't have to do a loop on each turn, but later with that.
    public StorageIngestState CurrentState => new (trackers.Select(kv => kv.Value.State).ToArray());



    public void OnNext(IStorageChange value)
    {
        trackers.AddOrUpdate(value.Area, _ => throw new InvalidDataException(), (_, state) => state.UpdateState(value.Generation));
        Publish(CurrentState);
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
        // TODO: We are adding a number of computational cycles here on each single update, this should be improved as well.
        //       So we don't have to do a loop on each turn, but later with that.
        Publish(CurrentState);
    }

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