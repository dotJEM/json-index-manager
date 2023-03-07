using DotJEM.Json.Index.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Json.Index;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;
using DotJEM.Json.Index.Manager.WriteContexts;
using DotJEM.Json.Index.Storage.Snapshot;

namespace Debugging.Adapter;


internal class Lucene3 : IJsonIndexAdapter
{
    public Lucene3(LuceneStorageIndex index)
    {
        throw new NotImplementedException();
    }
}


public class SequentialLuceneWriteContext : IIndexWriteContext
{
    private readonly double ramBufferSize;
    private readonly IStorageIndex index;
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

public class LuceneFileAdapter : ILuceneFile
{
    public string Name { get; }

    public Stream Open()
    {
        throw new NotImplementedException();
    }

}