namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultJsonIndexManagerConfiguration : IJsonIndexManagerConfiguration
{
    public IStorageWatchConfiguration StorageConfiguration { get; } = new DefaultStorageWatchConfiguration();
    public IWriteContextConfiguration WriterConfiguration { get; } = new DefaultWriteContextConfiguration();
}