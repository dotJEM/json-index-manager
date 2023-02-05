using System;

namespace DotJEM.Json.Index.Manager.Diagnostics;

public interface IInfoStreamExceptionEvent : IInfoStreamEvent
{
    Exception Exception { get; }
}