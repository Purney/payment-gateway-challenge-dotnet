using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : Controller
{
    private readonly ILogger<PaymentsController> _logger;
    private readonly PaymentService _paymentService;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsController(
        ILogger<PaymentsController> logger,
        PaymentService paymentService,
        IPaymentsRepository paymentsRepository)
    {
        _logger = logger;
        _paymentService = paymentService;
        _paymentsRepository = paymentsRepository;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var payment = _paymentsRepository.Get(id);

        if (payment is null)
        {
            _logger.LogInformation("Payment {PaymentId} was not found", id);
            return NotFound();
        }

        _logger.LogInformation("Payment {PaymentId} was retrieved with status {PaymentStatus}", id, payment.Status);

        return Ok(new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        });
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPayment(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessAsync(request, cancellationToken);

        return StatusCode((int)result.StatusCode, result.Payment);
    }
}
