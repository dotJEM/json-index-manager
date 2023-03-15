using System;
using System.Diagnostics;
using System.Threading;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using DotJEM.TaskScheduler;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.Writer;

public interface IJsonIndexWriter 
{
    void Write(JObject entity);
    void Create(JObject entity);
    void Delete(JObject entity);
    void Commit();
    void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes);
}

public class JsonIndexWriter : IJsonIndexWriter
{
    private readonly double ramBufferSize;

    private readonly IStorageIndex index;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;
    private readonly IndexCommitter committer;

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

    public JsonIndexWriter(IStorageIndex index, IWebTaskScheduler scheduler, string commitInterval = "10s", int batchSize = 20000, double ramBufferSize = 1024)
    {
        this.index = index;
        this.ramBufferSize = ramBufferSize;
        this.mapper = index.Services.DocumentFactory;
        this.resolver = index.Configuration.IdentityResolver;
        this.committer = new IndexCommitter(this, AdvParsers.AdvParser.ParseTimeSpan(commitInterval), batchSize);
        scheduler.Schedule(nameof(IndexCommitter), _ => committer.Increment(), commitInterval);
    }

    public void Write(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Document doc = mapper.Create(entity);
        Writer.UpdateDocument(term, doc);
        committer.Increment();
    }

    public void Create(JObject entity)
    {
        Document doc = mapper.Create(entity);
        Writer.AddDocument(doc);
        committer.Increment();
    }

    public void Delete(JObject entity)
    {
        Term term = resolver.CreateTerm(entity);
        Writer.DeleteDocuments(term);
        committer.Increment();
    }


    public void Commit()
    {
        Writer.Commit();
    }

    public void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes) => Writer.Flush(triggerMerge, flushDocStores, flushDeletes);
    public void Optimize() => Writer.Optimize();
    public void Optimize(int maxNumSegments) => Writer.Optimize(maxNumSegments);
    public void ExpungeDeletes() => Writer.ExpungeDeletes();
    public void MaybeMerge() => Writer.MaybeMerge();

    private class IndexCommitter
    {
        private readonly int batchSize;
        private readonly TimeSpan commitInterval;
        private readonly IJsonIndexWriter owner;

        private long writes = 0;
        private Stopwatch time = Stopwatch.StartNew();

        public IndexCommitter(IJsonIndexWriter owner, TimeSpan commitInterval, int batchSize)
        {
            this.commitInterval = commitInterval;
            this.batchSize = batchSize;
        }

        public bool Increment()
        {
            long value  = Interlocked.Increment(ref writes);
            return (value % batchSize == 0 || time.Elapsed > commitInterval) && Commit();
        }

        private bool Commit()
        {
            owner.Commit();
            time.Restart();
            return true;
        }
    }
}

