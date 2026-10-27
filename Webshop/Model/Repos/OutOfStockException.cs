namespace Webshop.Model.Repos;

public sealed class OutOfStockException : Exception
{
    public int ProductId { get; }

    public OutOfStockException(int productId, string productName)
        : base($"'{productName}' er desværre ikke længere på lager i det ønskede antal.")
        => ProductId = productId;
}