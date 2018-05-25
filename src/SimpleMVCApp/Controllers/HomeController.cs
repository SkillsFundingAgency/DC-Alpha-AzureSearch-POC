using Microsoft.Azure.Search.Models;
using System.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Mvc;

namespace SimpleSearchMVCApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly FeaturesSearch _featuresSearch = new FeaturesSearch();


        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Search(string q = "")
        {
            // If blank search, assume they want to search everything
            if (string.IsNullOrWhiteSpace(q))
            {
                q = "*";
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            IList<SearchResult> results = _featuresSearch.Search(q).Results;
            stopwatch.Stop();

            var properties = new Dictionary<string, string> {
                {"SearchServiceName", ConfigurationManager.AppSettings["SearchServiceName"]},
                {"SearchId", ""},
                {"IndexName", ConfigurationManager.AppSettings["SearchIndexName"]},
                {"QueryTerms", q} ,
                {"ResultCount", results.Count.ToString() },
                {"ScoringProfile", "NA" }
            };
            MvcApplication.TelemetryClient.TrackEvent("Search", properties);

            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new { res = results, time = stopwatch.ElapsedMilliseconds }
            };
        }
    }
}
