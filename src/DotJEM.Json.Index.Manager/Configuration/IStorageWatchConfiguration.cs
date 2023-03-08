using System.Collections.Generic;

namespace DotJEM.Json.Index.Manager.Configuration;

public interface IStorageWatchConfiguration
{

    IEnumerable<IStorageAreaWatchConfiguration> GetConfigurations(IEnumerable<string> names);
}