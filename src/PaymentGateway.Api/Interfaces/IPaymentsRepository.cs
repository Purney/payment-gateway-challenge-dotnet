using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Interfaces;

public interface IPaymentsRepository
{
    void Add(Payment payment);

    Payment? Get(Guid id, string merchantId);
}
