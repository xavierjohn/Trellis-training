# Order Management System — Specification

> This specification describes a simplified but realistic Order Management System. It is intended to be given to an AI along with the Trellis Copilot Instructions to generate a working .NET application. The spec focuses on business requirements and outcomes. Implementation patterns come from the Copilot Instructions.

## 1. Domain Overview

A company sells products to customers. Customers place orders, each order contains line items referencing products. Orders go through a lifecycle from draft to completion. Payments are processed externally. Inventory is tracked per product.

The system uses role-based access control. Sales representatives create customers and orders. Warehouse managers approve and ship orders. Administrators have full access.

## 2. Ubiquitous Language

| Term | Definition |
|------|-----------|
| **Customer** | A person or organization that places orders. Identified by a unique ID. Has a name, email, optional phone number, and shipping address. |
| **Product** | An item available for purchase. Has a name, SKU, unit price, and current stock quantity. |
| **Order** | A request by a customer to purchase one or more products. Has a unique ID, belongs to one customer, contains one or more line items, and has a status. Records which actor created it. |
| **Line Item** | A single entry in an order specifying a product, quantity, and unit price at time of order. |
| **Order Status** | The current state of an order: Draft, Submitted, Approved, Shipped, Delivered, Cancelled. |
| **SKU** | Stock Keeping Unit — a unique alphanumeric identifier for a product, 3–20 characters, uppercase letters and digits only. |
| **Shipping Address** | A value consisting of street, city, state, postal code, and country. All fields required. |
| **Order Total** | The sum of (unit price × quantity) for all line items in the order. |
| **Inventory** | The current available stock quantity of a product. Cannot go below zero. |
| **Actor** | The authenticated user performing an operation. Has an identity, a set of permissions, and optional attributes. |
| **Permission** | A named capability granted to an actor (e.g., `orders:create`). Checked before operations execute. |

## 3. Aggregates

### 3.1 Customer Aggregate

**Identity:** CustomerId (unique identifier)

**Properties:**
- FirstName — required, 1–100 characters
- LastName — required, 1–100 characters
- Email — valid email address, unique across all customers
- PhoneNumber — optional, valid phone number
- ShippingAddress — street, city, state, postal code, country (all required)

**Rules:**
- A customer cannot be created without a valid name, email, and shipping address.
- PhoneNumber is optional.
- Email must be unique. Attempting to create a customer with a duplicate email produces a Conflict error.

### 3.2 Product Aggregate

**Identity:** ProductId (unique identifier)

**Properties:**
- ProductName — required, 1–200 characters
- SKU — required, 3–20 characters, uppercase alphanumeric only, unique across all products
- UnitPrice — must be greater than zero, USD currency
- StockQuantity — non-negative integer

**Rules:**
- A product cannot be created without a valid name, SKU, and unit price.
- SKU must be unique. Attempting to create a product with a duplicate SKU produces a Conflict error.
- Stock quantity cannot go below zero. Attempting to reduce stock below zero produces a Validation error.

**Operations:**
- `AddStock(quantity)` — increases stock quantity. Quantity must be positive.
- `ReserveStock(quantity)` — decreases stock quantity. Fails if insufficient stock.

### 3.3 Order Aggregate

**Identity:** OrderId (unique identifier)

**Properties:**
- CustomerId — reference to an existing customer
- CreatedByActorId — the identity of the user who created the order
- LineItems — collection of line items (at least one required)
- Status — current order status
- CreatedAt — UTC timestamp when order was created
- SubmittedAt — UTC timestamp when order was submitted (absent if not yet submitted)
- ShippedAt — UTC timestamp when order was shipped (absent if not yet shipped)
- PaidAt — UTC timestamp when payment was confirmed (absent if payment not yet confirmed)
- PaymentReference — external payment-gateway reference recorded on payment confirmation (absent until paid)
- PaidAmount — amount recorded when payment was confirmed (absent until paid)

