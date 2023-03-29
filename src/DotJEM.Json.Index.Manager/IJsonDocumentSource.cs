using System;
using System.Threading.Tasks;
using DotJEM.ObservableExtensions;
using DotJEM.ObservableExtensions.InfoStreams;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager;


public interface IJsonDocumentSource
{
    IInfoStream InfoStream { get; }
    IObservable<IJsonDocumentChange> Observable { get; }
    Task RunAsync();
    void UpdateGeneration(string area, long generation);
}

public interface IJsonDocumentChange
{
    string Area { get; }
    GenerationInfo Generation { get; }
    JsonChangeType Type { get; }
    JObject Entity { get; }
}

public class ChangeStream : BasicSubject<IJsonDocumentChange> { }

public struct JsonDocumentChange : IJsonDocumentChange
{
    public GenerationInfo Generation { get; }
    public JsonChangeType Type { get; }
    public string Area { get; }
    public JObject Entity { get; }

    public JsonDocumentChange(string area, JsonChangeType type, JObject entity, GenerationInfo generationInfo)
    {
        Generation = generationInfo;
        Type = type;
        Area = area;
        Entity = entity;
    }
}

public struct GenerationInfo
{
    public long Current { get; }
    public long Latest { get; }

    public GenerationInfo(long current, long latest)
    {
        Current = current;
        Latest = latest;
    }

    public static GenerationInfo operator +(GenerationInfo left, GenerationInfo right)
    {
        return new GenerationInfo(left.Current + right.Current, left.Latest + right.Latest);
    }
}


public enum JsonChangeType
{
    Create, Update, Delete
}