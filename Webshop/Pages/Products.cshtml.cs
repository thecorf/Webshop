using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

public class ProductsModel : PageModel
{
    private readonly IProductRepository _productRepository;
    private readonly CartService _cartService;
    private readonly ILogger<ProductsModel> _logger;

    public ProductsModel(IProductRepository productRepository,
                         CartService cartService,
                         ILogger<ProductsModel> logger)
    {
        _productRepository = productRepository;
        _cartService = cartService;
        _logger = logger;
    }

    public List<Product> Products { get; private set; } = new();
    public Dictionary<int, string> Categories { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Categories = await _productRepository.GetCategoryMapAsync(ct);

        var search = Query?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            Products = await _productRepository.SearchAsync(search, ct);
            if (CategoryId is int id)
                Products = Products.Where(p => p.CategoryId == id).ToList();
        }
        else if (CategoryId is int id)
        {
            Products = await _productRepository.GetByCategoryAsync(id, ct);
        }
        else
        {
            Products = await _productRepository.GetAllAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null || product.Stock <= 0)
        {
            TempData["CartMessage"] = "Produktet er ikke længere tilgængeligt.";
            return RedirectToPage();
        }

        await _cartService.AddItemAsync(productId, 1, ct);
        _logger.LogInformation("Produkt {ProductId} lagt i kurv", productId);

        TempData["CartMessage"] = $"'{product.Name}' lagt i kurven.";
        return RedirectToPage();
    }
}