﻿namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IUriComposer
{
    string ComposePicUri(string uriTemplate);
    string GetServiceBusConnectionString();
    string GetDeliveryOrderProcessorUrl();
}
