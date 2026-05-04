using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PaymentGateway.Api.Security;

public sealed class MerchantAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public MerchantAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(MerchantAuthenticationDefaults.MerchantIdHeaderName, out var merchantIds))
        {
            return Task.FromResult(AuthenticateResult.Fail("Merchant id header is missing."));
        }

        var merchantId = merchantIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Merchant id header is empty."));
        }

        var claims = new[]
        {
            new Claim(MerchantAuthenticationDefaults.MerchantIdClaimType, merchantId)
        };
        var identity = new ClaimsIdentity(claims, MerchantAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, MerchantAuthenticationDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
