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
    public static class GetLiveTDMData
    {
        [FunctionName("GetLiveTDMData")]
        public static async Task<HttpResponseMessage>  Run(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string id = Guid.NewGuid().ToString();

            //  string trimmedData = requestBody.Substring(0,requestBody.Length-2)+",\r\n                \"hack\": \""+id+"\"}";
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            List<ResContent> z = (data?.attributes).ToObject<List<ResContent>>();
            String tlog = "";
            foreach (ResContent x in z)
            {
                if (x.Key == "tlog_id")
                {
                    tlog = x.value;
                    break;
                }

            }

            var client = new HttpClient();
            //client.BaseAddress = new Uri("https://gateway-staging.ncrcloud.com/transaction-document/transaction-documents/" + tlog);
            client.DefaultRequestHeaders.Add("nep-organization", "ur-hack");
            client.DefaultRequestHeaders.Add("nep-service-version", "2:1");
            client.DefaultRequestHeaders.Add("nep-application-key", "8a008d406ddb112d016e0683ea5a003b");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "YWNjdDpyb290QGhhY2tfaGFja2VyY2xvd25zOiorQD5NODhmIVs=");
            var response = client.GetAsync("https://gateway-staging.ncrcloud.com/transaction-document/transaction-documents/" + tlog).Result.Content.ReadAsStringAsync().Result;
            dynamic tdmGetResponse = JsonConvert.DeserializeObject(response);

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
            var collection = db.GetCollection<ItemDetails>("ItemDetails");

            foreach (JObject item in tdmGetResponse?.tlog.items)
            {
                ItemDetails itemDetails = new ItemDetails();
                itemDetails.itemId = item.GetValue("id").ToString();
                itemDetails.tlog_id = tlog;
                itemDetails.productName = item.GetValue("productName").ToString();
                itemDetails.quantity = Convert.ToInt16(item["quantity"]["quantity"]);
                itemDetails.actualAmount = Convert.ToDouble(item["actualAmount"]["amount"]);
                itemDetails.extendedAmount = Convert.ToDouble(item["extendedAmount"]["amount"]);
                try{
                itemDetails.foodcategory = (item["category"]["name"]).ToString();
                }
                catch{
                itemDetails.foodcategory = "N/A";
                }
                Console.WriteLine((item["endDateTimeUtc"]["dateTime"]).ToString());
                itemDetails.TransTime = DateTime.Parse((item["endDateTimeUtc"]["dateTime"]).ToString()).Date;
                itemDetails.RevCenterName = (item["revenueCenter"]["name"]).ToString();
                //bson1 = (itemDetails).ToBsonDocument();//itemsList.Add((itemDetails).ToBsonDocument());
                var json = JsonConvert.SerializeObject(itemDetails, Formatting.Indented);
                await collection.InsertOneAsync(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<ItemDetails>(json));
            }

            var collection2 = db.GetCollection<TenderDetails>("TenderDetails");
            TenderDetails tenderDetails = new TenderDetails();
            tenderDetails.restName = tdmGetResponse?.siteInfo.name;
            tenderDetails.transTime = Convert.ToDateTime(tdmGetResponse?.closeDateTimeUtc.dateTime);
            tenderDetails.tlog_id = tlog;
            tenderDetails.tenderAmount = Convert.ToDouble(tdmGetResponse?.tlog.totals.grandAmount.amount); 
            foreach (JObject item in tdmGetResponse?.tlog.tenders)
            {
                tenderDetails.empId = (item["employee"]["id"]).ToString();
                tenderDetails.empName = (item["employee"]["name"]).ToString();
                tenderDetails.tipAmount += Convert.ToDouble(item["tipAmount"]["amount"]);
                var json = JsonConvert.SerializeObject(tenderDetails, Formatting.Indented);
                await collection2.InsertOneAsync(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<TenderDetails>(json));
            }

            //resDetails.actualAmount =
            var test = tdmGetResponse?.id;

            BsonDocument bson = new BsonDocument {
     { "TDMData", tlog },
     { "hack", id }
 };


            // collection.InsertOne(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(json));

            //await collection.InsertOneAsync(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<BsonDocument>(bson));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
        }
    }
}

public class ResContent
{
    public string Key { get; set; }
    public string value { get; set; }

}

public class TenderDetails
{
    [BsonId]
    public ObjectId _id { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime transTime;
    public string tlog_id { get; set; }
    public string empId { get; set; }
    public string empName { get; set; }
    public double tipAmount { get; set; }
    public string restName { get; set; }
    public double tenderAmount {get; set;}

    public string mongoKey = "Hack";
}

public class ItemDetails
{
    [BsonId]
    public ObjectId _id { get; set; }
    public string itemId { get; set; }
    public string tlog_id { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    public DateTime TransTime { get; set; }
    public string productName { get; set; }
    public string RevCenterName { get; set; }
    public int quantity { get; set; }
    public string foodcategory { get; set; }
    public double actualAmount { get; set; }
    public double extendedAmount { get; set; }
    // public string orderchannel;
    public string mongoKey = "Hack";

}

