using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class PaymentRequestValidator : IPaymentRequestValidator
{
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "GBP",
        "USD",
        "EUR"
    };

    public bool IsValid(PostPaymentRequest request)
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
