using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using Webshop.Model;
using Webshop.Model.Repos;

namespace Webshop.Pages;

[Authorize(Roles = "Admin")]
public class AdminProductEditModel : PageModel
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<AdminProductEditModel> _logger;

    public AdminProductEditModel(IProductRepository productRepository,
                                 ILogger<AdminProductEditModel> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public bool IsNewProduct => Input.Id == 0;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> CategoryOptions { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Navn er påkrævet.")]
        [StringLength(200)]
        public string Name { get; set; } = "";

        // Valgfrit felt: nullable ⇒ ingen implicit required.
        [StringLength(2000)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Pris er påkrævet.")]
        [Range(0.01, 1_000_000, ErrorMessage = "Prisen skal være et positivt beløb.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Lagerbeholdning er påkrævet.")]
        [Range(0, 1_000_000)]
        public int Stock { get; set; }

        [Required(ErrorMessage = "Kategori er påkrævet.")]
        public int CategoryId { get; set; }

        // Valgfrit felt: nullable ⇒ må være blankt.
        [StringLength(500, ErrorMessage = "Billede-URL må maks være 500 tegn.")]
        [RegularExpression(@"^(https?://|/)[^\s]*$",
            ErrorMessage = "Billede-URL skal starte med http(s):// eller / og må ikke indeholde mellemrum.")]
        public string? ImageUrl { get; set; }
    }

    public async Task OnGetAsync(int? id, CancellationToken ct)
    {
        await LoadCategoriesAsync(ct);

        if (id is int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId, ct);
            if (product is null)
            {
                TempData["ErrorMessage"] = "Produktet blev ikke fundet.";
                RedirectToPage("/AdminProducts");
                return;
            }

            Input = new InputModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,   
                Price = product.Price,
                Stock = product.Stock,
                CategoryId = product.CategoryId,
                ImageUrl = product.ImageUrl          
            };
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!User.IsInRole("Admin"))
            return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(ct);
            return Page();
        }

        var imageUrl = string.IsNullOrWhiteSpace(Input.ImageUrl) ? null : Input.ImageUrl.Trim();
        var description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();

        var product = new Product
        {
            Id = Input.Id,
            Name = Input.Name.Trim(),
            Description = description,
            Price = Input.Price,
            Stock = Input.Stock,
            CategoryId = Input.CategoryId,
            ImageUrl = imageUrl
        };

        if (Input.Id == 0)
        {
            await _productRepository.CreateAsync(product, ct);
            _logger.LogInformation("Admin {Admin} oprettede produkt '{ProductName}'",
                User.Identity!.Name, product.Name);
        }
        else
        {
            await _productRepository.UpdateAsync(product, ct);
            _logger.LogInformation("Admin {Admin} redigerede produkt {ProductId}",
                User.Identity!.Name, product.Id);
        }

        return RedirectToPage("/AdminProducts");
    }

    private async Task LoadCategoriesAsync(CancellationToken ct)
    {
        var categories = await _productRepository.GetCategoryMapAsync(ct);
        CategoryOptions = categories
            .Select(c => new SelectListItem { Value = c.Key.ToString(), Text = c.Value })
            .ToList();
    }
}