using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage;
using DotJEM.TaskScheduler;

namespace DotJEM.Json.Index.Manager;

public interface IStorageAreaObserverFactory
{
    IStorageAreaObserver Create(string area);
    IEnumerable<IStorageAreaObserver> CreateAll();
}
public class StorageAreaObserverFactory : IStorageAreaObserverFactory
{
    private readonly IStorageContext context;
    private readonly ITaskScheduler scheduler;

    public StorageAreaObserverFactory(IStorageContext context, ITaskScheduler scheduler)
    {
        this.context = context;
        this.scheduler = scheduler;
    }

    public IStorageAreaObserver Create(string area) => new StorageAreaObserver(context.Area(area),scheduler);
    public IEnumerable<IStorageAreaObserver> CreateAll() => context.AreaInfos.Select(areaInfo => Create(areaInfo.Name));
}
