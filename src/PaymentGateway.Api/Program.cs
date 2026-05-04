using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication;

using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Options;
using PaymentGateway.Api.Security;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var acquiringBankOptions = builder.Configuration.GetSection("AcquiringBank").Get<AcquiringBankOptions>()
    ?? new AcquiringBankOptions();

builder.Services.Configure<RequestProtectionOptions>(builder.Configuration.GetSection("RequestProtection"));
builder.Services.AddAuthentication(MerchantAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, MerchantAuthenticationHandler>(
        MerchantAuthenticationDefaults.AuthenticationScheme,
        options => { });
builder.Services.AddAuthorization();
var requestProtectionOptions = builder.Configuration
    .GetSection("RequestProtection")
    .Get<RequestProtectionOptions>() ?? new RequestProtectionOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var merchantId = context.Request.Headers[MerchantAuthenticationDefaults.MerchantIdHeaderName].FirstOrDefault();
        var partitionKey = string.IsNullOrWhiteSpace(merchantId)
            ? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"
            : merchantId;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = requestProtectionOptions.RateLimitPermitLimit,
                Window = requestProtectionOptions.RateLimitWindow,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddSingleton<IPaymentRequestValidator, PaymentRequestValidator>();
builder.Services.AddSingleton<IPaymentService, PaymentService>();
builder.Services.AddHttpClient("AcquiringBank", client =>
{
    client.BaseAddress = new Uri(acquiringBankOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(acquiringBankOptions.TimeoutSeconds);
});
builder.Services.AddSingleton<IAcquiringBankClient, AcquiringBankClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<PaymentRequestSizeLimitMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();
