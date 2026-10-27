using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model;
using Webshop.Model.Repos;

namespace Webshop.Pages;

[Authorize]
public class OrderDetailModel : PageModel
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderDetailModel> _logger;

    public OrderDetailModel(IOrderRepository orderRepository,
                            ILogger<OrderDetailModel> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public Order? Order { get; private set; }
    public bool NotFound { get; private set; }
    public bool Forbidden { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct);

        // IDOR-forsvar: ordren må KUN vises for ejeren (samme princip som OrderConfirmation)
        if (order is null)
        {
            NotFound = true;
            return Page();
        }

        var userId = GetCurrentUserId();
        if (userId is null || order.UserId != userId.Value)
        {
            Forbidden = true;
            _logger.LogWarning("Bruger {CurrentUserId} prøvede at se ordre {OrderId} (ejerskab: {OrderUserId})",
                userId, id, order.UserId);
            return Page();
        }

        Order = order;
        _logger.LogInformation("Bruger {UserId} åbnede ordre {OrderId}", userId, id);
        return Page();
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}