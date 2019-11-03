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

namespace HackGSU.TDM
{
    public static class GetItemStats
    {
        [FunctionName("GetItemStats")]
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
            var tenderDetailsCollection = db.GetCollection<ItemDetails>("ItemDetails");

            var itemResults = tenderDetailsCollection.Find(y => (y.TransTime > fromDate && y.TransTime < toDate)).ToList();


            var itemData = new Dictionary<String, int>();
            foreach (ItemDetails itemDetails in itemResults)
            {
                try
                {
                    int prevCount = itemData[itemDetails.productName];
                    itemData.Remove(itemDetails.productName);
                    itemData.Add(itemDetails.productName, prevCount + 1);
                }
                catch
                {
                    itemData.Add(itemDetails.productName, 1);
                }
            }

           // var convertedDictionary = transData.ToDictionary(item => item.Key.ToString(), item => item.Value.ToString());

            var json = JsonConvert.SerializeObject(itemData);


            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }
}