using DotJEM.Json.Index.Configuration;
using DotJEM.Json.Index;
using DotJEM.Json.Storage.Configuration;
using DotJEM.Json.Storage;
using Lucene.Net.Documents;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO.Compression;
using Legacy;
using Lucene.Net.Analysis.Standard;
using Version = Lucene.Net.Util.Version;

var storage = new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True");
storage.Configure.MapField(JsonField.Id, "id");
storage.Configure.MapField(JsonField.ContentType, "contentType");
storage.Configure.MapField(JsonField.Version, "$version");
storage.Configure.MapField(JsonField.Created, "$created");
storage.Configure.MapField(JsonField.Updated, "$updated");
storage.Configure.MapField(JsonField.SchemaVersion, "$schemaVersion");

var analyzer = new StandardAnalyzer(Version.LUCENE_30, new SortedSet<string>());
var index = new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index"), analyzer);
index.Configuration.SetTypeResolver("contentType");
index.Configuration.SetRawField("$raw");
index.Configuration.SetScoreField("$score");
index.Configuration.SetIdentity("id");
index.Configuration.SetSerializer(new ZipJsonDocumentSerializer());


IStorageManager storageManager = new StorageManager(storage);
IIndexManager manager = new IndexManager(storageManager, index);
Task runner =Task.Run(async () => await storageManager.Run());
//Task runner = storage.Run();

while (true)
{
    switch (Console.ReadLine())
    {
        case "EXIT":
            goto EXIT;
            break;

        default:
            manager.Flush();
            break;
    }
}

EXIT:
await runner;


public class ZipJsonDocumentSerializer : IJsonDocumentSerializer
{

    public IFieldable Serialize(string rawfield, JObject value)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (GZipStream zip = new GZipStream(stream, CompressionLevel.Optimal))
            {
                JsonTextWriter jsonWriter = new JsonTextWriter(new StreamWriter(zip));
                value.WriteTo(jsonWriter);
                jsonWriter.Close();
            }
            byte[] buffer = stream.GetBuffer();
            return new Field(rawfield, buffer, Field.Store.YES);
        }


    }

    public JObject Deserialize(string rawfield, Document document)
    {
        byte[] buffer = document.GetBinaryValue(rawfield);
        using (MemoryStream stream = new MemoryStream(buffer))
        {
            using (GZipStream zip = new GZipStream(stream, CompressionMode.Decompress))
            {
                JsonTextReader reader = new JsonTextReader(new StreamReader(zip));
                JObject entity = (JObject)JToken.ReadFrom(reader);
                reader.Close();
                return entity;
            }
        }
    }
}