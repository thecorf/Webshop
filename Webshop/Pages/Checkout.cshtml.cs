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

public class CheckoutModel : PageModel
{
    private readonly CheckoutFacade _checkoutFacade;
    private readonly CartService _cartService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<CheckoutModel> _logger;

    public CheckoutModel(CheckoutFacade checkoutFacade,
                         CartService cartService,
                         IProductRepository productRepository,
                         ILogger<CheckoutModel> logger)
    {
        _checkoutFacade = checkoutFacade;
        _cartService = cartService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public List<CheckoutLine> Lines { get; private set; } = new();
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public bool IsGuest => GetCurrentUserId() is null;
    public IReadOnlyCollection<Model.Services.Payments.IPaymentMethod> PaymentMethods
        => _checkoutFacade.AvailablePaymentMethods;

    [BindProperty]
    public InputModel Input { get; set; } = new() { PaymentMethod = "card" };

    public class InputModel
    {
        [Required(ErrorMessage = "Vælg en betalingsmetode.")]
        public string PaymentMethod { get; set; } = "card";


        [EmailAddress(ErrorMessage = "Angiv en gyldig e-mailadresse.")]
        [StringLength(320, ErrorMessage = "E-mailen er for lang.")]
        public string? GuestEmail { get; set; }

        public string? CardNumber { get; set; }
        public string? CardExpiry { get; set; }
        public string? CardCvc { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (IsAdmin())
            return BlockedForAdmins();

        await LoadLinesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {

        if (IsAdmin())
            return BlockedForAdmins();

        await LoadLinesAsync(ct);
        if (Lines.Count == 0)
            return RedirectToPage("/Cart");

        var userId = GetCurrentUserId();
        var isGuest = userId is null;


        if (isGuest && string.IsNullOrWhiteSpace(Input.GuestEmail))
        {
            ModelState.AddModelError("Input.GuestEmail",
                "Angiv din e-mail, så du kan modtage din ordrebekræftelse.");
            return Page();
        }

        if (!ModelState.IsValid)
            return Page();

        var result = await _checkoutFacade.CheckoutAsync(
            userId,
            isGuest ? Input.GuestEmail : null,
            Input.PaymentMethod,
            new PaymentCardDetails(Input.CardNumber, Input.CardExpiry, Input.CardCvc),
            ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Checkout fejlede.");
            return Page();
        }


        if (isGuest)
        {
            return RedirectToPage("/OrderConfirmation",
                new { token = result.ConfirmationToken?.ToString("N") });
        }

        return RedirectToPage("/OrderConfirmation", new { id = result.OrderId });
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private IActionResult BlockedForAdmins()
    {
        _logger.LogWarning("ADMIN blokeret fra checkout: {UserId}", GetCurrentUserId());
        TempData["ErrorMessage"] =
            "Som administrator kan du ikke købe produkter i webshoppen.";
        return RedirectToPage("/Products");
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