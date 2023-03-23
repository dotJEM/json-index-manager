using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DotJEM.Json.Storage;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;

namespace DotJEM.Json.Index.Manager;


public interface IJsonStorageManager
{
    IInfoStream InfoStream { get; }
    IObservable<IStorageChange> Observable { get; }
    Task RunAsync();
    void UpdateGeneration(string area, long generation);
}

public class JsonStorageManager : IJsonStorageManager
{
    private readonly Dictionary<string, IJsonStorageAreaObserver> observers;
    private readonly Subject<IStorageChange> observable = new ();
    private readonly InfoStream<JsonStorageManager> infoStream = new ();

    public IObservable<IStorageChange> Observable => observable;
    public IInfoStream InfoStream => infoStream;

    public JsonStorageManager(IStorageContext context, IWebTaskScheduler scheduler)
        : this(new JsonStorageAreaObserverFactory(context, scheduler))
    {
    }

    public JsonStorageManager(IJsonStorageAreaObserverFactory factory)
    {
        this.observers = factory.CreateAll()
            .Select(observer => {
                observer.Observable.Subscribe(observable);
                observer.InfoStream.Subscribe(infoStream);
                return observer;
            }).ToDictionary(x => x.AreaName);
    }

    public async Task RunAsync()
    {
        await Task.WhenAll(
            observers.Values.Select(async observer => await observer.RunAsync().ConfigureAwait(false))
        ).ConfigureAwait(false);
    }

    public void UpdateGeneration(string area, long generation)
    {
        if (!observers.TryGetValue(area, out IJsonStorageAreaObserver observer))
            return; // TODO?

        observer.UpdateGeneration(generation);
    }
}
