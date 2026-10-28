using System;
using System.Collections.Generic;
using System.Linq;

namespace Webshop.Model;

public sealed class Order
{
    public int Id { get; init; }


    public int? UserId { get; init; }

    public string? GuestEmail { get; init; }

    public Guid? ConfirmationToken { get; init; }

    public DateTime OrderDate { get; init; }
    public decimal Total { get; init; }
    public OrderStatus Status { get; init; }
    public IReadOnlyList<OrderLine> Lines { get; init; } = Array.Empty<OrderLine>();
    public decimal ComputedTotal => Lines.Sum(l => l.UnitPrice * l.Quantity);
}

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Cancelled = 3
}