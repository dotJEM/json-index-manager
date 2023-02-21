using System;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.WriteContexts;

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
    private readonly double ramBufferSize;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;
    private IndexWriter writer;

    private IndexWriter Writer
    {
        get
        {
            if (writer == index.Storage.Writer) return writer;
            writer = index.Storage.Writer;
            writer.SetRAMBufferSizeMB(ramBufferSize);
            return writer;
        }
    }

    private readonly double originalBufferSize;

    public SequentialLuceneWriteContext(IStorageIndex index, double ramBufferSize)
    {
        this.index = index;
        this.ramBufferSize = ramBufferSize;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;

        originalBufferSize = Writer.GetRAMBufferSizeMB();
        Writer.SetRAMBufferSizeMB(ramBufferSize);
    }

    private long counter = 0;

    public void Write(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Document doc = mapper.Create(entity);
        Writer.UpdateDocument(term, doc);
        counter++;
        if(counter % 100000 == 0) Writer.Commit();
    }


    public void Create(JObject entity)
    {
        Document doc = mapper.Create(entity);
        Writer.AddDocument(doc);
        counter++;
        if(counter % 100000 == 0) Writer.Commit();


    }

    public void Delete(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Writer.DeleteDocuments(term);
    }

    public void Commit()
    {
        Writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes)
    {
        Writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    }

    public void Optimize()
    {
        Writer.Optimize();
    }

    public void Optimize(int maxNumSegments)
    {
        Writer.Optimize(maxNumSegments);
    }

    public void ExpungeDeletes()
    {
        Writer.ExpungeDeletes();
    }

    public void MaybeMerge()
    {
        Writer.MaybeMerge();
    }

    public void Dispose()
    {
        Writer?.Dispose();
        Writer?.SetRAMBufferSizeMB(originalBufferSize);
    }
}