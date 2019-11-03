using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Driver;
using System.Net;
using System.Net.Http;
using MongoDB.Bson.Serialization.Attributes;
using System.Security.Authentication;
using MongoDB.Bson;
using System.Text;
using System.Collections.Generic;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace HackGSU.TDM
{
    public static class GetLiveRestaurantStats
    {
        [FunctionName("GetLiveRestaurantStats")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            DateTime fromDate = DateTime.Parse(req.Query["fromDate"]).Date;
            DateTime toDate = DateTime.Parse(req.Query["toDate"]).Date;

            string connectionString =
                   @"mongodb://cmacherl:fDyMtIVEHSCBjl7UezkNUbwStuPLmRzjjSwJscIk6rbST6xKnEof9RfSikWLoOwAEZKinLY1L2XrGVixigGtRA==@cmacherl.documents.azure.com:10255/?ssl=true&replicaSet=globaldb";
            MongoClientSettings settings = MongoClientSettings.FromUrl(
              new MongoUrl(connectionString)
            );

            settings.SslSettings =
              new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
            // var client = new MongoClient(connectionString);
            var mongoClient = new MongoClient(settings);
            IMongoDatabase db = mongoClient.GetDatabase("RestaurantData");
            var itemDetailsCollection = db.GetCollection<ItemDetails>("ItemDetails");


            var results = itemDetailsCollection.Find(x => (x.TransTime > fromDate && x.TransTime < toDate)).ToList();

            var map = new Dictionary<string, int>();
            foreach (ItemDetails itemDetails in results)
            {
                try
                {
                    int prevValue = map[itemDetails.productName];
                    map.Remove(itemDetails.productName);
                    map.Add(itemDetails.productName, prevValue + 1);
                }
                catch
                {
                    map.Add(itemDetails.productName, 1);
                }
            }

            RestaurantStats restaurantStats = new RestaurantStats();
            restaurantStats.bestSoldItem = map.OrderBy(i => i.Value).First().Key;
            restaurantStats.leastSoldItem = map.OrderByDescending(i => i.Value).First().Key;

            var tenderDetailsCollection = db.GetCollection<TenderDetails>("TenderDetails");

            var tenderResults = tenderDetailsCollection.Find(y => (y.transTime > fromDate && y.transTime < toDate)).ToList();

            Double revenue = 0;
            int count = 0;
            var empAchievement = new Dictionary<string, Double>();
            foreach (TenderDetails tenderDetails in tenderResults)
            {
                revenue += tenderDetails.tenderAmount;
                count++;

                try
                {

                    Double prevTip = empAchievement[tenderDetails.empName];
                    empAchievement.Remove(tenderDetails.empName);
                    empAchievement.Add(tenderDetails.empName, tenderDetails.tipAmount + prevTip);

                }
                catch
                {
                    empAchievement.Add(tenderDetails.empName, tenderDetails.tipAmount);
                }
            }

            restaurantStats.revenue = revenue;
            restaurantStats.profit = revenue * 30 / 100;
            restaurantStats.transactionCount = count;
            BestEmployee bestEmployee = new BestEmployee();
            bestEmployee.employeeName = empAchievement.OrderByDescending(i => i.Value).First().Key;
            bestEmployee.tipAmount = empAchievement[bestEmployee.employeeName];
            restaurantStats.bestEmployee = bestEmployee;
            var json = JsonConvert.SerializeObject(restaurantStats, Formatting.Indented);


            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}

public class RestaurantStats
{

    public Double revenue { get; set; }

    public BestEmployee bestEmployee { get; set; }

    public Double profit { get; set; }

    public String bestSoldItem { get; set; }

    public String leastSoldItem { get; set; }

    public int transactionCount { get; set; }
}

public class BestEmployee
{
    public String employeeName;

    public Double tipAmount;

}
