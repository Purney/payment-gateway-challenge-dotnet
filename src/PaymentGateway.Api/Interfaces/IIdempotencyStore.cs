using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Interfaces;

public interface IIdempotencyStore
{
    IdempotencyRecord? Get(string merchantId, string key);

    void Add(IdempotencyRecord record);
}
