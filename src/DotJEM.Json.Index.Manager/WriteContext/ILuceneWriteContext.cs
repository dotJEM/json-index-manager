using System;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.WriteContext;

public interface ILuceneWriteContext : IDisposable
{
    void Write(JObject entity);
    void Create(JObject entity);
    void Delete(JObject entity);
    void Commit();
    void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes);
}

public class SequentialLuceneWriteContext : ILuceneWriteContext
{
    private readonly IStorageIndex index;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;

    private readonly IndexWriter writer;
    private readonly double originalBufferSize;

    public SequentialLuceneWriteContext(IStorageIndex index)
    {
        this.index = index;
        this.writer = index.Storage.Writer;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;

        originalBufferSize = writer.GetRAMBufferSizeMB();
        writer.SetRAMBufferSizeMB(1000);
    }

    public void Write(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Document doc = mapper.Create(entity);
        writer.UpdateDocument(term, doc);
    }


    public void Create(JObject entity)
    {
        Document doc = mapper.Create(entity);
        writer.AddDocument(doc);
    }

    public void Delete(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        writer.DeleteDocuments(term);
    }

    public void Commit()
    {
        writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes)
    {
        writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    }

    public void Dispose()
    {
        writer?.Dispose();
        writer?.SetRAMBufferSizeMB(originalBufferSize);
    }
}