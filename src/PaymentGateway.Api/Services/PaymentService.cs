using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentRequestValidator _paymentRequestValidator;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentService(
        IAcquiringBankClient acquiringBankClient,
        ILogger<PaymentService> logger,
        IPaymentRequestValidator paymentRequestValidator,
        IPaymentsRepository paymentsRepository)
    {
        _acquiringBankClient = acquiringBankClient;
        _logger = logger;
        _paymentRequestValidator = paymentRequestValidator;
        _paymentsRepository = paymentsRepository;
    }

    public async Task<PaymentServiceResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Payment request received for amount {Amount} {Currency}", request.Amount, request.Currency);

        if (!_paymentRequestValidator.IsValid(request))
        {
            _logger.LogWarning("Payment request was rejected by gateway validation");
            return PaymentServiceResult.Rejected();
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

        return PaymentServiceResult.Ok(PaymentResponseMapper.ToPostPaymentResponse(payment));
    }
}
