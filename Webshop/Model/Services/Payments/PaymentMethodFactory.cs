using System.Collections.Generic;
using System.Linq;

namespace Webshop.Model.Services.Payments;

public sealed class PaymentMethodFactory
{
    private readonly IReadOnlyDictionary<string, IPaymentMethod> _methods;

    public PaymentMethodFactory(IEnumerable<IPaymentMethod> methods)
    {
        _methods = methods.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IPaymentMethod> All => _methods.Values.ToList();

    public IPaymentMethod? Resolve(string? key)
        => key is not null && _methods.TryGetValue(key, out var method) ? method : null;
}