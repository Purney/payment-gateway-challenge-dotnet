using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IAcquiringBankClient
{
    Task<AcquiringBankResult> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken);
}
