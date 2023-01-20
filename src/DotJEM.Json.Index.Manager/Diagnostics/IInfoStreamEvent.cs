using System;

namespace DotJEM.Json.Index.Manager.Diagnostics;

public interface IInfoStreamEvent
{
    Type Source { get; }
    string Level { get; }
    string Message { get; }
}
