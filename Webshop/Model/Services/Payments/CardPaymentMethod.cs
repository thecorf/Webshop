using System.Linq;
using Microsoft.Extensions.Logging;

namespace Webshop.Model.Services.Payments;

public sealed class CardPaymentMethod : IPaymentMethod
{
    private readonly ILogger<CardPaymentMethod> _logger;

    public CardPaymentMethod(ILogger<CardPaymentMethod> logger) => _logger = logger;

    public string Key => "card";
    public string DisplayName => "Betalingskort";

    public Task<PaymentResult> PayAsync(PaymentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumber) ||
            string.IsNullOrWhiteSpace(request.Expiry) ||
            string.IsNullOrWhiteSpace(request.Cvc))
        {
            return Task.FromResult(PaymentResult.Fail("Udfyld kortnummer, udløb og CVC."));
        }

        var digits = new string(request.CardNumber.Where(char.IsDigit).ToArray());

        if (!IsLuhnValid(digits))
            return Task.FromResult(PaymentResult.Fail("Kortnummeret er ugyldigt."));

        if (!TryParseExpiry(request.Expiry, out var expiry) || expiry < DateTime.UtcNow)
            return Task.FromResult(PaymentResult.Fail("Kortet er udløbet."));

        var masked = digits.Length >= 4 ? $"****{digits[^4..]}" : "****";
        _logger.LogInformation("Simuleret kortbetaling for {Reference}: {Masked}, beløb {Amount}",
            request.OrderReference, masked, request.Amount);

        return Task.FromResult(PaymentResult.Ok($"SIM-{Guid.NewGuid():N}"));
    }

    private static bool IsLuhnValid(string digits)
    {
        if (digits.Length < 12) return false;
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static bool TryParseExpiry(string expiry, out DateTime expiresAt)
    {
        expiresAt = default;
        var parts = expiry.Split('/', '-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var month) || month is < 1 or > 12) return false;
        if (!int.TryParse(parts[1], out var year)) return false;
        year += year < 100 ? 2000 : 0;
        expiresAt = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1);
        return true;
    }
}