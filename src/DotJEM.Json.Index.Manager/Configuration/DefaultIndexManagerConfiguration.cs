namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultIndexManagerConfiguration : IIndexManagerConfiguration
{
    public IStorageWatchConfiguration StorageConfiguration { get; } = new DefaultStorageWatchConfiguration();
    public IWriteContextConfiguration WriterConfiguration { get; } = new DefaultWriteContextConfiguration();
}