# Order Management — Order Returns (v2 Delta)

> **Read this file in addition to `order-management-sqlite.md` (or `order-management-cosmosdb.md`).** This document is a delta — it does not replace the v1 spec; it adds a new feature on top.
>
> The v1 spec describes the service as it shipped on `2026-11-12`. This delta describes the next release: a new feature (Order Returns), shipped under a new API version (`2026-12-01`), while keeping every v1 client working unchanged.

## Context

Customers want to return delivered orders. The team needs to add this capability without breaking the existing 16-endpoint surface that v1 clients rely on. The v1 surface stays available indefinitely; the new behaviour ships under a second concurrent API version.

This delta is **storage-agnostic** — it applies whether the v1 implementation chose SQLite/EF Core or Cosmos DB. Storage-specific guidance for the new fields lives alongside the existing v1 storage rules.

## 1. API versions

The service now publishes **two** concurrent API versions. Both are first-class — clients on either version must continue to work indefinitely.

| Version | Status | Surface |
|---|---|---|
| `2026-11-12` | Initial release (v1) | The 16 endpoints in §7.1 of the v1 spec. Unchanged by this delta. |
| `2026-12-01` | Adds Order Returns (v2) | All 16 v1 endpoints PLUS `POST /api/orders/{id}/return`. Order Response shape extended with `returnReason` and `returnedAt`. Order Status enum extended with `Returned`. |

### Routing rules

- The 16 endpoints from the v1 spec are accessible under **both** `2026-11-12` AND `2026-12-01`. Removing v1 access would break existing clients.
- `POST /api/orders/{id}/return` is accessible **only** under `2026-12-01`. A request to that path with `?api-version=2026-11-12` returns 404 (no matching endpoint at that version) — not 400, not 415.
- Every request must include `?api-version=<value>` where `<value>` is one of the published versions. Requests with an unknown api-version return 400 Bad Request. Missing api-version returns 400 (framework-level error). 400 remains reserved for framework-level request problems; business validation failures still return 422 Unprocessable Content.

### Response-shape rules

- Under `?api-version=2026-11-12`, Order Response uses the **v1 shape** (§7.1 of the v1 spec). The fields `returnReason` and `returnedAt` MUST NOT appear, regardless of whether the underlying order has been returned. The v1 client population must not see new fields.
- Under `?api-version=2026-12-01`, Order Response uses the **v2 shape** (§3 below). `returnReason` and `returnedAt` are always present (null until the order is returned).
- The `status` field on a returned order:
  - Under `2026-11-12`, status of a returned order is reported as `"Delivered"`. v1 has no concept of `Returned`; the underlying aggregate state is preserved, only the projection differs.
  - Under `2026-12-01`, status reflects the actual aggregate state including `Returned`.

### Location-header rules

`Location` headers (POST /customers, POST /products, POST /orders, POST /api/orders/{id}/return) MUST round-trip the requested api-version. The Location header echoes whichever version the client requested for the POST, so a subsequent GET to that URL works unchanged. Hardcoding a single version into Location headers (e.g., always emitting `?api-version=2026-12-01`) breaks v1 clients on the GET round-trip.

### Authorization scoping

The new permission `orders:return` is required only for `POST /api/orders/{id}/return`. Every existing v1 endpoint exposed on `?api-version=2026-12-01` MUST NOT require it: an actor whose role grants only the v1 permission set must continue to work against v1 operations regardless of the api-version they request. Authorization scopes belong to operations, not to versions.

## 2. Domain changes

### 2.1 Order status

- Add `Returned` to the `OrderStatus` enum.
- State machine: add transition `Delivered → Returned`.

### 2.2 Order aggregate

Add the following fields:

| Field | Type | Semantics |
|---|---|---|
| `ReturnedAt` | `Maybe<DateTime>` | UTC timestamp at which the return was accepted. Absent until the `Delivered → Returned` transition fires. Populated by `TimeProvider`. |
| `ReturnReason` | `Maybe<ReturnReason>` | Free-text explanation supplied by the customer. Validated 10–500 chars. Absent until return. |

Extend the §3.4 Timestamp fields table of the v1 spec with:

