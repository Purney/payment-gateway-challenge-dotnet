using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class AcquiringBankClientTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AuthorizeAsync_WhenBankReturnsServerError_ReturnsUnavailable()
    {
        using var bank = FakeBank.ServerError();
        var client = CreateClient(bank);

        var result = await client.AuthorizeAsync(ValidPaymentRequest(), CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal(1, bank.RequestCount);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenBankReturnsMalformedSuccessResponse_ReturnsUnavailable()
    {
        using var bank = FakeBank.MalformedSuccess();
        var client = CreateClient(bank);

        var result = await client.AuthorizeAsync(ValidPaymentRequest(), CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal(1, bank.RequestCount);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenNetworkFails_DoesNotRetryAndReturnsUnavailable()
    {
        using var bank = FakeBank.Throws();
        var client = CreateClient(bank);

        var result = await client.AuthorizeAsync(ValidPaymentRequest(), CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal(1, bank.RequestCount);
    }

    private static AcquiringBankClient CreateClient(FakeBank bank)
    {
        return new AcquiringBankClient(
            new StubHttpClientFactory(bank.Client),
            NullLogger<AcquiringBankClient>.Instance);
    }

    private static PostPaymentRequest ValidPaymentRequest()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248871",
            ExpiryMonth = 12,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
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
        private readonly Func<HttpResponseMessage> _responseFactory;

        private FakeBank(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
            Client = new HttpClient(this)
            {
                BaseAddress = new Uri("http://bank-simulator")
            };
        }

        public HttpClient Client { get; }

        public int RequestCount { get; private set; }

        public static FakeBank ServerError()
        {
            return new FakeBank(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        public static FakeBank MalformedSuccess()
        {
            return new FakeBank(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json")
            });
        }

        public static FakeBank Throws()
        {
            return new FakeBank(() => throw new HttpRequestException("Connection failed."));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(_responseFactory());
        }

        public new void Dispose()
        {
            Client.Dispose();
            base.Dispose();
        }
    }
}
