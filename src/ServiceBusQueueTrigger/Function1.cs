using System;
using System.Collections.Generic;
using System.IO;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace ServiceBusQueueTrigger
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

    public static class ExtClass
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
    }
    public class Function1
    {

        [FunctionName("Function1")]
        public void Run([ServiceBusTrigger("default", Connection = "connection")] string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            Order order = JsonConvert.DeserializeObject<Order>(myQueueItem);

            //
            // saves to blob storage
            //

            var blobConnectionString = Environment.GetEnvironmentVariable("blob-connection");
            var fileContainerName = "container1";
            string blobName = Guid.NewGuid().ToString(); // generated a new guid for each order

            BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(fileContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            blobClient.Upload(ExtClass.ToStream(myQueueItem));

            throw new Exception("test exception");
        }
    }

}

