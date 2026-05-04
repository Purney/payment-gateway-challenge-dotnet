namespace PaymentGateway.Api.Services;

public sealed class AcquiringBankResult
{
    private AcquiringBankResult(bool isAvailable, bool authorized)
    {
        IsAvailable = isAvailable;
        Authorized = authorized;
    }

    public bool IsAvailable { get; }

    public bool Authorized { get; }

    public static AcquiringBankResult AuthorizedPayment() => new(isAvailable: true, authorized: true);

    public static AcquiringBankResult DeclinedPayment() => new(isAvailable: true, authorized: false);

    public static AcquiringBankResult Unavailable() => new(isAvailable: false, authorized: false);
}
