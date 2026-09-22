using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Webshop.Model.Services;

public sealed class CartService
{
    private const string SessionKey = "Cart:Items";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CartService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public Task AddItemAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (productId <= 0) throw new ArgumentOutOfRangeException(nameof(productId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        var items = ReadItems();
        var existing = items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null)
        {
            items.Add(new Model.CartItem { ProductId = productId, Quantity = quantity });
        }
        else
        {
            existing.Quantity += quantity;
        }

        WriteItems(items);
        return Task.CompletedTask;
    }

    public Task RemoveItemAsync(int productId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = ReadItems().Where(i => i.ProductId != productId).ToList();
        WriteItems(items);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Model.CartItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IEnumerable<Model.CartItem>>(ReadItems());
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ctx = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
        ctx.Session.Remove(SessionKey);
        return Task.CompletedTask;
    }

    private List<Model.CartItem> ReadItems()
    {
        var ctx = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
        var json = ctx.Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json)) return new List<Model.CartItem>();
        try
        {
            return JsonSerializer.Deserialize<List<Model.CartItem>>(json, _jsonOptions) ?? new List<Model.CartItem>();
        }
        catch
        {
            ctx.Session.Remove(SessionKey);
            return new List<Model.CartItem>();
        }
    }

    private void WriteItems(IEnumerable<Model.CartItem> items)
    {
        var ctx = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");
        var json = JsonSerializer.Serialize(items, _jsonOptions);
        ctx.Session.SetString(SessionKey, json);
    }
}
