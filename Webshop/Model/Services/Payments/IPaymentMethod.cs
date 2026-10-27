using System.Threading;
using System.Threading.Tasks;

namespace Webshop.Model.Services.Payments;

public interface IPaymentMethod
{
    string Key { get; }
    string DisplayName { get; }
    Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct);
}

public sealed record PaymentRequest(
    string OrderReference,
    decimal Amount,
    string? CardNumber = null,
    string? Expiry = null,
    string? Cvc = null);

public sealed record PaymentResult(bool Success, string? TransactionId, string? ErrorMessage)
{
    public static PaymentResult Ok(string txId) => new(true, txId, null);
    public static PaymentResult Fail(string error) => new(false, null, error);
}