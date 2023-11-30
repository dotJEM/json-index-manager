using System.Collections.Generic;
using System.Linq;
using DotJEM.Json.Storage;
using DotJEM.Web.Scheduler;

namespace Stress.Adapter;

public interface IJsonStorageAreaObserverFactory
{
    IEnumerable<IJsonStorageAreaObserver> CreateAll();
}

public class JsonStorageAreaObserverFactory : IJsonStorageAreaObserverFactory
{
    private readonly IStorageContext context;
    private readonly IWebTaskScheduler scheduler;
    private readonly string[] areas;

    public JsonStorageAreaObserverFactory(IStorageContext context, IWebTaskScheduler scheduler, params string[] areas)
    {
        this.context = context;
        this.scheduler = scheduler;
        this.areas = areas;
    }

    public IEnumerable<IJsonStorageAreaObserver> CreateAll()
        => areas.Length == 0
            ? context.AreaInfos.Select(areaInfo => new JsonStorageAreaObserver(context.Area(areaInfo.Name), scheduler))
            : areas.Select(area => new JsonStorageAreaObserver(context.Area(area), scheduler));
}
