namespace DotJEM.Json.Index.Manager.Configuration;

public interface IJsonIndexManagerConfiguration
{
    IStorageWatchConfiguration StorageConfiguration { get; }
    IWriteContextConfiguration WriterConfiguration { get; }
}