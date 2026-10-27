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
public class OrderHistoryModel : PageModel
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderHistoryModel> _logger;

    public OrderHistoryModel(IOrderRepository orderRepository,
                             ILogger<OrderHistoryModel> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public List<Order> Orders { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Challenge();   // Cookie udløbet — redirect til login

        Orders = await _orderRepository.GetByUserIdAsync(userId.Value, ct);

        // Log når nogen taster ind på ordrehistorik (god til overvågning)
        _logger.LogInformation("Bruger {UserId} læste ordrehistorik ({Count} ordrer)",
            userId.Value, Orders.Count);

        return Page();
    }

    private int? GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : null;
    }
}