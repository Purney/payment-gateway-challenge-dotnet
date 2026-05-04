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

The acquiring bank timeout defaults to 5 seconds and can be overridden with:

```powershell
$env:AcquiringBank__TimeoutSeconds="5"
```

## Test

```powershell
dotnet test PaymentGateway.sln
```

The test suite drives the implementation through the public HTTP API and focused unit tests. It uses a fake acquiring bank client under the ASP.NET Core test host so payment outcomes are deterministic and do not require Docker.

## API

All API requests require a merchant identity header:

```http
X-Merchant-Id: merchant-a
```

### Create Payment

```http
POST /api/Payments
Content-Type: application/json
X-Merchant-Id: merchant-a
Idempotency-Key: optional-key
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

### Idempotency

`POST /api/Payments` supports an optional `Idempotency-Key` header scoped to the merchant.

- same merchant, same key, same request: returns the original payment response without calling the bank again
- same merchant, same key, different request: returns `409 Conflict`
- different merchants can reuse the same key independently
- bank unavailable responses are not cached, so the merchant can retry later

### Get Payment

```http
GET /api/Payments/{id}
X-Merchant-Id: merchant-a
```

Returns `200 OK` with the stored payment response when the payment exists for that merchant. Returns `404 Not Found` when the id is unknown or belongs to a different merchant.

## Validation

Gateway validation rejects requests before calling the bank when:

- card number is missing, non-numeric, shorter than 14 digits, or longer than 19 digits
- expiry month is outside `1` to `12`
- expiry date is in the past
- currency is missing or not one of `GBP`, `USD`, `EUR`
- amount is less than or equal to zero
- CVV is missing, non-numeric, shorter than 3 digits, or longer than 4 digits

Rejected requests return `400 Bad Request` with status `Rejected`.

## Protection

- Missing `X-Merchant-Id` returns `401 Unauthorized`.
- Requests are rate limited per merchant, defaulting to 60 requests per minute.
- `POST /api/Payments` request bodies larger than 4096 bytes return `413 Payload Too Large`.
- Acquiring bank calls have a configurable timeout.

## Design

The controller is intentionally thin. Payment processing lives in `PaymentService`, which handles validation, bank authorization mapping, masking, idempotency handling, and persistence. The acquiring bank integration is isolated behind `IAcquiringBankClient`.

Payment records are currently stored in an in-memory `List<Payment>` through `IPaymentsRepository`. This is intentionally simple for the challenge and represents where durable database-backed storage would sit in a production service.

Idempotency records are stored in memory and keyed by merchant id plus idempotency key. In production this would also need durable storage so idempotency survives process restarts.

The acquiring bank client sends requests to `/payments` and formats expiry as `MM/YYYY`, matching the simulator contract. Network failures, timeouts, malformed successful responses, and bank `5xx` responses are returned to merchants as `503 Service Unavailable`. Bank `4xx` responses are treated as declines.

Automatic retries are not enabled for bank authorization calls. Retrying a payment authorization without a downstream idempotency guarantee could create duplicate authorizations if the bank processed the original request but the gateway did not receive the response.

## Observability

The API uses structured logging for:

- payment request received
- validation rejection
- idempotency replay or conflict
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
- Header-based merchant authentication is acceptable for the challenge.
- In-memory storage is acceptable for the challenge.
- Declined payments are stored and retrievable.
- Gateway validation failures can be replayed through idempotency but are not stored as payments.
