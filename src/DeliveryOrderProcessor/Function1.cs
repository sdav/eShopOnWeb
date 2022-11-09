using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using System.Collections.Generic;

namespace OrderItemsReserverApp
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
        static Function1()
        {
        }
        public static Stream ToStream(this string str)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            StreamReader sr = new StreamReader(req.Body);
            string requestBody = await sr.ReadToEndAsync();
            if (requestBody?.Length <= 0)
            {
                throw new Exception("No Data!");
            }

            Order order = JsonConvert.DeserializeObject<Order>(requestBody);


            //
            // saves to cosmos db
            //

            // Create a new instance of the Cosmos Client
            string EndpointUri = Environment.GetEnvironmentVariable("uri");
            string PrimaryKey = Environment.GetEnvironmentVariable("pk");
            string databaseId = Environment.GetEnvironmentVariable("db");
            string containerId = Environment.GetEnvironmentVariable("container");

            CosmosClient cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);

            // Runs the CreateDatabaseAsync method
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);

            // Run the CreateContainerAsync method
            Microsoft.Azure.Cosmos.Container container = await database.CreateContainerIfNotExistsAsync(containerId, "/id");

            await container.CreateItemAsync(order, new PartitionKey(order.Id));

            string responseMessage = "This HTTP triggered function executed successfully!";

            return new OkObjectResult(responseMessage);
        }
    }
}
