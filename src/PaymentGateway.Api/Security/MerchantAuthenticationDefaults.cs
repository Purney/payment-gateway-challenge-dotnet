namespace PaymentGateway.Api.Security;

public static class MerchantAuthenticationDefaults
{
    public const string AuthenticationScheme = "Merchant";
    public const string IdempotencyKeyHeaderName = "Idempotency-Key";
    public const string MerchantIdClaimType = "merchant_id";
    public const string MerchantIdHeaderName = "X-Merchant-Id";
}
