using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Pages.Basket;
using Microsoft.Extensions.Logging;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.Web.Services;

public class BasketViewModelService : IBasketViewModelService
{
    private readonly IRepository<Basket> _basketRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IBasketQueryService _basketQueryService;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly ILogger<BasketViewModelService> _logger;

    public BasketViewModelService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IUriComposer uriComposer,
        IBasketQueryService basketQueryService)
    {
        _basketRepository = basketRepository;
        _uriComposer = uriComposer;
        _basketQueryService = basketQueryService;
        _itemRepository = itemRepository;
    }

    public async Task<BasketViewModel> GetOrCreateBasketForUser(string userName)
    {
        Guard.Against.NullOrEmpty(userName, nameof(userName));

        var basketSpec = new BasketWithItemsSpecification(userName);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        if (basket is null)
        {
            _logger?.LogInformation("Creating new basket for user {UserName}", userName);
            return await CreateBasketForUser(userName);
        }

        var viewModel = await Map(basket);
        return viewModel;
    }

    private async Task<BasketViewModel> CreateBasketForUser(string userId)
    {
        var basket = new Basket(userId);
        await _basketRepository.AddAsync(basket);

        return new BasketViewModel()
        {
            BuyerId = basket.BuyerId,
            Id = basket.Id,
        };
    }

    private async Task<List<BasketItemViewModel>> GetBasketItems(IReadOnlyCollection<BasketItem> basketItems)
    {
        if (basketItems == null || basketItems.Count == 0)
            return new List<BasketItemViewModel>();

        var catalogIds = basketItems.Select(b => b.CatalogItemId).Distinct().ToArray();
        var catalogItemsSpecification = new CatalogItemsSpecification(catalogIds);
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        // Build a dictionary for O(1) lookup
        var catalogById = catalogItems.ToDictionary(c => c.Id);

        var items = new List<BasketItemViewModel>(basketItems.Count);
        foreach (var basketItem in basketItems)
        {
            if (!catalogById.TryGetValue(basketItem.CatalogItemId, out var catalogItem))
            {
                _logger?.LogWarning("Catalog item {CatalogItemId} not found for basket item {BasketItemId}", basketItem.CatalogItemId, basketItem.Id);
                continue;
            }

            items.Add(new BasketItemViewModel
            {
                Id = basketItem.Id,
                UnitPrice = basketItem.UnitPrice,
                Quantity = basketItem.Quantity,
                CatalogItemId = basketItem.CatalogItemId,
                PictureUrl = _uriComposer.ComposePicUri(catalogItem.PictureUri),
                ProductName = catalogItem.Name
            });
        }

        return items;
    }

    public async Task<BasketViewModel> Map(Basket basket)
    {
        return new BasketViewModel()
        {
            BuyerId = basket.BuyerId,
            Id = basket.Id,
            Items = await GetBasketItems(basket.Items)
        };
    }

    public async Task<int> CountTotalBasketItems(string username)
    {
        Guard.Against.NullOrEmpty(username, nameof(username));
        var counter = await _basketQueryService.CountTotalBasketItems(username);
        return counter;
    }
}
