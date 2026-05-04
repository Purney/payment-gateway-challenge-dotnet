using Microsoft.Extensions.Options;

using PaymentGateway.Api.Options;

namespace PaymentGateway.Api.Security;

public sealed class PaymentRequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestProtectionOptions _options;

    public PaymentRequestSizeLimitMiddleware(RequestDelegate next, IOptions<RequestProtectionOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPaymentPost(context) && context.Request.ContentLength > _options.MaxPaymentRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await _next(context);
    }

    private static bool IsPaymentPost(HttpContext context)
    {
        return HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/api/Payments", StringComparison.OrdinalIgnoreCase);
    }
}
