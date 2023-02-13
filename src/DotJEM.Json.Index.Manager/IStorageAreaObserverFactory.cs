using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Index.Manager.Configuration;
using DotJEM.Json.Storage;
using DotJEM.Json.Storage.Configuration;
using DotJEM.TaskScheduler;

namespace DotJEM.Json.Index.Manager;

public interface IStorageAreaObserverFactory
{
    IEnumerable<IStorageAreaObserver> CreateAll();
}

public class StorageAreaObserverFactory : IStorageAreaObserverFactory
{
    private readonly IStorageContext context;
    private readonly IWebBackgroundTaskScheduler scheduler;
    private readonly IStorageWatchConfiguration configuration;

    public StorageAreaObserverFactory(IStorageContext context, IWebBackgroundTaskScheduler scheduler, IStorageWatchConfiguration configuration)
    {
        this.context = context;
        this.scheduler = scheduler;
        this.configuration = configuration;
    }

    public IEnumerable<IStorageAreaObserver> CreateAll()
        => context.AreaInfos.Select(areaInfo => Create(areaInfo.Name));

    private IStorageAreaObserver Create(string area) 
        => new StorageAreaObserver(context.Area(area), scheduler, configuration.GetConfiguration(area));
}
