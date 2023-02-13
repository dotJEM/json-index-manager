namespace DotJEM.Json.Index.Manager.Configuration;

public class DefaultWriteContextConfiguration : IWriteContextConfiguration
{
    public double RamBufferSize { get; }
    
    public DefaultWriteContextConfiguration(double ramBufferSize = 1024)
    {
        RamBufferSize = ramBufferSize;
    }
}