**Line Item Properties:**
- LineItemId (unique identifier)
- ProductId — reference to an existing product
- ProductName — snapshot of product name at time of ordering
- Quantity — positive integer, minimum 1, maximum 999
- UnitPrice — snapshot of product price at time of ordering

**Rules:**
- An order must have at least one line item.
- An order must reference a valid customer.
- Each line item must reference a valid product.
- The same product cannot appear in multiple line items within the same order. Combine quantities instead.
- Quantity per line item must be between 1 and 999.
- Unit price is captured at the time the line item is added and does not change if the product price changes later.
- Payment must be confirmed (via the payment round-trip in Section 11) before an order can be approved. Recording a payment is idempotent for an exact duplicate (same reference and amount) and rejects a conflicting different payment.

## 4. State Machine

Order status transitions follow these rules:

```
Draft → Submitted → Approved → Shipped → Delivered
                                  ↓
                              Cancelled

Draft → Cancelled
Submitted → Cancelled
Approved → Cancelled
```

**Transition: Draft → Submitted**
- Precondition: Order must have at least one line item.
- Precondition: All line items must have sufficient stock available.
- Side effect: Reserve stock for each line item (reduce product stock quantity).
- Side effect: Set SubmittedAt to current UTC time.
- Domain event: OrderSubmittedEvent(OrderId, CustomerId, OrderTotal, SubmittedAt)

**Transition: Submitted → Approved**
- Precondition: Payment must already be confirmed for the order (PaidAt present). Approving an unpaid order fails with a validation error (422). See Section 11 for the payment round-trip.
- Domain event: OrderApprovedEvent(OrderId, ApprovedAt)

**Transition: Approved → Shipped**
- Precondition: None beyond being in Approved status.
- Side effect: Set ShippedAt to current UTC time.
- Domain event: OrderShippedEvent(OrderId, CustomerId, ShippedAt)

**Transition: Shipped → Delivered**
- Precondition: None beyond being in Shipped status.
- Domain event: OrderDeliveredEvent(OrderId, DeliveredAt)

**Transition: Draft/Submitted/Approved → Cancelled**
- Precondition: Order must NOT be in Shipped or Delivered status.
- Side effect: If order was Submitted or Approved, release reserved stock (restore product stock quantity for each line item).
- Domain event: OrderCancelledEvent(OrderId, CancelledFromStatus, CancelledAt)

**Invalid transitions produce a Validation error with a message explaining why the transition is not allowed.**

**Payment confirmation (not a state transition)**
- Recording a confirmed payment sets PaidAt, PaymentReference, and PaidAmount and raises OrderPaidEvent(OrderId, PaymentReference, PaidAt). It does not change the order status — it is a precondition that unblocks the Submitted → Approved transition.
- Idempotent: recording the exact same payment (same reference and amount) again is a no-op success; a different payment for an already-paid order is a Conflict (409).

## 5. Authorization

### 5.1 Permissions

The system defines the following permissions:

| Permission | Description |
|-----------|-------------|
| `customers:create` | Create new customers |
| `products:create` | Create new products |
| `products:manage-stock` | Add stock to products |
| `orders:create` | Create draft orders and manage line items |
| `orders:submit` | Submit draft orders |
| `orders:approve` | Approve submitted orders |
| `orders:ship` | Ship approved orders |
| `orders:deliver` | Mark shipped orders as delivered |
| `orders:cancel` | Cancel orders (subject to ownership check) |
| `orders:read` | View orders |
| `orders:read-all` | View any customer's orders and overdue orders |

### 5.2 Roles

Roles are not enforced by the system — they exist in the identity provider. The spec defines them here so test fixtures can construct actors with the right permission sets.

| Role | Permissions |
|------|------------|
| **SalesRep** | `customers:create`, `orders:create`, `orders:submit`, `orders:cancel`, `orders:read` |
| **WarehouseManager** | `products:create`, `products:manage-stock`, `orders:approve`, `orders:ship`, `orders:deliver`, `orders:read-all` |
| **Admin** | All permissions |

### 5.3 Permission Checks

Every command and query declares its required permissions. Missing permission → Forbidden error → HTTP 403.

