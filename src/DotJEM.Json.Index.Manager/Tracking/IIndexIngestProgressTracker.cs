using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Snapshots.Zip;
using DotJEM.Json.Index.Storage.Snapshot;
using DotJEM.ObservableExt;

namespace DotJEM.Json.Index.Manager.Tracking;

// ReSharper disable once PossibleInterfaceMemberAmbiguity -> Just dictates implementation must be explicit which is OK.
public interface IIndexIngestProgressTracker : IObserver<IStorageChange>, IObserver<IInfoStreamEvent>, IObservable<ITrackerState>
{
    IInfoStream InfoStream { get; }
    StorageIngestState IngestState { get; }
    SnapshotRestoreState RestoreState { get; }
    void UpdateState(StorageAreaIngestState state);
}
public interface ITrackerState {}


public class IndexIngestProgressTracker : ForwarderObservable<ITrackerState>, IIndexIngestProgressTracker
{
    //TODO: Along with the Todo later down, this should be changed so that we can compute the state quicker.
    //      It's fine that data-in is guarded by a ConcurrentDictionary, but for data out it shouldn't matter.
    private readonly ConcurrentDictionary<string, StorageAreaIngestStateTracker> observerTrackers = new();
    private readonly ConcurrentDictionary<string, IndexFileRestoreStateTracker> restoreTrackers = new();
    private readonly IInfoStream<JsonIndexManager> infoStream = new InfoStream<JsonIndexManager>();

    public IInfoStream InfoStream => infoStream;

    // TODO: We are adding a number of computational cycles here on each single update, this should be improved as well.
    //       So we don't have to do a loop on each turn, but later with that.
    public StorageIngestState IngestState => new (observerTrackers.Select(kv => kv.Value.State).ToArray());
    public SnapshotRestoreState RestoreState => new (restoreTrackers.Select(kv => kv.Value.State).ToArray());

    public void OnNext(IStorageChange value)
    {
        observerTrackers.AddOrUpdate(value.Area, _ => throw new InvalidDataException(), (_, state) => state.UpdateState(value.Generation));
        Publish(IngestState);
    }

    public void UpdateState(StorageAreaIngestState state)
    {
        observerTrackers.AddOrUpdate(state.Area, s => new StorageAreaIngestStateTracker(s, StorageObserverEventType.Initialized).UpdateState(state)
            , (s, tracker) => tracker.UpdateState(state));
    }

    public void OnNext(IInfoStreamEvent value)
    {
        switch (value)
        {
            case StorageObserverInfoStreamEvent evt:
                OnStorageObserverInfoStreamEvent(evt);
                break;
                
            case ZipSnapshotEvent evt:
                OnZipSnapshotEvent(evt);
                break;
                
            case ZipFileEvent evt:
                OnZipFileEvent(evt);
                break;
        }
    }

    private void OnZipFileEvent(ZipFileEvent sne)
    {
        switch (sne.EventType)
        {
            case FileEventType.OPEN:
                restoreTrackers.AddOrUpdate(
                    sne.File.Name,
                    name => new IndexFileRestoreStateTracker(name),
                    (name, tracker) => tracker.Restoring()
                );
                break;
            case FileEventType.CLOSE:
                restoreTrackers.AddOrUpdate(
                    sne.File.Name,
                    name => new IndexFileRestoreStateTracker(name),
                    (name, tracker) => tracker.Complete()
                );
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        Publish(RestoreState);
    }

    private void OnZipSnapshotEvent(ZipSnapshotEvent sne)
    {
        switch (sne.EventType)
        {
            case FileEventType.OPEN:
                restoreTrackers.TryAdd(sne.Snapshot.SegmentsFile.Name, new IndexFileRestoreStateTracker(sne.Snapshot.SegmentsFile.Name));
                restoreTrackers.TryAdd(sne.Snapshot.SegmentsGenFile.Name, new IndexFileRestoreStateTracker(sne.Snapshot.SegmentsGenFile.Name));
                foreach (ILuceneFile file in sne.Snapshot.Files)
                    restoreTrackers.TryAdd(file.Name, new IndexFileRestoreStateTracker(file.Name));
                break;
            case FileEventType.CLOSE:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        Publish(RestoreState);
    }

    private void OnStorageObserverInfoStreamEvent(StorageObserverInfoStreamEvent soe)
    {
        switch (soe.EventType)
        {
            case StorageObserverEventType.Starting:
                observerTrackers.GetOrAdd(soe.Area, new StorageAreaIngestStateTracker(soe.Area, soe.EventType));
                break;
            case StorageObserverEventType.Initializing:
            case StorageObserverEventType.Updating:
            case StorageObserverEventType.Initialized:
            case StorageObserverEventType.Updated:
            case StorageObserverEventType.Stopped:
            default:
                observerTrackers.AddOrUpdate(soe.Area, _ => new StorageAreaIngestStateTracker(soe.Area, soe.EventType), (_, state) => state.UpdateState(soe.EventType));
                break;
        }

        // TODO: We are adding a number of computational cycles here on each single update, this should be improved as well.
        //       So we don't have to do a loop on each turn, but later with that.
        Publish(IngestState);
    }

    void IObserver<IInfoStreamEvent>.OnError(Exception error) { }
    void IObserver<IInfoStreamEvent>.OnCompleted() { }
    void IObserver<IStorageChange>.OnError(Exception error) { }
    void IObserver<IStorageChange>.OnCompleted() { }

    private class IndexFileRestoreStateTracker
    {
        public SnapshotFileRestoreState State { get; private set; }

        public IndexFileRestoreStateTracker(string name)
        {
            State = new SnapshotFileRestoreState(name, "PENDING", DateTime.Now, DateTime.Now);
        }

        public IndexFileRestoreStateTracker Restoring()
        {
            State = State with{ State = "RESTORING", StartTime = DateTime.Now };
            return this;
        }

        public IndexFileRestoreStateTracker Complete()
        {
            State = State with{ State = "COMPLETE", StopTime = DateTime.Now };
            return this;
        }
    }

    private class StorageAreaIngestStateTracker
    {
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

            State = State with { LastEvent = state, Duration = timer.Elapsed};
            return this;
        }

        public StorageAreaIngestStateTracker UpdateState(GenerationInfo generation)
        {
            State = State with { IngestedCount = State.IngestedCount+1, Generation = generation };
            return this;
        }

        public StorageAreaIngestStateTracker UpdateState(StorageAreaIngestState areaState)
        {
            this.State = areaState;
            return this;
        }
    }
}