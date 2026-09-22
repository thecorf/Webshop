namespace Webshop.Model;

public sealed class OrderLine
{
    public int ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