### 5.4 Resource-Based Authorization (Cancel Order)

Cancel Order has an ownership check in addition to the `orders:cancel` permission:

- An actor with `orders:cancel` can cancel an order **only if** the actor created the order (actor's identity matches order's CreatedByActorId).
- An actor with **both** `orders:cancel` and `orders:read-all` (i.e., Admin) can cancel any order regardless of who created it.

### 5.5 Actor Provider

In the API layer, the current actor is determined from the request context:
- Actor identity from the `sub` (or `oid`) claim
- Actor permissions from `role` claims

For testing and evaluation, the API layer reads a custom `X-Test-Actor` header containing a JSON payload: `{"id": "actor-1", "permissions": ["orders:create", "orders:read"]}`. If the header is absent, use a default Admin actor so existing tests don't break.

## 6. Operations (Use Cases)

All operations are implemented as Commands or Queries using CQRS.

### 6.1 Create Customer (Command)

- **Permission required:** `customers:create`
- **Input:** firstName, lastName, email, phoneNumber (optional), shippingAddress
- **Validation:** firstName, lastName, email, shippingAddress fields are validated. phoneNumber, when provided, must be valid.
- **Success:** Returns the created Customer.
- **Failure:** Validation error → 400. Duplicate email → 409.

### 6.2 Create Product (Command)

- **Permission required:** `products:create`
- **Input:** productName, sku, unitPrice
- **Validation:** productName, sku, unitPrice are validated.
- **Success:** Returns the created Product.
- **Failure:** Validation error → 400. Duplicate SKU → 409.

### 6.3 Add Stock (Command)

- **Permission required:** `products:manage-stock`
- **Input:** productId, quantity
- **Validation:** quantity must be positive.
- **Success:** Returns updated Product.
- **Failure:** Validation error → 400. Product not found → 404.

### 6.4 Create Draft Order (Command)

- **Permission required:** `orders:create`
- **Input:** customerId, list of (productId, quantity)
- **Validation:** customerId required, at least one line item, no duplicate productIds in list, each quantity between 1 and 999.
- **Behavior:** Fetch customer and all referenced products. Create order with unit prices captured from products at creation time. Record the actor's identity as CreatedByActorId. Stock is NOT reserved yet.
- **Success:** Returns the created Order in Draft status.
- **Failure:** Validation error → 400. Customer or product not found → 404.

### 6.5 Add Line Item to Draft Order (Command)

- **Permission required:** `orders:create`
- **Input:** orderId, productId, quantity
- **Validation:** quantity between 1 and 999.
- **Behavior:** Order must be in Draft status. Product must not already be in the order. Unit price is captured from the product.
- **Success:** Returns the updated Order with the new line item.
- **Failure:** Validation error (not Draft, duplicate product, invalid quantity) → 400. Order or product not found → 404.

### 6.6 Remove Line Item from Draft Order (Command)

- **Permission required:** `orders:create`
- **Input:** orderId, lineItemId
- **Behavior:** Order must be in Draft status. Order must have more than one line item (cannot remove the last one).
- **Success:** Returns the updated Order without the removed line item.
- **Failure:** Order not in Draft or cannot remove last line item → 400. Order or line item not found → 404.

### 6.7 Submit Order (Command)

- **Permission required:** `orders:submit`
- **Input:** orderId
- **Behavior:** Fires state machine transition Draft → Submitted. Reserves stock for each line item.
- **Success:** Returns the Order in Submitted status.
- **Failure:** Invalid transition or insufficient stock → 400. Order not found → 404.

### 6.8 Approve Order (Command)

- **Permission required:** `orders:approve`
- **Input:** orderId
- **Behavior:** Fires state machine transition Submitted → Approved. Requires that payment has already been confirmed for the order (see Section 11) — approval is gated on the payment round-trip.
- **Success:** Returns the Order in Approved status.
- **Failure:** Payment not yet confirmed → 422. Invalid transition → 400. Order not found → 404.

### 6.9 Ship Order (Command)

