using System.Threading;
using System.Threading.Tasks;

namespace Webshop.Model.Services.Payments;

public sealed class InvoicePaymentMethod : IPaymentMethod
{
    public string Key => "invoice";
    public string DisplayName => "Faktura (14 dage)";

    public Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct)
        => Task.FromResult(PaymentResult.Ok($"SIM-INV-{Guid.NewGuid():N}"));
}