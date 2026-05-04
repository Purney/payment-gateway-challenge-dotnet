using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Mappers;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Security;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PaymentsController : Controller
{
    private readonly ILogger<PaymentsController> _logger;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsController(
        ILogger<PaymentsController> logger,
        IPaymentService paymentService,
        IPaymentsRepository paymentsRepository)
    {
        _logger = logger;
        _paymentService = paymentService;
        _paymentsRepository = paymentsRepository;
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GetPaymentResponse> GetPayment(Guid id)
    {
        var merchantId = GetMerchantId();
        var payment = _paymentsRepository.Get(id, merchantId);

        if (payment is null)
        {
            _logger.LogInformation("Payment {PaymentId} was not found", id);
            return NotFound();
        }

        _logger.LogInformation("Payment {PaymentId} was retrieved with status {PaymentStatus}", id, payment.Status);

        return Ok(PaymentResponseMapper.ToGetPaymentResponse(payment));
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPayment(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentService.ProcessAsync(request, GetMerchantId(), cancellationToken);

        return StatusCode((int)result.StatusCode, result.Payment);
    }

    private string GetMerchantId()
    {
        return User.Claims.Single(claim => claim.Type == MerchantAuthenticationDefaults.MerchantIdClaimType).Value;
    }
}
