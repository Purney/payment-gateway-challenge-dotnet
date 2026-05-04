using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentRequestValidator _paymentRequestValidator;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentService(
        IAcquiringBankClient acquiringBankClient,
        IIdempotencyStore idempotencyStore,
        ILogger<PaymentService> logger,
        IPaymentRequestValidator paymentRequestValidator,
        IPaymentsRepository paymentsRepository)
    {
        _acquiringBankClient = acquiringBankClient;
        _idempotencyStore = idempotencyStore;
        _logger = logger;
        _paymentRequestValidator = paymentRequestValidator;
        _paymentsRepository = paymentsRepository;
    }

    public async Task<PaymentServiceResult> ProcessAsync(
        PostPaymentRequest request,
        string merchantId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Payment request received for amount {Amount} {Currency}", request.Amount, request.Currency);

        var requestHash = CreateRequestHash(request);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingRecord = _idempotencyStore.Get(merchantId, idempotencyKey);
            if (existingRecord is not null)
            {
                if (existingRecord.RequestHash != requestHash)
                {
                    _logger.LogWarning("Idempotency key was reused with a different request for merchant {MerchantId}", merchantId);
                    return PaymentServiceResult.IdempotencyConflict();
                }

                _logger.LogInformation("Idempotent payment response replayed for merchant {MerchantId}", merchantId);
                return PaymentServiceResult.FromSnapshot(existingRecord.Result);
            }
        }

        if (!_paymentRequestValidator.IsValid(request))
        {
            _logger.LogWarning("Payment request was rejected by gateway validation");
            var result = PaymentServiceResult.Rejected();
            StoreIdempotencyRecord(merchantId, idempotencyKey, requestHash, result);
            return result;
        }

        var bankResult = await _acquiringBankClient.AuthorizeAsync(request, cancellationToken);
        if (!bankResult.IsAvailable)
        {
            _logger.LogWarning("Payment authorization failed because the acquiring bank was unavailable");
            return PaymentServiceResult.BankUnavailable();
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
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

        var completedResult = PaymentServiceResult.Ok(PaymentResponseMapper.ToPostPaymentResponse(payment));
        StoreIdempotencyRecord(merchantId, idempotencyKey, requestHash, completedResult);

        return completedResult;
    }

    private static string CreateRequestHash(PostPaymentRequest request)
    {
        var canonicalRequest = JsonSerializer.Serialize(new
        {
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            Currency = request.Currency?.ToUpperInvariant(),
            request.Amount,
            request.Cvv
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest));
        return Convert.ToHexString(hash);
    }

    private void StoreIdempotencyRecord(
        string merchantId,
        string? idempotencyKey,
        string requestHash,
        PaymentServiceResult result)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || result.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return;
        }

        _idempotencyStore.Add(new IdempotencyRecord
        {
            MerchantId = merchantId,
            Key = idempotencyKey,
            RequestHash = requestHash,
            Result = result.ToSnapshot()
        });
    }
}
