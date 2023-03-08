using System.Collections.Generic;
using System.Linq;

namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultStorageWatchConfiguration : IStorageWatchConfiguration
{
    public IEnumerable<IStorageAreaWatchConfiguration> GetConfigurations(IEnumerable<string> names)
        => names.Select(n => new DefaultStorageAreaWatchConfiguration(n));


    public class DefaultStorageAreaWatchConfiguration : IStorageAreaWatchConfiguration
    {
        public string Name { get; }
        public string Interval => "10sec";

        public DefaultStorageAreaWatchConfiguration(string name)
        {
            Name = name;
        }

    }
}