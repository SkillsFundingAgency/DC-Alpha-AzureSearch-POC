using System;
using System.Configuration;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace SimpleSearchMVCApp
{
    public class FeaturesSearch
    {
        private static readonly ISearchIndexClient IndexClient;

        private static string _errorMessage;

        static FeaturesSearch()
        {
            try
            {
                string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
                string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

                // Create an HTTP reference to the catalog index
                ISearchServiceClient searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
                IndexClient = searchClient.Indexes.GetClient("geonames");
            }
            catch (Exception e)
            {
                _errorMessage = e.Message;
            }
        }

        public DocumentSearchResult Search(string searchText)
        {
            // Execute search based on query string
            try
            {
                SearchParameters sp = new SearchParameters { SearchMode = SearchMode.All };
                return IndexClient.Documents.Search(searchText, sp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error querying index: {ex.Message}\r\n");
            }
            return null;
        }

    }
}