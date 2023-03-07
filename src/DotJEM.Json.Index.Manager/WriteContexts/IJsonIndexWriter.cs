using System;
using Newtonsoft.Json.Linq;

namespace DotJEM.Json.Index.Manager.WriteContexts;

public interface IIndexWriteContext : IDisposable
{
    void Write(JObject entity);
    void Create(JObject entity);
    void Delete(JObject entity);
    void Commit();
    void Flush(bool triggerMerge, bool flushDocStores, bool flushDeletes);
}
