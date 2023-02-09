using System;

namespace DotJEM.Diagnostics.Streams;

public interface IInfoStreamEvent
{
    Type Source { get; }
    InfoLevel Level { get; }
    string Message { get; }
}
