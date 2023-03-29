using DotJEM.Json.Storage;
using DotJEM.ObservableExtensions.InfoStreams;
using DotJEM.Web.Scheduler;

namespace DotJEM.Json.Index.Manager.V1Adapter;

public class JsonStorageDocumentSource : IJsonDocumentSource
{
    private readonly Dictionary<string, IJsonStorageAreaObserver> observers;
    private readonly ChangeStream observable = new();
    private readonly InfoStream<JsonStorageDocumentSource> infoStream = new();

    public IObservable<IJsonDocumentChange> Observable => observable;
    public IInfoStream InfoStream => infoStream;

    public JsonStorageDocumentSource(IStorageContext context, IWebTaskScheduler scheduler)
        : this(new JsonStorageAreaObserverFactory(context, scheduler))
    {
    }

    public JsonStorageDocumentSource(IJsonStorageAreaObserverFactory factory)
    {
        observers = factory.CreateAll()
            .Select(observer =>
            {
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