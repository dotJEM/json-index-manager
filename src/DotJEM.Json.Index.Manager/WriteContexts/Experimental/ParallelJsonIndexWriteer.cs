using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotJEM.Json.Index.Configuration.IdentityStrategies;
using DotJEM.ObservableExt;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.WriteContexts.Experimental;

internal class ParallelJsonIndexWriteer : IJsonIndexWriteer
{
    private readonly IStorageIndex index;
    private readonly IDocumentFactory mapper;
    private readonly IIdentityResolver resolver;

    private readonly IndexWriter writer;
    private readonly IngestPipeline<IWriterCommand> pipeline = new IngestPipeline<IWriterCommand>();
    private readonly double originalBufferSize;

    public ParallelJsonIndexWriteer(IStorageIndex index)
    {
        this.index = index;
        writer = index.Storage.Writer;
        mapper = index.Services.DocumentFactory;
        resolver = index.Configuration.IdentityResolver;

        this.originalBufferSize = writer.GetRAMBufferSizeMB();
        writer.SetRAMBufferSizeMB(1000);

        pipeline.Observable
            .Buffer(TimeSpan.FromSeconds(2))
            .ForEachAsync(OnNext2);
    }

    private void OnNext2(IList<IWriterCommand> obj)
    {
        foreach (IWriterCommand command in obj)
        {
            command.Apply(writer);
        }
    }


    public interface IWriterCommand
    {
        void Apply(IndexWriter indexWriter);
    }
    public class WriteCommand : IWriterCommand
    {
        private readonly Term term;
        private readonly Document document;

        public WriteCommand(Term term, Document document)
        {
            this.term = term;
            this.document = document;
        }

        public void Apply(IndexWriter writer)
        {
            writer.UpdateDocument(term, document);
        }
    }

    public class WriteTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IIdentityResolver resolver;
        private readonly IDocumentFactory mapper;

        public WriteTask(JObject entity, IIdentityResolver resolver, IDocumentFactory mapper)
        {
            this.entity = entity;
            this.resolver = resolver;
            this.mapper = mapper;
        }

        public bool Completed { get; private set; }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Term term = resolver.CreateTerm(entity);
            Document doc = mapper.Create(entity);
            Value = new WriteCommand(term, doc);
            Completed = true;
            entity = null;
        }
    }

    public void Write(JObject entity)
    {
        pipeline.Enqueue(new WriteTask(entity, resolver, mapper));
    }

    public class CreateCommand : IWriterCommand
    {
        private readonly Document document;

        public CreateCommand(Document document)
        {
            this.document = document;
        }

        public void Apply(IndexWriter writer)
        {
            writer.AddDocument(document);
        }
    }

    public class CreateTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IDocumentFactory mapper;

        public CreateTask(JObject entity, IDocumentFactory mapper)
        {
            this.entity = entity;
            this.mapper = mapper;
        }

        public bool Completed { get; private set; }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Document doc = mapper.Create(entity);
            Value = new CreateCommand(doc);
            Completed = true;
            entity = null;
        }
    }

    public void Create(JObject entity)
    {
        pipeline.Enqueue(new CreateTask(entity, mapper));
    }

    public class DeleteCommand : IWriterCommand
    {
        private readonly Term term;

        public DeleteCommand(Term term)
        {
            this.term = term;
        }

        public void Apply(IndexWriter writer)
        {
            writer.DeleteDocuments(term);
        }
    }

    public class DeleteTask : IIngestTask<IWriterCommand>
    {
        private JObject entity;
        private readonly IIdentityResolver resolver;

        public DeleteTask(JObject entity, IIdentityResolver resolver)
        {
            this.entity = entity;
            this.resolver = resolver;
        }


        public bool Completed { get; private set; }
        public IWriterCommand Value { get; private set; }

        public void Execute()
        {
            Term term = resolver.CreateTerm(entity);
            Value = new DeleteCommand(term);
            Completed = true;
            entity = null;
        }
    }

    public void Delete(JObject entity)
    {
        pipeline.Enqueue(new DeleteTask(entity, resolver));
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
        pipeline?.Dispose();
        writer?.SetRAMBufferSizeMB(originalBufferSize);
    }
}

internal class IngestPipeline<TOutput> : IDisposable
{
    private readonly int capacity;
    private readonly ConcurrentQueue<IIngestTask<TOutput>> items = new();
    private readonly AutoResetEvent enqueueGate = new AutoResetEvent(false);
    private readonly AutoResetEvent completionGate = new AutoResetEvent(false);
    private bool disposed;

    public IForwarderObservable<TOutput> Observable { get; } = new ForwarderObservable<TOutput>();

    public IngestPipeline(int capacity = 256 * 1024)
    {
        this.capacity = capacity;
        //Thread transferThread = new(TransferCompleted);
        //transferThread.Start();
        Task.Run(TransferCompleted);
    }

    public void Enqueue(IIngestTask<TOutput> item)
    {
        if (items.Count >= capacity)
            enqueueGate.WaitOne();

        items.Enqueue(item);
        Task.Run(item.Execute);
    }

    private void MaybeSetEnqueueGate()
    {
        if (items.Count < capacity / 2)
            enqueueGate.Set();
    }

    private async Task TransferCompleted()
    {
        while (!disposed)
        {
            if (!items.TryDequeue(out IIngestTask<TOutput> task))
            {
                MaybeSetEnqueueGate();
                await Task.Delay(2000);
                continue;
            }

            while (!task.Completed)
            {
                MaybeSetEnqueueGate();
                await Task.Delay(2000);
            }

            Observable.Publish(task.Value);
        }
    }

    public void Dispose()
    {
        enqueueGate?.Dispose();
        completionGate?.Dispose();
        disposed = true;

    }

}

internal interface IIngestTask<out TOutput>
{
    bool Completed { get; }

    TOutput Value { get; }
    public void Execute();
}
