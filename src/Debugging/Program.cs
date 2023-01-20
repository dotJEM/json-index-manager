// See https://aka.ms/new-console-template for more information

using System;
using DotJEM.Json.Index;
using DotJEM.Json.Index.Manager;
using DotJEM.Json.Storage;
using Lucene.Net.Analysis;

Console.WriteLine("Hello, World!");



IStorageManager storage = new StorageManager(new SqlServerStorageContext("Data Source=.\\DEV;Initial Catalog=ssn3db;Integrated Security=True"));
IIndexManager manager = new IndexManager(storage, new LuceneStorageIndex(new LuceneFileIndexStorage(".\\app_data\\index", new WhitespaceAnalyzer())) );


await storage.Run();
