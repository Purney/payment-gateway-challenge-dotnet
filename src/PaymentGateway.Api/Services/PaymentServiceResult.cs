using System.Net;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public sealed class PaymentServiceResult
{
    private PaymentServiceResult(HttpStatusCode statusCode, PostPaymentResponse? payment)
    {
        StatusCode = statusCode;
        Payment = payment;
    }

    public HttpStatusCode StatusCode { get; }

    public PostPaymentResponse? Payment { get; }

    public static PaymentServiceResult Ok(PostPaymentResponse payment) => new(HttpStatusCode.OK, payment);

    public static PaymentServiceResult FromSnapshot(PaymentServiceResultSnapshot snapshot) => new(
        (HttpStatusCode)snapshot.StatusCode,
        snapshot.Payment);

    public PaymentServiceResultSnapshot ToSnapshot()
    {
        return new PaymentServiceResultSnapshot
        {
            StatusCode = (int)StatusCode,
            Payment = Payment
        };
    }

    public static PaymentServiceResult Rejected() => new(
        HttpStatusCode.BadRequest,
        new PostPaymentResponse { Status = PaymentStatus.Rejected });

    public static PaymentServiceResult BankUnavailable() => new(HttpStatusCode.ServiceUnavailable, null);

    public static PaymentServiceResult IdempotencyConflict() => new(HttpStatusCode.Conflict, null);
}
