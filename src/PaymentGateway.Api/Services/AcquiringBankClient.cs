using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(IHttpClientFactory httpClientFactory, ILogger<AcquiringBankClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
            _logger.LogInformation("Sending payment authorization request to acquiring bank");
            response = await client.PostAsJsonAsync("/payments", bankRequest, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Acquiring bank request failed");
            return AcquiringBankResult.Unavailable();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Acquiring bank request timed out");
            return AcquiringBankResult.Unavailable();
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Acquiring bank returned {StatusCode}", response.StatusCode);
            return AcquiringBankResult.Unavailable();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Acquiring bank declined request with HTTP status {StatusCode}", response.StatusCode);
            return AcquiringBankResult.DeclinedPayment();
        }

        var bankResponse = await response.Content.ReadFromJsonAsync<AcquiringBankResponse>(cancellationToken);
        _logger.LogInformation("Acquiring bank authorization completed with authorized={Authorized}", bankResponse?.Authorized);

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
