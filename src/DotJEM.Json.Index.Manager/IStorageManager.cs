using System.Linq;
using System.Threading.Tasks;
using DotJEM.Diagnostics.Streams;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Adapter.Materialize.ChanceLog.ChangeObjects;
using DotJEM.ObservableExt;
using DotJEM.TaskScheduler;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;


public interface IStorageManager
{
    IInfoStream InfoStream { get; }
    IForwarderObservable<IStorageChange> Observable { get; }
    Task RunAsync();
}

public class StorageManager : IStorageManager
{
    private readonly IStorageAreaObserverFactory factory;

    private readonly ForwarderObservable<IStorageChange> observable = new ();
    private readonly InfoStream<StorageManager> infoStream = new ();

    public IForwarderObservable<IStorageChange> Observable => observable;
    public IInfoStream InfoStream => infoStream;

    public StorageManager(IStorageContext context, IWebBackgroundTaskScheduler scheduler, IStorageWatchConfiguration configuration)
        : this(new StorageAreaObserverFactory(context, scheduler, configuration))
    {
    }

    public StorageManager(IStorageAreaObserverFactory factory)
    {
        this.factory = factory;
    }

    public async Task RunAsync()
    {
        await Task.WhenAll(
            factory.CreateAll().Select(async observer => {
                observer.Observable.Forward(observable);
                observer.InfoStream.Forward(infoStream);
                await observer.RunAsync().ConfigureAwait(false);
            })
        ).ConfigureAwait(false);
    }
}
