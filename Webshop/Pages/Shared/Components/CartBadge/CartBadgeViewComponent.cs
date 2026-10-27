using Microsoft.AspNetCore.Mvc;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages.Shared.Components.CartBadge;

public class CartBadgeViewComponent : ViewComponent
{
    private readonly CartService _cartService;
    private readonly IProductRepository _productRepository;

    public CartBadgeViewComponent(CartService cartService, IProductRepository productRepository)
    {
        _cartService = cartService;
        _productRepository = productRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        // Totalen beregnes ALTID fra DB-priser — sessionen rummer kun id + antal,
        // så prisen kan ikke manipuleres via session/cookiedata.
        var items = (await _cartService.GetItemsAsync(CancellationToken.None)).ToList();

        decimal total = 0m;
        foreach (var item in items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, CancellationToken.None);
            if (product is not null)
                total += product.Price * item.Quantity;
        }

        return View(total);
    }
}