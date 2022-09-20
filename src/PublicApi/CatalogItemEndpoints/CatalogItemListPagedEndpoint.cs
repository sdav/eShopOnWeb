using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

/// <summary>
/// List Catalog Items (paged)
/// </summary>
public class CatalogItemListPagedEndpoint : IEndpoint<IResult, ListPagedCatalogItemRequest>
{
    private IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IMapper _mapper;
    private readonly ILogger<CatalogItemListPagedEndpoint> _logger;

    public CatalogItemListPagedEndpoint(IUriComposer uriComposer, IMapper mapper, ILogger<CatalogItemListPagedEndpoint> logger)
    {
        _uriComposer = uriComposer;
        _mapper = mapper;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/catalog-items",
            async (int? pageSize, int? pageIndex, int? catalogBrandId, int? catalogTypeId, IRepository<CatalogItem> itemRepository) =>
            {
                _itemRepository = itemRepository;
                return await HandleAsync(new ListPagedCatalogItemRequest(pageSize, pageIndex, catalogBrandId, catalogTypeId));
            })
            .Produces<ListPagedCatalogItemResponse>()
            .WithTags("CatalogItemEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPagedCatalogItemRequest request)
    {
        try
        {
            var response = new ListPagedCatalogItemResponse(request.CorrelationId());
            _logger.LogWarning("**DAV** HandleAsync Started!"); //Add it! I will be easier to find logs on Azure.

            var filterSpec = new CatalogFilterSpecification(request.CatalogBrandId, request.CatalogTypeId);
            int totalItems = await _itemRepository.CountAsync(filterSpec);

            var pagedSpec = new CatalogFilterPaginatedSpecification(
                skip: request.PageIndex.Value * request.PageSize.Value,
                take: request.PageSize.Value,
                brandId: request.CatalogBrandId,
                typeId: request.CatalogTypeId);

            var items = await _itemRepository.ListAsync(pagedSpec);
            _logger.LogWarning("**DAV** Hello World! items.Count = " + items.Count);//logging for numer of items

            response.CatalogItems.AddRange(items.Select(_mapper.Map<CatalogItemDto>));
            foreach (CatalogItemDto item in response.CatalogItems)
            {
                item.PictureUri = _uriComposer.ComposePicUri(item.PictureUri);
            }

            if (request.PageSize > 0)
            {
                response.PageCount = int.Parse(Math.Ceiling((decimal)totalItems / request.PageSize.Value).ToString());
            }
            else
            {
                response.PageCount = totalItems > 0 ? 1 : 0;
            }

            // throw new Exception("Cannot move further");

            _logger.LogWarning("**DAV** HandleAsync Ended!");//Add it! I will be easier to find logs on Azure.

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
