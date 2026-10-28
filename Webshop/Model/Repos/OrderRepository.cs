using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Webshop.Model;

namespace Webshop.Model.Repos;

public class OrderRepository : SqlRepositoryBase, IOrderRepository
{
    public OrderRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<int> CreateOrderAsync(Order order, CancellationToken ct = default)
        => InTransactionAsync<int>(async (conn, tx) =>
        {
            // 1. Ordre-hoved: Total skrives som snapshot i SAMME transaktion som linjerne,
            //    så de to aldrig kan divergere ved delvis fejl.
            //    UserId er nu NULLABLE (gæsteordrer) — GuestEmail kræves i stedet.
            //    ConfirmationToken bruges til gæstens adgang til ordrebekræftelsen.
            const string orderSql = @"INSERT INTO Orders (UserId, GuestEmail, OrderDate, Total, Status, ConfirmationToken)
                                      VALUES (@userId, @guestEmail, @orderDate, @total, @status, @confirmationToken);
                                      SELECT CAST(SCOPE_IDENTITY() AS INT);";

            int orderId;
            await using (var cmd = new SqlCommand(orderSql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@userId", (object?)order.UserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@guestEmail", (object?)order.GuestEmail ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@orderDate", order.OrderDate);
                cmd.Parameters.AddWithValue("@total", order.Total);
                cmd.Parameters.AddWithValue("@status", (int)order.Status);
                cmd.Parameters.AddWithValue("@confirmationToken", (object?)order.ConfirmationToken ?? DBNull.Value);
                orderId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }

            // 2. Linjerne + BETINGET lager-nedskrivning (optimistisk samtidighedskontrol):
            //    "Stock >= @quantity" i WHERE sikrer at to samtidige købere ikke
            //    oversælger det sidste produkt. 0 rækker = udsolgt = rollback.
            foreach (var line in order.Lines)
            {
                const string lineSql = @"INSERT INTO OrderLines
                                             (OrderId, ProductId, ProductName, Quantity, UnitPrice)
                                         VALUES (@orderId, @productId, @productName, @quantity, @unitPrice)";
                await using (var lineCmd = new SqlCommand(lineSql, conn, tx))
                {
                    lineCmd.Parameters.AddWithValue("@orderId", orderId);
                    lineCmd.Parameters.AddWithValue("@productId", line.ProductId);
                    lineCmd.Parameters.AddWithValue("@productName", line.ProductName);
                    lineCmd.Parameters.AddWithValue("@quantity", line.Quantity);
                    lineCmd.Parameters.AddWithValue("@unitPrice", line.UnitPrice);
                    await lineCmd.ExecuteNonQueryAsync(ct);
                }

                const string stockSql = @"UPDATE Products
                                          SET Stock = Stock - @quantity
                                          WHERE Id = @productId AND Stock >= @quantity";
                int rowsAffected;
                await using (var stockCmd = new SqlCommand(stockSql, conn, tx))
                {
                    stockCmd.Parameters.AddWithValue("@quantity", line.Quantity);
                    stockCmd.Parameters.AddWithValue("@productId", line.ProductId);
                    rowsAffected = await stockCmd.ExecuteNonQueryAsync(ct);
                }

                if (rowsAffected == 0)
                    throw new OutOfStockException(line.ProductId, line.ProductName);
            }

            return orderId;
        }, ct);

    public async Task<Order?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string headerSql = @"SELECT Id, UserId, GuestEmail, OrderDate, Total, Status, ConfirmationToken
                                   FROM Orders WHERE Id = @id";

        var headers = await QueryAsync(headerSql, MapHeader,
            p => p.AddWithValue("@id", id), ct);

        if (headers.Count == 0) return null;

        var header = headers[0];
        return new Order
        {
            Id = header.Id,
            UserId = header.UserId,
            GuestEmail = header.GuestEmail,
            OrderDate = header.OrderDate,
            Total = header.Total,
            Status = header.Status,
            ConfirmationToken = header.ConfirmationToken,
            Lines = await LoadLinesAsync(id, ct)
        };
    }

