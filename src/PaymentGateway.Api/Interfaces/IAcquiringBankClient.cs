using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Interfaces;

public interface IAcquiringBankClient
{
    Task<AcquiringBankResult> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken);
}
