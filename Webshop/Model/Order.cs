using System;
using System.Collections.Generic;

namespace Webshop.Model;

public sealed class Order
{
    public int Id { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public IReadOnlyList<OrderLine> Lines { get; init; } = Array.Empty<OrderLine>();
}
