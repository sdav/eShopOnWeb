﻿using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class UriComposer : IUriComposer
{
    private readonly CatalogSettings _catalogSettings;

    public UriComposer(CatalogSettings catalogSettings) => _catalogSettings = catalogSettings;

    public string ComposePicUri(string uriTemplate)
    {
        return uriTemplate.Replace("http://catalogbaseurltobereplaced", _catalogSettings.CatalogBaseUrl);
    }

    public string GetServiceBusConnectionString()
    {
        return _catalogSettings.ServiceBusConnectionString;
    }

    public string GetDeliveryOrderProcessorUrl()
    {
        return _catalogSettings.DeliveryOrderProcessorUrl;
    }
}
