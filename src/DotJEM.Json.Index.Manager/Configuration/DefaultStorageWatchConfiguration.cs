namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultStorageWatchConfiguration : IStorageWatchConfiguration
{
    public IStorageAreaWatchConfiguration GetConfiguration(string areaName)
    {
        return new DefaultStorageAreaWatchConfiguration();
    }

    public class DefaultStorageAreaWatchConfiguration : IStorageAreaWatchConfiguration
    {
        public string Interval => "10sec";
    }
}