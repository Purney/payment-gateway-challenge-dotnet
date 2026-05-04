using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Models;

public sealed class IdempotencyRecord
{
    public string MerchantId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public PaymentServiceResultSnapshot Result { get; set; } = new();
}

public sealed class PaymentServiceResultSnapshot
{
    public int StatusCode { get; set; }
    public PostPaymentResponse? Payment { get; set; }
}
