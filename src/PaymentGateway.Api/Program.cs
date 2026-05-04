using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authentication;

using PaymentGateway.Api.Interfaces;
using PaymentGateway.Api.Security;
using PaymentGateway.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(MerchantAuthenticationDefaults.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, MerchantAuthenticationHandler>(
        MerchantAuthenticationDefaults.AuthenticationScheme,
        options => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();
builder.Services.AddSingleton<IPaymentRequestValidator, PaymentRequestValidator>();
builder.Services.AddSingleton<IPaymentService, PaymentService>();
builder.Services.AddHttpClient("AcquiringBank", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AcquiringBank:BaseUrl"] ?? "http://localhost:8080");
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
