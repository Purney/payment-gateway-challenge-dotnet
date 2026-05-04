using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Security;

public sealed class MerchantAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IOptionsMonitor<MerchantAuthenticationOptions> _merchantOptions;

    public MerchantAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<MerchantAuthenticationOptions> merchantOptions)
        : base(options, logger, encoder)
    {
        _merchantOptions = merchantOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(MerchantAuthenticationDefaults.ApiKeyHeaderName, out var apiKeys))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header is missing."));
        }

        var apiKey = apiKeys.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key header is empty."));
        }

        var merchant = _merchantOptions.CurrentValue.Merchants
            .FirstOrDefault(merchant => merchant.ApiKey == apiKey);
        if (merchant is null || string.IsNullOrWhiteSpace(merchant.MerchantId))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key is invalid."));
        }

        var claims = new[]
        {
            new Claim(MerchantAuthenticationDefaults.MerchantIdClaimType, merchant.MerchantId)
        };
        var identity = new ClaimsIdentity(claims, MerchantAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, MerchantAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
