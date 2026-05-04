using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly List<IdempotencyRecord> _records = new();

    public IdempotencyRecord? Get(string merchantId, string key)
    {
        return _records.FirstOrDefault(record => record.MerchantId == merchantId && record.Key == key);
    }

    public void Add(IdempotencyRecord record)
    {
        _records.Add(record);
    }
}
