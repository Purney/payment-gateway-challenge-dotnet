using System.Collections.Concurrent;

using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<(string MerchantId, string Key), IdempotencyRecord> _records = new();

    public IdempotencyRecord? Get(string merchantId, string key)
    {
        return _records.GetValueOrDefault((merchantId, key));
    }

    public void Add(IdempotencyRecord record)
    {
        _records[(record.MerchantId, record.Key)] = record;
    }
}