    public async Task<List<Order>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        // UserID-filteret betyder automatisk, at gæsteordrer (UserId = NULL)
        // ALDRIG kan ramme en login-brugers ordrehistorik — NULL =/= noget id.
        const string sql = @"SELECT Id, UserId, GuestEmail, OrderDate, Total, Status, ConfirmationToken
                             FROM Orders
                             WHERE UserId = @userId
                             ORDER BY OrderDate DESC";

        var headers = await QueryAsync(sql, MapHeader,
            p => p.AddWithValue("@userId", userId), ct);

        var orders = new List<Order>();
        foreach (var header in headers)
        {
            orders.Add(new Order
            {
                Id = header.Id,
                UserId = header.UserId,
                GuestEmail = header.GuestEmail,
                OrderDate = header.OrderDate,
                Total = header.Total,
                Status = header.Status,
                ConfirmationToken = header.ConfirmationToken,
                Lines = await LoadLinesAsync(header.Id, ct)
            });
        }
        return orders;
    }

    /// <summary>
    /// Gæsteordrer slås KUN op via det u-gættelige confirmationToken — aldrig
    /// via det sekventielle Id, som ellers kunne udnyttes til IDOR/enumeration.
    /// </summary>
    public async Task<Order?> GetByConfirmationTokenAsync(Guid token, CancellationToken ct = default)
    {
        const string headerSql = @"SELECT Id, UserId, GuestEmail, OrderDate, Total, Status, ConfirmationToken
                                   FROM Orders
                                   WHERE ConfirmationToken = @token AND UserId IS NULL";

        var headers = await QueryAsync(headerSql, MapHeader,
            p => p.AddWithValue("@token", token), ct);

        if (headers.Count == 0) return null;

        var header = headers[0];
        return new Order
        {
            Id = header.Id,
            UserId = header.UserId,
            GuestEmail = header.GuestEmail,
            OrderDate = header.OrderDate,
            Total = header.Total,
            Status = header.Status,
            ConfirmationToken = header.ConfirmationToken,
            Lines = await LoadLinesAsync(header.Id, ct)
        };
    }

    private async Task<IReadOnlyList<OrderLine>> LoadLinesAsync(int orderId, CancellationToken ct)
    {
        const string sql = @"SELECT OrderId, ProductId, ProductName, Quantity, UnitPrice
                             FROM OrderLines
                             WHERE OrderId = @orderId
                             ORDER BY Id";

        return await QueryAsync(sql, MapLine,
            p => p.AddWithValue("@orderId", orderId), ct);
    }

    private static Order MapHeader(SqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        // Nullable efter gæste-flowet:
        UserId = r.IsDBNull(r.GetOrdinal("UserId"))
            ? null
            : r.GetInt32(r.GetOrdinal("UserId")),
        GuestEmail = r.IsDBNull(r.GetOrdinal("GuestEmail"))
            ? null
            : r.GetString(r.GetOrdinal("GuestEmail")),
        OrderDate = r.GetDateTime(r.GetOrdinal("OrderDate")),
        Total = r.GetDecimal(r.GetOrdinal("Total")),
        Status = (OrderStatus)r.GetInt32(r.GetOrdinal("Status")),
        ConfirmationToken = r.IsDBNull(r.GetOrdinal("ConfirmationToken"))
            ? null
            : r.GetGuid(r.GetOrdinal("ConfirmationToken"))
    };

    private static OrderLine MapLine(SqlDataReader r) => new()
    {
        OrderId = r.GetInt32(r.GetOrdinal("OrderId")),
        ProductId = r.GetInt32(r.GetOrdinal("ProductId")),
        ProductName = r.GetString(r.GetOrdinal("ProductName")),
        Quantity = r.GetInt32(r.GetOrdinal("Quantity")),
        UnitPrice = r.GetDecimal(r.GetOrdinal("UnitPrice"))
    };
}