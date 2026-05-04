using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Interfaces;

public interface IPaymentService
{
    Task<PaymentServiceResult> ProcessAsync(PostPaymentRequest request, string merchantId, CancellationToken cancellationToken);
}
