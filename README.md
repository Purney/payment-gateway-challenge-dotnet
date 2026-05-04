# Payment Gateway

This is a small ASP.NET Core payment gateway for the Checkout.com recruitment challenge.

The gateway exposes merchant-facing endpoints to request a payment authorization and retrieve a previously processed payment. It validates incoming requests, calls the acquiring bank simulator, stores the result in memory, and returns only masked card details.

## Run

Start the bank simulator and API together:

```powershell
docker-compose up --build
```

The API is then available at:

```text
http://localhost:5067
```

For local development without containerizing the API:

```powershell
docker-compose up bank_simulator
dotnet run --project src/PaymentGateway.Api
```

The default local bank URL is `http://localhost:8080`. It can be overridden with:

```powershell
$env:AcquiringBank__BaseUrl="http://localhost:8080"
```

## Test

```powershell
dotnet test PaymentGateway.sln
```

The test suite drives the current implementation through the public HTTP API. It uses a fake acquiring bank client under the ASP.NET Core test host so payment outcomes are deterministic and do not require Docker.

## API

### Create Payment

```http
POST /api/Payments
Content-Type: application/json
```

```json
{
  "card_number": "2222405343248871",
  "expiry_month": 12,
  "expiry_year": 2027,
  "currency": "GBP",
  "amount": 100,
  "cvv": "123"
}
```

Successful authorization response:

```json
{
  "id": "f7680f53-a9df-435d-9b41-724fe29961e8",
  "status": "Authorized",
  "cardNumberLastFour": 8871,
  "expiryMonth": 12,
  "expiryYear": 2027,
  "currency": "GBP",
  "amount": 100
}
```

Possible statuses:

- `Authorized`
- `Declined`
- `Rejected`

The full card number and CVV are never returned.

### Get Payment

```http
GET /api/Payments/{id}
```

Returns `200 OK` with the stored payment response, or `404 Not Found` when the id is unknown.

## Validation

Gateway validation rejects requests before calling the bank when:

- card number is missing, non-numeric, shorter than 14 digits, or longer than 19 digits
- expiry month is outside `1` to `12`
- expiry date is in the past
- currency is missing or not one of `GBP`, `USD`, `EUR`
- amount is less than or equal to zero
- CVV is missing, non-numeric, shorter than 3 digits, or longer than 4 digits

Rejected requests return `400 Bad Request` with status `Rejected`.

## Design

The controller is intentionally thin. Payment processing lives in `PaymentService`, which handles validation, bank authorization mapping, masking, and persistence. The acquiring bank integration is isolated behind `IAcquiringBankClient`.

Payment records are currently stored in an in-memory `ConcurrentDictionary` through `IPaymentsRepository`. This is suitable for the challenge and local testing, but would be replaced by durable storage in production.

The acquiring bank client sends requests to `/payments` and formats expiry as `MM/YYYY`, matching the simulator contract. Bank unavailability or network failures are returned to merchants as `503 Service Unavailable`.

## Observability

The API uses structured logging for:

- payment request received
- validation rejection
- bank request sent
- bank timeout or request failure
- bank unavailable response
- authorization result
- payment stored
- payment retrieved or not found

Sensitive values such as full card number and CVV are not logged.

## Assumptions

- Amount is represented as an integer minor unit.
- Supported currencies are limited to `GBP`, `USD`, and `EUR`.
- In-memory storage is acceptable for the challenge.
- Declined payments are stored and retrievable.
- Gateway validation failures are not stored.
