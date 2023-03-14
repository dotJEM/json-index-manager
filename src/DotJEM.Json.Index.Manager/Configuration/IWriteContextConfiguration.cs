namespace DotJEM.Json.Index.Manager.Configuration;

public interface IWriteContextConfiguration
{
    int BatchSize { get; }
    double RamBufferSize { get; }
}

public interface ISnapshotConfiguration
{
    string Schedule { get; }
}