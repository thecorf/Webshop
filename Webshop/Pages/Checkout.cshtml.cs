using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services;

namespace Webshop.Pages;

[Authorize]
public class CheckoutModel : PageModel
{
    private readonly CheckoutFacade _checkoutFacade;
    private readonly CartService _cartService;
    private readonly IProductRepository _productRepository;

    public CheckoutModel(CheckoutFacade checkoutFacade,
                         CartService cartService,
                         IProductRepository productRepository)
    {
        _checkoutFacade = checkoutFacade;
        _cartService = cartService;
        _productRepository = productRepository;
    }

    public List<CheckoutLine> Lines { get; private set; } = new();
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public IReadOnlyCollection<Model.Services.Payments.IPaymentMethod> PaymentMethods
        => _checkoutFacade.AvailablePaymentMethods;

    [BindProperty]
    public InputModel Input { get; set; } = new() { PaymentMethod = "card" };

    public class InputModel
    {
        [Required(ErrorMessage = "Vælg en betalingsmetode.")]
        public string PaymentMethod { get; set; } = "card";
        public string? CardNumber { get; set; }
        public string? CardExpiry { get; set; }
        public string? CardCvc { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadLinesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadLinesAsync(ct);
        if (Lines.Count == 0)
            return RedirectToPage("/Cart");

        if (!ModelState.IsValid)
            return Page();

        var userId = GetCurrentUserId();
        if (userId is null)
            return Challenge();

        var result = await _checkoutFacade.CheckoutAsync(
            userId.Value,
            Input.PaymentMethod,
            new PaymentCardDetails(Input.CardNumber, Input.CardExpiry, Input.CardCvc),
            ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Checkout fejlede.");
            return Page();
        }

        return RedirectToPage("/OrderConfirmation", new { id = result.OrderId });
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }

    private async Task LoadLinesAsync(CancellationToken ct)
    {
        var items = (await _cartService.GetItemsAsync(ct)).ToList();
        var result = new List<CheckoutLine>();

        foreach (var item in items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null) continue;
            var qty = Math.Min(item.Quantity, product.Stock);
            if (qty > 0)
                result.Add(new CheckoutLine(product.Name, product.Price, qty));
        }
        Lines = result;
    }

    public sealed record CheckoutLine(string ProductName, decimal UnitPrice, int Quantity)
    {
        public decimal LineTotal => UnitPrice * Quantity;
    }
}