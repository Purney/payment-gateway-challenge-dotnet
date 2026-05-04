using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Security;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PostPayment_WhenBankAuthorizes_ReturnsAuthorizedPaymentAndStoresIt()
    {
        using var bank = FakeBank.Authorizes();
        using var client = CreateClient(bank);
        var request = ValidPaymentRequest(cardNumber: "2222405343248871");

        var response = await client.PostAsJsonAsync("/api/Payments", request, SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await ReadJsonAsync(response);
        var id = AssertGuid(payment, "id");
        Assert.Equal("Authorized", payment["status"]?.GetValue<string>());
        Assert.Equal(8871, payment["cardNumberLastFour"]?.GetValue<int>());
        Assert.DoesNotContain("2222405343248871", payment.ToJsonString());
        Assert.DoesNotContain("123", payment.ToJsonString());
        Assert.Equal(1, bank.RequestCount);

        var getResponse = await client.GetAsync($"/api/Payments/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var storedPayment = await ReadJsonAsync(getResponse);
        Assert.Equal(id, AssertGuid(storedPayment, "id"));
        Assert.Equal("Authorized", storedPayment["status"]?.GetValue<string>());
        Assert.Equal(8871, storedPayment["cardNumberLastFour"]?.GetValue<int>());
    }

    [Fact]
    public async Task PostPayment_WhenBankDeclines_ReturnsDeclinedPaymentAndStoresIt()
    {
        using var bank = FakeBank.Declines();
        using var client = CreateClient(bank);
        var request = ValidPaymentRequest(cardNumber: "2222405343248872");

        var response = await client.PostAsJsonAsync("/api/Payments", request, SerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payment = await ReadJsonAsync(response);
        var id = AssertGuid(payment, "id");
        Assert.Equal("Declined", payment["status"]?.GetValue<string>());
        Assert.Equal(8872, payment["cardNumberLastFour"]?.GetValue<int>());
        Assert.Equal(1, bank.RequestCount);

        var getResponse = await client.GetAsync($"/api/Payments/{id}");
        var storedPayment = await ReadJsonAsync(getResponse);
        Assert.Equal("Declined", storedPayment["status"]?.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(RejectedPaymentRequests))]
    public async Task PostPayment_WhenGatewayValidationFails_ReturnsRejectedWithoutCallingBank(object request)
    {
        using var bank = FakeBank.Authorizes();
        using var client = CreateClient(bank);

        var response = await client.PostAsJsonAsync("/api/Payments", request, SerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bank.RequestCount);

        var rejection = await ReadJsonAsync(response);
        Assert.Equal("Rejected", rejection["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task PostPayment_WhenBankIsUnavailable_ReturnsServiceUnavailableAndDoesNotStorePayment()
    {
        using var bank = FakeBank.Unavailable();
        using var client = CreateClient(bank);
        var request = ValidPaymentRequest(cardNumber: "2222405343248870");

        var response = await client.PostAsJsonAsync("/api/Payments", request, SerializerOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, bank.RequestCount);
    }

    [Fact]
    public async Task GetPayment_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        using var bank = FakeBank.Authorizes();
        using var client = CreateClient(bank);

        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_WhenMerchantIdIsMissing_ReturnsUnauthorized()
    {
        using var bank = FakeBank.Authorizes();
        using var client = CreateClient(bank, merchantId: null);

        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_WhenPaymentBelongsToAnotherMerchant_ReturnsNotFound()
    {
        using var bank = FakeBank.Authorizes();
        using var client = CreateClient(bank, merchantId: "merchant-a");

        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidPaymentRequest(), SerializerOptions);
        var payment = await ReadJsonAsync(postResponse);
        var paymentId = AssertGuid(payment, "id");

        client.DefaultRequestHeaders.Remove(MerchantAuthenticationDefaults.MerchantIdHeaderName);
        client.DefaultRequestHeaders.Add(MerchantAuthenticationDefaults.MerchantIdHeaderName, "merchant-b");

        var getResponse = await client.GetAsync($"/api/Payments/{paymentId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    public static TheoryData<object> RejectedPaymentRequests()
    {
        var expiredYear = DateTime.UtcNow.Year - 1;
        var unsupportedCurrency = "JPY";

        return new TheoryData<object>
        {
            ValidPaymentRequest(cardNumber: null),
            ValidPaymentRequest(cardNumber: "1234567890123"),
            ValidPaymentRequest(cardNumber: "12345678901234567890"),
            ValidPaymentRequest(cardNumber: "222240534324887X"),
            ValidPaymentRequest(expiryMonth: 0),
            ValidPaymentRequest(expiryMonth: 13),
            ValidPaymentRequest(expiryYear: expiredYear),
            ValidPaymentRequest(currency: null),
            ValidPaymentRequest(currency: "GB"),
            ValidPaymentRequest(currency: unsupportedCurrency),
            ValidPaymentRequest(amount: 0),
            ValidPaymentRequest(cvv: null),
            ValidPaymentRequest(cvv: "12"),
            ValidPaymentRequest(cvv: "12345"),
            ValidPaymentRequest(cvv: "12A")
        };
    }

    private static HttpClient CreateClient(FakeBank bank, string? merchantId = "merchant-a")
    {
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        var client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(bank.Client));
            }))
            .CreateClient();

        if (merchantId is not null)
        {
            client.DefaultRequestHeaders.Add(MerchantAuthenticationDefaults.MerchantIdHeaderName, merchantId);
        }

        return client;
    }

    private static object ValidPaymentRequest(
        string? cardNumber = "2222405343248871",
        int expiryMonth = 12,
        int? expiryYear = null,
        string? currency = "GBP",
        int amount = 100,
        string? cvv = "123")
    {
        return new
        {
            card_number = cardNumber,
            expiry_month = expiryMonth,
            expiry_year = expiryYear ?? DateTime.UtcNow.Year + 1,
            currency,
            amount,
            cvv
        };
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(json) ?? throw new InvalidOperationException("Response body was not valid JSON.");
    }

    private static Guid AssertGuid(JsonNode json, string propertyName)
    {
        var value = json[propertyName]?.GetValue<string>();
        Assert.True(Guid.TryParse(value, out var id), $"Expected '{propertyName}' to contain a GUID.");
        return id;
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeBank : HttpMessageHandler, IDisposable
    {
        private readonly HttpStatusCode _statusCode;
        private readonly bool _authorized;

        private FakeBank(HttpStatusCode statusCode, bool authorized)
        {
            _statusCode = statusCode;
            _authorized = authorized;
            Client = new HttpClient(this)
            {
                BaseAddress = new Uri("http://bank-simulator")
            };
        }

        public HttpClient Client { get; }

        public int RequestCount { get; private set; }

        public static FakeBank Authorizes() => new(HttpStatusCode.OK, authorized: true);

        public static FakeBank Declines() => new(HttpStatusCode.OK, authorized: false);

        public static FakeBank Unavailable() => new(HttpStatusCode.ServiceUnavailable, authorized: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            var response = new HttpResponseMessage(_statusCode);
            if (_statusCode == HttpStatusCode.OK)
            {
                response.Content = JsonContent.Create(new
                {
                    authorized = _authorized,
                    authorization_code = Guid.NewGuid().ToString()
                }, options: SerializerOptions);
            }

            return Task.FromResult(response);
        }

        public new void Dispose()
        {
            Client.Dispose();
            base.Dispose();
        }
    }
}
