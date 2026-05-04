using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR"
    };

    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentService(
        IAcquiringBankClient acquiringBankClient,
        ILogger<PaymentService> logger,
        IPaymentsRepository paymentsRepository)
    {
        _acquiringBankClient = acquiringBankClient;
        _logger = logger;
        _paymentsRepository = paymentsRepository;
    }

    public async Task<PaymentServiceResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Payment request received for amount {Amount} {Currency}", request.Amount, request.Currency);

        if (!IsValid(request))
        {
            _logger.LogWarning("Payment request was rejected by gateway validation");
            return PaymentServiceResult.Rejected();
        }

        var bankResult = await _acquiringBankClient.AuthorizeAsync(request, cancellationToken);
        if (!bankResult.IsAvailable)
        {
            _logger.LogWarning("Payment authorization failed because the acquiring bank was unavailable");
            return PaymentServiceResult.BankUnavailable();
        }

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = bankResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = int.Parse(request.CardNumber![^4..]),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency!.ToUpperInvariant(),
            Amount = request.Amount
        };

        _paymentsRepository.Add(payment);
        _logger.LogInformation(
            "Payment {PaymentId} was stored with status {PaymentStatus}",
            payment.Id,
            payment.Status);

        return PaymentServiceResult.Ok(payment);
    }

    private static bool IsValid(PostPaymentRequest request)
    {
        return IsValidCardNumber(request.CardNumber)
            && IsValidExpiry(request.ExpiryMonth, request.ExpiryYear)
            && IsValidCurrency(request.Currency)
            && request.Amount > 0
            && IsValidCvv(request.Cvv);
    }

    private static bool IsValidCardNumber(string? cardNumber)
    {
        return !string.IsNullOrWhiteSpace(cardNumber)
            && cardNumber.Length is >= 14 and <= 19
            && cardNumber.All(char.IsDigit);
    }

    private static bool IsValidExpiry(int month, int year)
    {
        if (month is < 1 or > 12)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        return year > now.Year || year == now.Year && month >= now.Month;
    }

    private static bool IsValidCurrency(string? currency)
    {
        return !string.IsNullOrWhiteSpace(currency)
            && currency.Length == 3
            && SupportedCurrencies.Contains(currency);
    }

    private static bool IsValidCvv(string? cvv)
    {
        return !string.IsNullOrWhiteSpace(cvv)
            && cvv.Length is >= 3 and <= 4
            && cvv.All(char.IsDigit);
    }
}
