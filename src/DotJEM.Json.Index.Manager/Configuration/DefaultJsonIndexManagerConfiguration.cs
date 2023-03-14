namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultJsonIndexManagerConfiguration : IJsonIndexManagerConfiguration
{
    public IStorageWatchConfiguration StorageConfiguration { get; } = new DefaultStorageWatchConfiguration();
    public IWriteContextConfiguration WriterConfiguration { get; } = new DefaultWriteContextConfiguration();
    public ISnapshotConfiguration SnapshotConfiguration { get; } = new DefaultSnapshotConfiguration();
}

public class DefaultSnapshotConfiguration : ISnapshotConfiguration
{
    public string Schedule { get; }
}