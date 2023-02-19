using System;
using DotJEM.Diagnostics.Streams;
using Lucene.Net.Analysis;
using Lucene.Net.Util;

namespace DotJEM.Json.Index;

public interface IJsonIndex
{
    IInfoStream InfoStream { get; }


    IServiceCollection Services { get; }
    IJsonIndexStorage Storage { get; }
    IJsonIndexConfiguration Configuration { get; }
}

public interface IJsonIndexConfiguration
{
    LuceneVersion Version { get; }
    Analyzer Analyzer { get; }
}

public interface IJsonIndexStorage
{
}

public class LuceneJsonIndex : IJsonIndex
{
    public IInfoStream InfoStream { get; }
    public IServiceCollection Services { get; }
    public IJsonIndexStorage Storage { get; }
    public IJsonIndexConfiguration Configuration { get; }

    public LuceneJsonIndex() { }
    public LuceneJsonIndex(string path) {}

    public LuceneJsonIndex(IJsonIndexStorage storage, IJsonIndexConfiguration configuration, IServiceCollection services)
    {

    }



}

public interface IServiceCollection
{

}