- **Permission required:** `orders:ship`
- **Input:** orderId
- **Behavior:** Fires state machine transition Approved → Shipped.
- **Success:** Returns the Order in Shipped status.
- **Failure:** Invalid transition → 400. Order not found → 404.

### 6.10 Deliver Order (Command)

- **Permission required:** `orders:deliver`
- **Input:** orderId
- **Behavior:** Fires state machine transition Shipped → Delivered.
- **Success:** Returns the Order in Delivered status.
- **Failure:** Invalid transition → 400. Order not found → 404.

### 6.11 Cancel Order (Command with Ownership Check)

- **Permission required:** `orders:cancel`
- **Ownership check:** Actor must be the order creator OR have `orders:read-all` permission
- **Input:** orderId
- **Behavior:** Fires state machine transition to Cancelled. If order was Submitted or Approved, releases reserved stock.
- **Success:** Returns the Order in Cancelled status.
- **Failure:** Forbidden (not owner and not admin) → 403. Invalid transition → 400. Order not found → 404.

### 6.12 Get Order by ID (Query)

- **Permission required:** `orders:read`
- **Input:** orderId
- **Success:** Returns the Order.
- **Failure:** Order not found → 404.

### 6.13 List Orders by Customer (Query)

- **Permission required:** `orders:read-all`
- **Input:** customerId, optional `cursor`, optional `limit`
- **Behavior:** Verifies customer exists. Returns a bounded page of the customer's orders using cursor (keyset) pagination ordered by the order's id (see §7.1).
- **Success:** Returns a page of Orders belonging to the Customer, with a `next` cursor when more pages remain.
- **Failure:** Customer not found → 404. Malformed `cursor` → 422.

### 6.14 List Overdue Orders (Query)

- **Permission required:** `orders:read-all`
- **Definition:** An order is overdue if it has been in Submitted status for more than 7 days without being Approved.
- **Input:** optional `cursor`, optional `limit`
- **Behavior:** Returns a bounded page of overdue orders using cursor (keyset) pagination ordered by the order's id (see §7.1).
- **Success:** Returns a page of overdue Orders, with a `next` cursor when more pages remain.
- **Failure:** Malformed `cursor` → 422.

## 7. API Endpoints

All endpoints return JSON. Error responses follow RFC 9457 (Problem Details). API versioning uses query parameter `api-version` with date values (e.g., `?api-version=2026-11-12`).

| Method | Path | Operation | Permission | Success | Error Codes |
|--------|------|-----------|-----------|---------|-------------|
| POST | /api/customers | Create Customer | `customers:create` | 201 Created | 400, 403, 409 |
| POST | /api/products | Create Product | `products:create` | 201 Created | 400, 403, 409 |
| POST | /api/products/{id}/stock-additions | Add Stock | `products:manage-stock` | 200 OK | 400, 403, 404 |
| POST | /api/orders | Create Draft Order | `orders:create` | 201 Created | 400, 403, 404 |
| POST | /api/orders/{id}/line-items | Add Line Item | `orders:create` | 200 OK | 400, 403, 404 |
| DELETE | /api/orders/{id}/line-items/{lineItemId} | Remove Line Item | `orders:create` | 200 OK | 400, 403, 404 |
| POST | /api/orders/{id}/submission | Submit Order | `orders:submit` | 200 OK | 400, 403, 404 |
| POST | /api/orders/{id}/approval | Approve Order | `orders:approve` | 200 OK | 400, 403, 404 |
| POST | /api/orders/{id}/shipment | Ship Order | `orders:ship` | 200 OK | 400, 403, 404 |
| POST | /api/orders/{id}/delivery | Deliver Order | `orders:deliver` | 200 OK | 400, 403, 404 |
| POST | /api/orders/{id}/cancellation | Cancel Order | `orders:cancel` + ownership | 200 OK | 400, 403, 404 |
| GET | /api/orders/{id} | Get Order | `orders:read` | 200 OK | 403, 404 |
| GET | /api/customers/{id}/orders | List Orders by Customer (paged) | `orders:read-all` | 200 OK | 403, 404, 422 |
| GET | /api/orders/overdue | List Overdue Orders (paged) | `orders:read-all` | 200 OK | 403, 422 |

