using System;

namespace DotJEM.Diagnostics.Streams;

public interface IInfoStreamEvent
{
    Type Source { get; }
    string Level { get; }
    string Message { get; }
}
