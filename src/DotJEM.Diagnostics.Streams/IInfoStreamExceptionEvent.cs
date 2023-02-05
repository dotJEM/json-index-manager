using System;

namespace DotJEM.Diagnostics.Streams;

public interface IInfoStreamExceptionEvent : IInfoStreamEvent
{
    Exception Exception { get; }
}