using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

public class CartModel : PageModel
{
    private readonly CartService _cartService;
    private readonly IProductRepository _productRepository;

    public CartModel(CartService cartService, IProductRepository productRepository)
    {
        _cartService = cartService;
        _productRepository = productRepository;
    }

    public List<CartLine> Lines { get; private set; } = new();
    public decimal Total => Lines.Sum(l => l.LineTotal);

    public async Task OnGetAsync(CancellationToken ct)
        => Lines = await LoadLinesAsync(ct);

    public async Task<IActionResult> OnPostUpdateAsync(int productId, int quantity, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);

        if (product is null)
        {
            await _cartService.RemoveItemAsync(productId, ct);
        }
        else if (quantity < 1)
        {
            await _cartService.RemoveItemAsync(productId, ct);
        }
        else
        {
            // Clamp til lagerbeholdning — brugeren kan håndlave POST-værdier, så vi stoler ikke på input
            var clamped = Math.Min(quantity, Math.Max(product.Stock, 1));
            await _cartService.RemoveItemAsync(productId, ct);
            await _cartService.AddItemAsync(productId, clamped, ct);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId, CancellationToken ct)
    {
        await _cartService.RemoveItemAsync(productId, ct);
        return RedirectToPage();
    }

    private async Task<List<CartLine>> LoadLinesAsync(CancellationToken ct)
    {
        var items = (await _cartService.GetItemsAsync(ct)).ToList();
        var lines = new List<CartLine>();

        foreach (var item in items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null)
            {
                // Produkt slettet af admin siden det kom i kurven — ryd linjen ud
                await _cartService.RemoveItemAsync(item.ProductId, ct);
                continue;
            }

            var quantity = Math.Min(item.Quantity, product.Stock);
            if (quantity != item.Quantity)
                await UpdateQuantityAsync(item.ProductId, quantity, ct);

            if (quantity > 0)
                lines.Add(new CartLine(product, quantity));
        }

        return lines;
    }

    private async Task UpdateQuantityAsync(int productId, int quantity, CancellationToken ct)
    {
        await _cartService.RemoveItemAsync(productId, ct);
        if (quantity > 0)
            await _cartService.AddItemAsync(productId, quantity, ct);
    }

    public record CartLine(Product Product, int Quantity)
    {
        public int ProductId => Product.Id;
        public string Name => Product.Name;
        public decimal UnitPrice => Product.Price;
        public string? ImageUrl => Product.ImageUrl;
        public int Stock => Product.Stock;
        public decimal LineTotal => Product.Price * Quantity;
    }
}