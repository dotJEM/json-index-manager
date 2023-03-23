using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage;
using DotJEM.Web.Scheduler;

namespace DotJEM.Json.Index.Manager;

public interface IJsonStorageAreaObserverFactory
{
    IEnumerable<IJsonStorageAreaObserver> CreateAll();
}

public class JsonStorageAreaObserverFactory : IJsonStorageAreaObserverFactory
{
    private readonly IStorageContext context;
    private readonly IWebTaskScheduler scheduler;

    public JsonStorageAreaObserverFactory(IStorageContext context, IWebTaskScheduler scheduler)
    {
        this.context = context;
        this.scheduler = scheduler;
    }

    public IEnumerable<IJsonStorageAreaObserver> CreateAll()
        => context.AreaInfos.Select(areaInfo => new JsonStorageAreaObserver(context.Area(areaInfo.Name), scheduler));
}
