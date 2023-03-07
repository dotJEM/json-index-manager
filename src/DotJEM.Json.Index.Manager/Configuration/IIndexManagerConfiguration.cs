namespace DotJEM.Json.Index.Manager.Configuration;

public interface IIndexManagerConfiguration
{
    IStorageWatchConfiguration StorageConfiguration { get; }
    IWriteContextConfiguration WriterConfiguration { get; }
    ISnapshotConfiguration SnapshotConfiguration { get; }
}

public interface ISnapshotConfiguration
{
}

