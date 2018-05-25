using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace DataIndexer
{
    public static class Program
    {
        private static readonly string[] SpinnerAnimationFrames =
        {
            "#=>        @", "#  =>      @", "#    =>    @", "#      =>  @", "#        =>@", "#          @"
        };

        private static int _currentAnimationFrame;

        private static bool _keepSpinning;

        private static ISearchServiceClient _searchClient;

        // This Sample shows how to delete, create, upload documents and query an index
        public static void Main(string[] args)
        {
            string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

            // Create an HTTP reference to the catalog index
            _searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));

            Console.WriteLine("Deleting index, data source, and indexer...");
            if (DeleteIndexingResources())
            {
                Console.WriteLine("Creating index...");
                CreateIndex();
                Console.WriteLine("Sync documents from Azure SQL...");
                SyncDataFromAzureSql();
            }

            Console.WriteLine("Complete.  Press any key to end application...");
            Console.ReadKey();
        }

        private static bool DeleteIndexingResources()
        {
            string geoNamesIndex = ConfigurationManager.AppSettings["SearchGeoNamesIndex"];
            string usgsDataSource = ConfigurationManager.AppSettings["SearchUsageDataSource"];
            string usgsIndexer = ConfigurationManager.AppSettings["SearchUsageindexer"];

            // Delete the index, data source, and indexer.
            try
            {
                _searchClient.Indexes.Delete(geoNamesIndex);
                _searchClient.DataSources.Delete(usgsDataSource);
                _searchClient.Indexers.Delete(usgsIndexer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting indexing resources: {ex.Message}\r\n");
                return false;
            }

            return true;
        }

        private static void CreateIndex()
        {
            string geoNamesIndex = ConfigurationManager.AppSettings["SearchGeoNamesIndex"];

            // Create the Azure Search index based on the included schema
            try
            {
                var definition = new Index
                {
                    Name = geoNamesIndex,
                    Fields = new[]
                    {
                        new Field("Column1", DataType.String) { IsKey = true,  IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true }
                    }
                };

                _searchClient.Indexes.Create(definition);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating index: {ex.Message}\r\n");
            }
        }

        private static void SyncDataFromAzureSql()
        {
            // This will use the Azure Search Indexer to synchronize data from Azure SQL to Azure Search
            Console.WriteLine("Creating Data Source...");
            string geoNamesIndex = ConfigurationManager.AppSettings["SearchGeoNamesIndex"];
            string usgsIndexer = ConfigurationManager.AppSettings["SearchUsageindexer"];
            string usgsDataSource = ConfigurationManager.AppSettings["SearchUsageDataSource"];

            var dataSource =
                DataSource.AzureSql(
                    usgsDataSource,
                    ConfigurationManager.AppSettings["SearchSqlSourceConnectionString"],
                    ConfigurationManager.AppSettings["SearchSqlSourceTableOrView"],
                    description: ConfigurationManager.AppSettings["SearchSqlSourceDescription"]);

            try
            {
                _searchClient.DataSources.Create(dataSource);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating data source: {ex.Message}");
                return;
            }

            Console.WriteLine("Creating Indexer and syncing data...");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var indexer = new Indexer
                {
                    Name = usgsIndexer,
                    Description = "Alpha data indexer",
                    DataSourceName = dataSource.Name,
                    TargetIndexName = geoNamesIndex
                };

            try
            {
                _searchClient.Indexers.Create(indexer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating and running indexer: {ex.Message}");
                return;
            }

            Console.WriteLine("{0}", "Synchronization running...");

            _keepSpinning = true;
            Thread spinner = new Thread(Spin);
            spinner.Start();

            while (_keepSpinning)
            {
                IndexerExecutionInfo status;

                try
                {
                    status = _searchClient.Indexers.GetStatus(indexer.Name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error polling for indexer status: {ex.Message}");
                    return;
                }

                IndexerExecutionResult lastResult = status.LastResult;
                if (lastResult == null)
                {
                    continue;
                }

                switch (lastResult.Status)
                {
                    case IndexerExecutionStatus.InProgress:
                        break;
                    case IndexerExecutionStatus.Success:
                        _keepSpinning = false;
                        stopwatch.Stop();
                        _keepSpinning = false;
                        Console.WriteLine($"Synchronized {lastResult.ItemCount} rows in {stopwatch.ElapsedMilliseconds}ms");
                        break;
                    case IndexerExecutionStatus.TransientFailure:
                        _keepSpinning = false;
                        Console.WriteLine($"Transient Failure: {lastResult.ErrorMessage}");
                        break;
                    case IndexerExecutionStatus.Reset:
                        Console.WriteLine("Reset");
                        break;
                    default:
                        _keepSpinning = false;
                        Console.WriteLine($"Synchronization failed: {lastResult.ErrorMessage}");
                        break;
                }
            }
        }

        private static void Spin(object obj)
        {
            try
            {
                while (_keepSpinning)
                {
                    UpdateProgress();
                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void UpdateProgress()
        {
            // Store the current position of the cursor
            var originalX = Console.CursorLeft;
            var originalY = Console.CursorTop;

            // Write the next frame (character) in the spinner animation
            Console.Write(SpinnerAnimationFrames[_currentAnimationFrame]);

            // Keep looping around all the animation frames
            _currentAnimationFrame++;
            if (_currentAnimationFrame == SpinnerAnimationFrames.Length)
            {
                _currentAnimationFrame = 0;
            }

            // Restore cursor to original position
            Console.SetCursorPosition(originalX, originalY);
        }
    }
}
