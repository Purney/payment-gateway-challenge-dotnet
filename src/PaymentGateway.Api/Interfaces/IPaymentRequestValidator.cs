using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Interfaces;

public interface IPaymentRequestValidator
{
    bool IsValid(PostPaymentRequest request);
}
