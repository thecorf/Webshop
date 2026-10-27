using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Webshop.Model;
using Webshop.Model.Repos;
using Webshop.Model.Services.Payments;

namespace Webshop.Model.Services;

public sealed class CheckoutFacade
{
    private readonly CartService _cartService;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly PaymentMethodFactory _paymentFactory;
    private readonly ILogger<CheckoutFacade> _logger;

    public CheckoutFacade(CartService cartService,
                          IProductRepository productRepository,
                          IOrderRepository orderRepository,
                          PaymentMethodFactory paymentFactory,
                          ILogger<CheckoutFacade> logger)
    {
        _cartService = cartService;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _paymentFactory = paymentFactory;
        _logger = logger;
    }

    public IReadOnlyCollection<IPaymentMethod> AvailablePaymentMethods => _paymentFactory.All;

    public async Task<CheckoutResult> CheckoutAsync(int userId,
                                                    string paymentMethodKey,
                                                    PaymentCardDetails? card,
                                                    CancellationToken ct)
    {
        var items = (await _cartService.GetItemsAsync(ct)).ToList();
        if (items.Count == 0)
            return CheckoutResult.Fail("Din kurv er tom.");

        var lines = new List<OrderLine>();
        foreach (var item in items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, ct);
            if (product is null)
            {
                await _cartService.RemoveItemAsync(item.ProductId, ct);
                continue;
            }
            if (product.Stock < item.Quantity)
                return CheckoutResult.Fail(
                    $"'{product.Name}' har kun {product.Stock} på lager. Justér antallet i kurven.");

            lines.Add(new OrderLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
        }

        if (lines.Count == 0)
            return CheckoutResult.Fail("Din kurv er tom.");

        var total = lines.Sum(l => l.LineTotal);

        var method = _paymentFactory.Resolve(paymentMethodKey);
        if (method is null)
            return CheckoutResult.Fail("Ugyldig betalingsmetode.");

        var orderReference = $"ORD-{userId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var payment = await method.PayAsync(
            new PaymentRequest(orderReference, total, card?.Number, card?.Expiry, card?.Cvc),
            ct);

        if (!payment.Success)
        {
            _logger.LogWarning("Betaling fejlede for {Reference}: {Error}",
                orderReference, payment.ErrorMessage);
            return CheckoutResult.Fail(payment.ErrorMessage ?? "Betalingen kunne ikke gennemføres.");
        }

        int orderId;
        try
        {
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Total = total,
                Status = OrderStatus.Paid,
                Lines = lines
            };
            orderId = await _orderRepository.CreateOrderAsync(order, ct);
        }
        catch (OutOfStockException ex)
        {
            _logger.LogWarning("Ordre for {Reference} rullet tilbage: {Reason}",
                orderReference, ex.Message);
            return CheckoutResult.Fail(ex.Message);
        }

        await _cartService.ClearAsync(ct);

        _logger.LogInformation("Ordre {OrderId} oprettet for bruger {UserId}, {Lines} linjer, total {Total:F2}",
            orderId, userId, lines.Count, total);

        return CheckoutResult.Ok(orderId);
    }
}

public sealed record CheckoutResult
{
    public bool Success { get; private init; }
    public int OrderId { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static CheckoutResult Ok(int orderId) => new() { Success = true, OrderId = orderId };
    public static CheckoutResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

public sealed record PaymentCardDetails(string? Number, string? Expiry, string? Cvc);