using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class UnavailableAcquiringBankClient : IAcquiringBankClient
{
    public Task<AcquiringBankResult> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(AcquiringBankResult.Unavailable());
    }
}
