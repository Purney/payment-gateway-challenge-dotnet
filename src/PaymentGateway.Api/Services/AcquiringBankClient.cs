using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AcquiringBankClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AcquiringBankResult> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AcquiringBank");
        var bankRequest = new AcquiringBankRequest
        {
            CardNumber = request.CardNumber!,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency!,
            Amount = request.Amount,
            Cvv = request.Cvv!
        };

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("/payments", bankRequest, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return AcquiringBankResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AcquiringBankResult.Unavailable();
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return AcquiringBankResult.Unavailable();
        }

        if (!response.IsSuccessStatusCode)
        {
            return AcquiringBankResult.DeclinedPayment();
        }

        var bankResponse = await response.Content.ReadFromJsonAsync<AcquiringBankResponse>(cancellationToken);
        return bankResponse?.Authorized == true
            ? AcquiringBankResult.AuthorizedPayment()
            : AcquiringBankResult.DeclinedPayment();
    }

    private sealed class AcquiringBankRequest
    {
        [JsonPropertyName("card_number")]
        public string CardNumber { get; set; } = string.Empty;

        [JsonPropertyName("expiry_date")]
        public string ExpiryDate { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("cvv")]
        public string Cvv { get; set; } = string.Empty;
    }

    private sealed class AcquiringBankResponse
    {
        [JsonPropertyName("authorized")]
        public bool Authorized { get; set; }
    }
}
