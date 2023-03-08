namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultWriteContextConfiguration : IWriteContextConfiguration
{
    public int BatchSize { get; } = 10000;
    public double RamBufferSize { get; }
    
    public DefaultWriteContextConfiguration(double ramBufferSize = 1024)
    {
        RamBufferSize = ramBufferSize;
    }
}