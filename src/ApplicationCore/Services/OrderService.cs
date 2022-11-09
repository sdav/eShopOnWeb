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
            if (!await PostOrder2DeliveryOrderProcessor(order))
            {
                System.Diagnostics.Debug.WriteLine("Delivery Order Processor Failed");
            }
            if (!await PostOrder2OrderItemReserver(order))
            {
                System.Diagnostics.Debug.WriteLine("Order Item Reserver Failed");
            }
        }
    }

    // uses Service Bus to process orders or sends email if failing
    private async Task<Boolean> PostOrder2OrderItemReserver(Order order)
    {
        // connection string to your Service Bus namespace
        string connectionString = _uriComposer.GetServiceBusConnectionString(); // Endpoint=sb:xxxx

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

    // post message to Azure App -> CosmosDB
    private async Task<Boolean> PostOrder2DeliveryOrderProcessor(Order order)
    {
        var functionAppUrl = _uriComposer.GetDeliveryOrderProcessorUrl();
        var json = JsonConvert.SerializeObject(order);
        HttpClient _client = new HttpClient();
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, functionAppUrl);
        req.Content = new StringContent(json);
        var response = await _client.SendAsync(req);

        if (response.IsSuccessStatusCode)
        {
            return response.IsSuccessStatusCode;
        }

        throw new Exception("Failed to send order to delivery!");
    }

}
