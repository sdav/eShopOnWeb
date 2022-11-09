// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Azure.Storage.Blobs;
using System.IO;
using Azure;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.eShopWeb;

namespace EventGridTrigger
{
    class Order
    {
        [JsonProperty("id")] //this part crutial! must be lowercase
        public string Id { get; set; }
        public string BuyerId;
        public DateTimeOffset OrderDate;
        public Address ShipToAddress;
        public List<OrderItem> OrderItems = new List<OrderItem>();
        public decimal FinalPrice
        {
            get
            {
                decimal count = 0;
                foreach (OrderItem oi in this.OrderItems)
                {
                    count += (oi.UnitPrice * oi.Units);
                }
                return count;
            }
        }
    }

    public static class Function1
    {
        public static Stream ToStream(this string str)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [FunctionName("Function1")]
        public static void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {

            var eventDataStr = eventGridEvent.Data.ToString();

            log.LogInformation(eventDataStr);

            Order order = JsonConvert.DeserializeObject<Order>(eventGridEvent.Data.ToString());

            //
            // saves to blob storage
            //

            var blobConnectionString = "DefaultEndpointsProtocol - xxxxxxx"; //Environment.GetEnvironmentVariable("bs");
            var fileContainerName = "container1";
            string blobName = Guid.NewGuid().ToString(); // generated a new guid for each order

            BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(fileContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            blobClient.Upload(ToStream(eventDataStr));

        }
    }
}
