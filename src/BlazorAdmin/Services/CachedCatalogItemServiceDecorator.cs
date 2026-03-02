using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using BlazorShared.Interfaces;
using BlazorShared.Models;
using Microsoft.Extensions.Logging;

namespace BlazorAdmin.Services;

public class CachedCatalogItemServiceDecorator : ICatalogItemService
{
    private readonly ILocalStorageService _localStorageService;
    private readonly CatalogItemService _catalogItemService;
    private ILogger<CachedCatalogItemServiceDecorator> _logger;

    public CachedCatalogItemServiceDecorator(ILocalStorageService localStorageService,
        CatalogItemService catalogItemService,
        ILogger<CachedCatalogItemServiceDecorator> logger)
    {
        _localStorageService = localStorageService;
        _catalogItemService = catalogItemService;
        _logger = logger;
    }

    private const string ItemsKey = "items";

    private async Task<List<CatalogItem>> GetOrRefreshLocalCache(Func<Task<List<CatalogItem>>> loader)
    {
        var cacheEntry = await _localStorageService.GetItemAsync<CacheEntry<List<CatalogItem>>>(ItemsKey);
        if (cacheEntry != null)
        {
            _logger.LogInformation("Found local cache for {Key}. Age={AgeMinutes} minutes", ItemsKey, (DateTime.UtcNow - cacheEntry.DateCreated).TotalMinutes);
            if (cacheEntry.DateCreated.AddMinutes(1) > DateTime.UtcNow)
            {
                return cacheEntry.Value;
            }

            _logger.LogInformation("Cache expired for {Key}, refreshing.", ItemsKey);
            await _localStorageService.RemoveItemAsync(ItemsKey);
        }

        var items = await loader();
        var entry = new CacheEntry<List<CatalogItem>>(items);
        await _localStorageService.SetItemAsync(ItemsKey, entry);
        return items;
    }

    public Task<List<CatalogItem>> ListPaged(int pageSize) => GetOrRefreshLocalCache(() => _catalogItemService.ListPaged(pageSize));

    public Task<List<CatalogItem>> List() => GetOrRefreshLocalCache(() => _catalogItemService.List());

    public async Task<CatalogItem> GetById(int id)
    {
        return (await List()).FirstOrDefault(x => x.Id == id);
    }

    public async Task<CatalogItem> Create(CreateCatalogItemRequest catalogItem)
    {
        var result = await _catalogItemService.Create(catalogItem);
        await RefreshLocalStorageList();

        return result;
    }

    public async Task<CatalogItem> Edit(CatalogItem catalogItem)
    {
        var result = await _catalogItemService.Edit(catalogItem);
        await RefreshLocalStorageList();

        return result;
    }

    public async Task<string> Delete(int id)
    {
        var result = await _catalogItemService.Delete(id);
        await RefreshLocalStorageList();

        return result;
    }

    private async Task RefreshLocalStorageList()
    {
        string key = "items";

        await _localStorageService.RemoveItemAsync(key);
        var items = await _catalogItemService.List();
        var entry = new CacheEntry<List<CatalogItem>>(items);
        await _localStorageService.SetItemAsync(key, entry);
    }
}
