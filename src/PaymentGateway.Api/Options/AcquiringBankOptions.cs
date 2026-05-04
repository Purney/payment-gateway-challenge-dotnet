namespace PaymentGateway.Api.Options;

public sealed class AcquiringBankOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";

    public int TimeoutSeconds { get; set; } = 5;
}