| Field | Aggregate property? | Persisted? | In API response? | Carried on event? |
|---|---|---|---|---|
| `ReturnedAt` | yes (`Maybe<DateTime>`) | yes | yes (v2 only) | `OrderReturnedEvent` |

### 2.3 Return window

Returns are only valid within 30 days of delivery (inclusive). Validation:

- `now - DeliveredAt <= TimeSpan.FromDays(30)` → valid.
- `now - DeliveredAt > TimeSpan.FromDays(30)` → invalid; transition fails with `Error.UnprocessableContent`.
- Both timestamps are UTC instants from the injected `TimeProvider` (no `DateTime.UtcNow`).

### 2.4 Side effects on `Delivered → Returned`

- Set `ReturnedAt` to current UTC time (via `TimeProvider`).
- Release reserved stock for each line item (mirror the cancel-order pattern).
- Raise `OrderReturnedEvent(OrderId, CustomerId, ReturnReason, ReturnedAt)`.

## 3. API changes

### 3.1 New endpoint

| Method | Path | Operation | Permission | Success | Failure |
|---|---|---|---|---|---|
| POST | `/api/orders/{id}/return` | Return Order *(v2 only)* | `orders:return` + ownership | 200 OK with v2 Order Response | 422 (window expired, wrong status), 403 (missing permission OR non-owner non-admin), 404 (order not found OR called under `?api-version=2026-11-12`) |

**Request body:**

```json
{
  "reason": "Defective product"
}
```

The endpoint mirrors Cancel Order's authorization shape: `IAuthorize` with permission `orders:return` AND `IAuthorizeResource` requiring the actor to be the order creator OR have `orders:read-all` (admin).

### 3.2 Order Response — v2 shape (`?api-version=2026-12-01`)

Used by all order-returning endpoints under `2026-12-01` (Create Draft Order 201, Get Order 200, all state-transition 200 responses, and the new Return Order 200 response). Identical to the v1 shape with two additional fields:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "660e8400-e29b-41d4-a716-446655440001",
  "status": "Draft | Submitted | Approved | Shipped | Delivered | Cancelled | Returned",
  "createdAt": "2026-01-15T10:00:00Z",
  "submittedAt": "2026-01-15T11:00:00Z | null",
  "shippedAt": "2026-01-16T08:00:00Z | null",
  "deliveredAt": "2026-01-17T14:00:00Z | null",
  "returnedAt": "2026-02-01T10:00:00Z | null",
  "returnReason": "Defective product | null",
  "lineItems": [ /* unchanged */ ],
  "totalAmount": 19.98,
  "currency": "USD"
}
```

`returnedAt` and `returnReason` are always present (null until the order is returned). The `status` enum admits the new `Returned` value.

### 3.3 Order Response — v1 shape (`?api-version=2026-11-12`)

Unchanged from the v1 spec. The v1 shape MUST NOT include `returnedAt` or `returnReason`, even after this delta ships, even when the underlying aggregate has been returned. A returned order projected through the v1 surface reports `"status": "Delivered"`.

## 4. Storage changes

### 4.1 SQLite/EF Core

- Add `DeliveredAt` (already required by v1) and `ReturnedAt` as `partial Maybe<DateTime>` properties on `Order`. The Trellis `MaybeConvention` and source generator handle persistence — no `HasConversion` boilerplate required.
- Add `ReturnReason` as `Maybe<ReturnReason>` mapped to a string column. Use the same value-object persistence pattern as the existing v1 value objects.

### 4.2 Cosmos DB

- Serialize the new fields via the Cosmos SDK as nullable JSON properties.
- The `OrderStatus.Returned` enum value persists as the string `"Returned"`.

## 5. Compatibility & migration

- **No breaking changes.** v1 clients see no new fields, no new endpoints, no new error shapes.
- **No data migration.** Existing orders persisted before this delta have `ReturnedAt` absent, `ReturnReason` absent, and continue to project to v1 unchanged.
- **Deprecation:** v1 (`2026-11-12`) is not deprecated by this release. Both versions are first-class and stay supported indefinitely. A future release may deprecate v1; that decision is out of scope here.
- **Test invariant:** every v1 acceptance test in `coverage-checklist.md` must remain green when exercised under both `?api-version=2026-11-12` and `?api-version=2026-12-01`. The v2-only assertions are enumerated in `coverage-checklist-returns.md`.
