namespace PaymentGateway.Api.Options;

public sealed class RequestProtectionOptions
{
    public int MaxPaymentRequestBytes { get; set; } = 4096;

    public int RateLimitPermitLimit { get; set; } = 60;

    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
}
