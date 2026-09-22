using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;

namespace Webshop.Pages;

[Authorize(Roles = "Admin")]
public class AdminProductsModel : PageModel
{
    private readonly IProductRepository _productRepository;

    public AdminProductsModel(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public List<Product> Products { get; set; } = new();
    public Dictionary<int, string> Categories { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Products = await _productRepository.GetAllAsync(ct);
        Categories = await _productRepository.GetCategoryMapAsync(ct);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        // Forsvar i dybden: selv hvis authorization-middleware fejler, nægtes ikke-admins her
        if (!User.IsInRole("Admin"))
            return Forbid();

        try
        {
            await _productRepository.DeleteAsync(id, ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // FK-CONSTRAINT: produktet er på eksisterende ordrelinjer
            TempData["ErrorMessage"] =
                "Produktet kan ikke slettes, da det indgår i eksisterende ordrer.";
        }

        return RedirectToPage();
    }

    public string CategoryName(int categoryId)
        => Categories.TryGetValue(categoryId, out var name) ? name : "-";
}