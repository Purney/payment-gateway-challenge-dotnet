namespace PaymentGateway.Api.Security;

public static class MerchantAuthenticationDefaults
{
    public const string AuthenticationScheme = "Merchant";
    public const string ApiKeyHeaderName = "X-Api-Key";
    public const string IdempotencyKeyHeaderName = "Idempotency-Key";
    public const string MerchantIdClaimType = "merchant_id";
}
