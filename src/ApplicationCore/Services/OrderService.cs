using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Newtonsoft.Json;


namespace Microsoft.eShopWeb.ApplicationCore.Services;

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
            await PostOrderAsync(order);
        }
    }

    private async Task<Boolean> PostOrderAsync(Order order)
    {
        //var topicEndpoint = "https://upload.switzerlandnorth-1.eventgrid.azure.net/api/events";
        //var topicAccessKey = "bCRGGw3u5lQVYEQnX2/0y20hb5Wt7btitKKxY6e+DaY=";

        var topicEndpoint = "http://localhost:7071/runtime/webhooks/EventGrid?functionName=Function1";
        var topicAccessKey = "abc";

        EventGridPublisherClient client = new EventGridPublisherClient(
            new Uri(topicEndpoint),
            new AzureKeyCredential(topicAccessKey));


        var json = JsonConvert.SerializeObject(order);

        // Add EventGridEvents to a list to publish to the topic
        List<EventGridEvent> eventsList = new List<EventGridEvent>
        {
            // EventGridEvent with custom model serialized to JSON
            new EventGridEvent(
                "ExampleEventSubject",
                "Example.EventType",
                "1.0",
                json),

        };

        // Send the events
        var response = await client.SendEventsAsync(eventsList);

        //HttpClient _client = new HttpClient();
        //HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, functionAppUrl);
        //req.Content = new StringContent(json);
        //var response = await _client.SendAsync(req);

        if (response.IsError)
        {
            throw new Exception("Failed to load data into Queue!");
        }

        return true;
    }
}