- All requests must include `?api-version=2026-11-12` query parameter. Requests without a version return 400 Bad Request.
- All requests must include authentication context via the `X-Test-Actor` header (see Section 5.5). Requests without authentication context use the default Admin actor.
- POST /customers and POST /orders return 201 Created with a Location header pointing to the created resource.
- A `/health` endpoint must be available for health checks.

### 7.1 List Pagination

Every list endpoint returns a **bounded page** — never an unbounded array (an unbounded list is a latent outage at scale):

- **Query parameters:** an optional `limit` (the server clamps it to a maximum; a sensible default applies when omitted) and an optional opaque `cursor`.
- **Ordering & paging:** cursor (keyset) pagination ordered by the order's id — a time-ordered UUID — used as a stable forward-only seek key; over-fetch by one to determine whether another page exists. Do **not** use OFFSET/skip.
- **Response body:** a page envelope containing `items`, `next`/`previous` cursor links, `requestedLimit`, `appliedLimit`, `deliveredCount`, and `wasCapped`.
- **Response headers:** an RFC 8288 `Link` header carrying `rel="next"` (and `rel="prev"` when a previous page exists).
- **Errors:** a malformed `cursor` → 422 (per the framework's invalid-input mapping).

Applies to `GET /api/orders/overdue` and `GET /api/customers/{id}/orders`.

## 8. Persistence

- **Database:** SQLite (file-based, zero setup).
- **Connection string** in `appsettings.Development.json`: `Data Source=OrderManagement.db`.
- **Entities to persist:** Customer, Product, Order (with LineItems).
- **Unique constraints:** Customer.Email, Product.SKU.
- **Indexes:** Order by CustomerId. Order by Status + SubmittedAt (for overdue query performance).
- **List pagination:** List endpoints page by cursor (keyset) using the order's id as the stable seek key (see §7.1); no OFFSET/skip.
- **OrderStatus** stored as string.
- **PhoneNumber** stored as nullable column.
- **Database creation:** Use `EnsureCreated()` on startup in development mode. Do NOT use EF Core migrations — `EnsureCreated` is simpler for development and avoids migration conflicts on repeated runs.

## 9. Error Behavior

| Situation | Expected Error | HTTP Status |
|-----------|---------------|-------------|
| Invalid input (blank name, bad email format, etc.) | Validation error | 400 |
| Invalid state transition (e.g., Draft → Approved) | Validation error | 400 |
| Insufficient stock on submit | Validation error | 400 |
| Entity not found by ID | Not Found error | 404 |
| Duplicate email on customer creation | Conflict error | 409 |
| Duplicate SKU on product creation | Conflict error | 409 |
| Missing required permission | Forbidden error | 403 |
| Cancel order by non-owner (without admin) | Forbidden error | 403 |
| Approve an order before its payment is confirmed | Validation error | 422 |
| Record a conflicting payment (different reference or amount already recorded) | Conflict error | 409 |

## 10. Testing Requirements

### 10.1 Domain Tests

Unit tests for each aggregate's business rules. No external dependencies.

- Customer: valid creation with/without phone, invalid email, blank name
- Product: valid creation, add stock, reserve stock, insufficient stock error
- Order: create with line items, add/remove line items, duplicate product rejection, last line item protection
- State machine: every valid transition, every invalid transition, stock reservation on submit, stock release on cancel
- Overdue specification: matches orders submitted 8+ days ago, excludes recent orders and approved orders
- Payment: approval is blocked before payment is confirmed; recording payment then approving succeeds; an exact-duplicate payment is idempotent; a different payment conflicts

### 10.2 Application Tests

Handler tests with mocked repositories.

- Authorization: command succeeds with correct permission, fails with missing permission
- Resource authorization: cancel by owner succeeds, cancel by non-owner fails, cancel by admin succeeds
- Not found: handler returns Not Found error when entity doesn't exist

### 10.3 API Integration Tests

HTTP round-trip tests using a test web application factory with SQLite in-memory.

- Create customer → 201 with Location header
- Duplicate email → 409 with Problem Details
- Missing permission → 403
- Full order lifecycle: create customer, create product, add stock, create order, submit, confirm payment, approve, ship, deliver
- Cancel by non-owner → 403
- Cancel by owner → 200
- Overdue orders query → 200 with a bounded page of the correctly filtered orders; a `limit` smaller than the total returns a `next` cursor, and following it returns the remaining orders with no duplicates or gaps
- List orders by customer → 200 with a bounded page; a malformed `cursor` → 422
- Missing api-version → 400
- Health check → 200
- Eventing: submitting an order captures an OrderSubmitted message in the outbox; a PaymentConfirmed event dispatched through the inbox unblocks approval; dispatching the same PaymentConfirmed event twice is de-duplicated by the inbox

## 11. Payment Round-Trip and Integration Events

An order cannot be approved until its payment has been confirmed. Payment confirmation arrives asynchronously from an external payments bounded context, delivered reliably via a transactional **outbox** (producing side) and an idempotent **inbox** (consuming side).

### 11.1 Flow

1. A client submits a Draft order (Section 6.7). In the SAME database transaction as the order change, the OrderSubmitted domain event is captured to the outbox and translated to a stable `OrderSubmittedIntegrationEvent` contract. Nothing is published to the broker inside the request, so a submit either fully commits (order + outbox row) or not at all — no lost events, no dual-write.
2. A background relay publishes committed outbox messages to the message broker after the transaction commits (at-least-once delivery).
3. The external payments service observes OrderSubmitted and, once payment clears, publishes a `PaymentConfirmedIntegrationEvent` back onto the broker.
4. A consumer receives PaymentConfirmed and dispatches it through the idempotent inbox, which de-duplicates redeliveries by event id (per consumer) before invoking the handler.
5. The handler records the payment on the order (setting PaidAt / PaymentReference / PaidAmount and raising OrderPaidEvent), which unblocks the Submitted → Approved transition (Section 6.8).

### 11.2 Integration Event Contracts

Stable, versioned, transport-facing records (camelCase JSON), decoupled from the internal domain events:

- `OrderSubmittedIntegrationEvent(EventId, OrderId, CustomerId, OrderTotal, OccurredAt, Currency = "USD")` — message type `orders.order-submitted.v1`.
- `OrderCancelledIntegrationEvent(EventId, OrderId, CancelledFromStatus, OccurredAt)` — message type `orders.order-cancelled.v1`.
- `PaymentConfirmedIntegrationEvent(EventId, OrderId, AmountPaid, PaymentReference, OccurredAt, Currency = "USD")` — message type `payments.payment-confirmed.v1`.

Event ids are deterministic (UUIDv5 over the order id plus a discriminator) so a retried translation yields the same integration event id, aiding consumer de-duplication ("dedupe on business identity, not the transport message id").

### 11.3 Hardened Consumer Rules

The PaymentConfirmed handler records payment ONLY for a Submitted order whose total matches the confirmed USD amount. Every other outcome is logged and acknowledged (so the broker does not redeliver a poison message forever):

- Non-USD currency → ignored.
- Unknown or non-Submitted order (including cancelled) → ignored.
- Malformed payment reference → ignored.
- Amount does not match the order total → ignored.
- A conflicting different payment already recorded → ignored (logged as an error).

Because delivery is at-least-once, the handler is idempotent: the inbox de-duplicates by event id, and RecordPayment no-ops an exact duplicate.

### 11.4 Development Payment Simulator

In development only, a simulator stands in for the external payments service: shortly after an order is submitted it publishes a matching `PaymentConfirmedIntegrationEvent` back onto the broker, so the submit → pay → approve round-trip can be exercised end-to-end without a real payments provider. Production wires a real PaymentConfirmed source instead.
