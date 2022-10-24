using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure;
using Azure.Core;
using Azure.Core.Serialization;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

class MyEventGridEvt
{
    public string id;
    public object data;
    public string topic;
    public string subject;
    public string eventType;
    public DateTimeOffset eventTime;
    public string dataVersion;

    public MyEventGridEvt(EventGridEvent ege, object data)
    {
        id = ege.Id;
        this.data = data;
        topic = ege.Topic;
        subject = ege.Subject;
        eventType = ege.EventType;
        eventTime = ege.EventTime;
        dataVersion = ege.DataVersion;
    }
}

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);
        if (await _orderRepository.AddAsync(order) != null)
        {
            // await PostOrderAsync2EventGrid(order);
            // await PostOrderAsync2ServiceBus(order);
        }
    }

    private async Task<Boolean> PostOrderAsync2ServiceBus(Order order)
    {
        // connection string to your Service Bus namespace
        string connectionString = "xxx"; // Endpoint=sb:xxxx

        // name of your Service Bus queue
        string queueName = "default";

        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client = new ServiceBusClient(connectionString);

        // the sender used to publish messages to the queue
        ServiceBusSender sender = client.CreateSender(queueName);

        // create a message that we can send. UTF-8 encoding is used when providing a string.
        var json = JsonConvert.SerializeObject(order);
        ServiceBusMessage message = new ServiceBusMessage(json);

        // send the message
        await sender.SendMessageAsync(message);

        return true;
    }

    private async Task<Boolean> PostOrderAsync2EventGrid(Order order)
    {
        var topicEndpoint = "https://[fake].xxxxxxx/api/events";
        var topicAccessKey = "xxxx";

        //var topicEndpoint = "http://localhost:7071/runtime/webhooks/EventGrid?functionName=Function1";
        //var topicAccessKey = "-";

        string subject = "Example";
        string eventType = "Event.Type";
        string dataVersion = "1.0";

        var resp = await this.sendEvent(topicEndpoint, topicAccessKey,
            subject, eventType, dataVersion,
            order);

        if (!resp)
        {
            throw new Exception("Failed to load data into Queue!");
        }

        return true;
    }

    private async Task<Boolean> sendEvent(
        string topicEndpoint,
        string topicAccessKey,
        string subject,
        string eventType,
        string dataVersion,
        Order order)
    {
        if (topicEndpoint.Contains("localhost", StringComparison.InvariantCultureIgnoreCase))
        {
            //
            // Localhost development
            //

            var wrappedOrder = new EventGridEvent(
                subject,
                eventType,
                dataVersion,
                order);

            var wrappedOrdeFix = new MyEventGridEvt(wrappedOrder, order);

            var json = JsonConvert.SerializeObject(wrappedOrdeFix);

            HttpClient _client = new HttpClient();
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, topicEndpoint);
            req.Content = new StringContent(json);
            req.Headers.Add("aeg-event-type", "Notification");

            var response = await _client.SendAsync(req);
            return response.IsSuccessStatusCode;

        }
        else
        {
            //
            // Azure EventGrid
            //

            EventGridPublisherClient client = new EventGridPublisherClient(
            new Uri(topicEndpoint),
            new AzureKeyCredential(topicAccessKey));

            //var json = JsonConvert.SerializeObject(order);

            // EventGridEvent with custom model serialized to JSON
            var evt = new EventGridEvent(
                subject,
                eventType,
                dataVersion,
                order);

            // Send the events
            var response = await client.SendEventAsync(evt);
            return !response.IsError;
        }
    }


}
