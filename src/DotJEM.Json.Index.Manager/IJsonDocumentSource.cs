using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.ObservableExtensions;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;


public interface IJsonDocumentSource
{
    IInfoStream InfoStream { get; }
    IObservable<IJsonDocumentChange> Observable { get; }
    Task RunAsync();
    void UpdateGeneration(string area, long generation);
}

public interface IJsonDocumentChange
{
    string Area { get; }
    GenerationInfo Generation { get; }
    JsonChangeType Type { get; }
    JObject Entity { get; }
}

public class ChangeStream : BasicSubject<IJsonDocumentChange> { }

public struct JsonDocumentChange : IJsonDocumentChange
{
    public GenerationInfo Generation { get; }
    public JsonChangeType Type { get; }
    public string Area { get; }
    public JObject Entity { get; }

    public JsonDocumentChange(string area, JsonChangeType type, JObject entity, GenerationInfo generationInfo)
    {
        Generation = generationInfo;
        Type = type;
        Area = area;
        Entity = entity;
    }
}

public enum JsonChangeType
{
    Create, Update, Delete
}

public class JsonDocumentSource : IJsonDocumentSource
{
    private readonly Dictionary<string, IJsonStorageAreaObserver> observers;
    private readonly ChangeStream observable = new ();
    private readonly InfoStream<JsonDocumentSource> infoStream = new ();

    public IObservable<IJsonDocumentChange> Observable => observable;
    public IInfoStream InfoStream => infoStream;

    public JsonDocumentSource(IStorageContext context, IWebTaskScheduler scheduler)
        : this(new JsonStorageAreaObserverFactory(context, scheduler))
    {
    }

    public JsonDocumentSource(IJsonStorageAreaObserverFactory factory)
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