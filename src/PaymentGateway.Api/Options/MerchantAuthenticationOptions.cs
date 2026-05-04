namespace PaymentGateway.Api.Options;

public sealed class MerchantAuthenticationOptions
{
    public List<MerchantCredential> Merchants { get; set; } = new();
}

public sealed class MerchantCredential
{
    public string MerchantId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
