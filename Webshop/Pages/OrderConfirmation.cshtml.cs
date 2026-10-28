using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;

namespace Webshop.Pages;

// INGEN [Authorize]: gæster bekræfter via det u-gættelige ConfirmationToken (?token=...).
// Loggede-in brugere bruger den autoriserede rute (?id=...) med ownership-tjek.
// De to stier er gensidigt eksklusive: token-lookup rammer KUN gæsteordrer (UserId IS NULL),
// id-lookup kræver login OG at ordren tilhører den indloggede bruger.
public class OrderConfirmationModel : PageModel
{
    private readonly IOrderRepository _orderRepository;

    public OrderConfirmationModel(IOrderRepository orderRepository)
        => _orderRepository = orderRepository;

    public Order? Order { get; private set; }

    public bool IsGuestOrder => Order?.UserId is null;

    public async Task<IActionResult> OnGetAsync(int? id, string? token, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {

            if (!Guid.TryParse(token, out var parsedToken))
                return NotFound();

            var guestOrder = await _orderRepository.GetByConfirmationTokenAsync(parsedToken, ct);
            if (guestOrder is null)
                return NotFound();

            Order = guestOrder;
            return Page();
        }


        if (id is int orderId)
        {
            var userId = GetCurrentUserId();
            if (userId is null)
                return Challenge();           

            var order = await _orderRepository.GetByIdAsync(orderId, ct);
            if (order is null)
                return NotFound();

            if (order.UserId != userId)
                return Forbid();

            Order = order;
            return Page();
        }
        return NotFound();
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}