namespace DotJEM.Json.Index.Manager.Configuration;

public interface IStorageWatchConfiguration
{
    IStorageAreaWatchConfiguration GetConfiguration(string areaName);
}