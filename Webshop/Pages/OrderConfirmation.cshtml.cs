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
public class OrderConfirmationModel : PageModel
{
    private readonly IOrderRepository _orderRepository;

    public OrderConfirmationModel(IOrderRepository orderRepository)
        => _orderRepository = orderRepository;

    public Order? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(id, ct);
        if (order is null)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (order.UserId.ToString() != userId)
            return Forbid();

        Order = order;
        return Page();
    }
}