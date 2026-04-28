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
| **SKU** | Stock Keeping Unit — a unique identifier for a product, 3–20 characters, uppercase letters, digits, and hyphens only (no leading/trailing hyphens). Examples: `WGT-PRO-001`, `ABC123`, `X1`. |
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
- SKU — required, 3–20 characters, uppercase letters, digits, and hyphens only, no leading/trailing hyphens (regex: `^[A-Z0-9][A-Z0-9\-]{1,18}[A-Z0-9]$`), unique across all products
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
- Precondition: None beyond being in Submitted status.
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

The template already registers actor providers conditionally in `Api/src/DependencyInjection.cs`:
- **Development:** `AddDevelopmentActorProvider()` reads the `X-Test-Actor` HTTP header (from `Trellis.Asp.Authorization`)
- **Production:** `AddEntraActorProvider()` reads JWT claims from Azure Entra ID tokens

**Do NOT create a custom `HttpActorProvider` or `IActorProvider` implementation.** The framework provides these.

For testing and evaluation, the `X-Test-Actor` header contains a JSON payload: `{"Id": "actor-1", "Permissions": ["orders:create", "orders:read"]}`. If the header is absent, `DevelopmentActorProvider` returns a default actor (configurable via `DevelopmentActorOptions`).

For API integration tests, use `factory.CreateClientWithActor("user-1", "perm1", "perm2")` from `Trellis.Testing`.

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
- **Behavior:** Fires state machine transition Submitted → Approved.
- **Success:** Returns the Order in Approved status.
- **Failure:** Invalid transition → 400. Order not found → 404.

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
- **Input:** customerId
- **Behavior:** Verifies customer exists. Returns list of orders for the customer.
- **Success:** Returns the list of Orders belonging to the Customer.
- **Failure:** Customer not found → 404.

### 6.14 List Overdue Orders (Query)

- **Permission required:** `orders:read-all`
- **Definition:** An order is overdue if it has been in Submitted status for more than 7 days without being Approved.
- **Input:** none
- **Success:** Returns the list of overdue Orders.

## 7. API Endpoints

All endpoints return JSON. Error responses follow RFC 9457 (Problem Details). API versioning uses query parameter `api-version` with date values (e.g., `?api-version=2026-11-12`).

| Method | Path | Operation | Permission | Success | Error Codes |
|--------|------|-----------|-----------|---------|-------------|
| POST | /api/customers | Create Customer | `customers:create` | 201 Created | 400, 403, 409 |
| GET | /api/customers/{id} | Get Customer | `customers:read` | 200 OK | 403, 404 |
| POST | /api/products | Create Product | `products:create` | 201 Created | 400, 403, 409 |
| GET | /api/products/{id} | Get Product | `products:read` | 200 OK | 403, 404 |
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
| GET | /api/customers/{id}/orders | List Orders by Customer | `orders:read-all` | 200 OK | 403, 404 |
| GET | /api/orders/overdue | List Overdue Orders | `orders:read-all` | 200 OK | 403 |

- All requests must include `?api-version=2026-11-12` query parameter. Requests without a version return 400 Bad Request.
- All requests must include authentication context via the `X-Test-Actor` header (see Section 5.5). Requests without authentication context use the default Admin actor.
- POST /customers, POST /products, and POST /orders return 201 Created with a Location header pointing to the created resource.
- A `/health` endpoint must be available for health checks.

### 7.1 Response Schemas

All successful responses use the following JSON shapes. Field names use **camelCase**. Use these exact field names in DTOs — this ensures api.http files and integration tests are portable across implementations.

**Customer Response** (used by Create Customer 201, and embedded in order responses):
```json
{
  "id": "guid",
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "phoneNumber": "string | null",
  "shippingAddress": {
    "street": "string",
    "city": "string",
    "state": "string",
    "postalCode": "string",
    "country": "string"
  }
}
```

**Product Response** (used by Create Product 201, Add Stock 200):
```json
{
  "id": "guid",
  "productName": "Widget Pro",
  "sku": "WGT-PRO-001",
  "unitPrice": { "amount": 29.99, "currency": "USD" },
  "stockQuantity": 0
}
```

**Order Response** (used by Create Draft Order 201, Get Order 200, and all state transition 200 responses):
```json
{
  "id": "guid",
  "customerId": "guid",
  "createdByActorId": "string",
  "status": "Draft | Submitted | Approved | Shipped | Delivered | Cancelled",
  "total": { "amount": 49.97, "currency": "USD" },
  "createdAt": "2026-01-15T12:00:00Z",
  "submittedAt": "2026-01-15T12:30:00Z | null",
  "shippedAt": "2026-01-16T09:00:00Z | null",
  "deliveredAt": "2026-01-17T14:00:00Z | null",
  "lineItems": [
    {
      "id": "guid",
      "productId": "guid",
      "productName": "string",
      "quantity": 2,
      "unitPrice": { "amount": 19.99, "currency": "USD" }
    }
  ]
}
```

**List Responses** (List Orders by Customer, List Overdue Orders):
Returns a JSON array of Order Response objects: `[ { ... }, { ... } ]`

**Error Response** (all error codes follow RFC 9457 Problem Details):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "Specific error message",
  "errors": { "fieldName": ["Error detail"] },
  "traceId": "00-..."
}
```

### 7.2 Request Schemas

**Create Customer:**
```json
{
  "firstName": "string",
  "lastName": "string",
  "email": "string",
  "phoneNumber": "string | null",
  "shippingAddress": {
    "street": "string",
    "city": "string",
    "state": "string",
    "postalCode": "string",
    "country": "string"
  }
}
```

**Create Product:**
```json
{
  "productName": "Widget Pro",
  "sku": "WGT-PRO-001",
  "unitPrice": { "amount": 29.99, "currency": "USD" }
}
```

**Add Stock:**
```json
{ "quantity": 10 }
```

**Create Draft Order:**
```json
{
  "customerId": "guid",
  "lineItems": [
    { "productId": "guid", "quantity": 2 }
  ]
}
```

**Add Line Item:**
```json
{ "productId": "guid", "quantity": 1 }
```

**Return Order** (Part 2 feature):
```json
{ "reason": "string (10-500 characters)" }
```

Submit, Approve, Ship, Deliver, and Cancel require no request body.

## 8. Persistence

- **Database:** Azure Cosmos DB (use the local emulator at `https://localhost:8081` with key `C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==`).
- **Connection** in `appsettings.Development.json`: Use the emulator endpoint and key above with database name `OrderManagement`.
- **Entities to persist:** Customer, Product, Order (with LineItems as embedded documents within Order).
- **Containers:** One container per aggregate root — `Customers`, `Products`, `Orders`. Use the entity ID as the partition key (e.g., `/id`).
- **Unique constraints:** Customer.Email (unique key policy on Customers container), Product.SKU (unique key policy on Products container).
- **OrderStatus** stored as string.
- **PhoneNumber** stored as nullable property.
- **Database creation:** Create the database and containers on startup in development mode if they don't exist. Use the Cosmos DB SDK directly — do NOT use EF Core.
- **Repository pattern:** Implement repositories using `Microsoft.Azure.Cosmos` SDK. Use `Container.CreateItemAsync`, `ReadItemAsync`, `ReplaceItemAsync`, `GetItemQueryIterator` with SQL queries.
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

## 10. Testing Requirements

### 10.1 Domain Tests

Unit tests for each aggregate's business rules. No external dependencies.

- Customer: valid creation with/without phone, invalid email, blank name
- Product: valid creation, add stock, reserve stock, insufficient stock error
- Order: create with line items, add/remove line items, duplicate product rejection, last line item protection
- State machine: every valid transition, every invalid transition, stock reservation on submit, stock release on cancel
- Overdue specification: matches orders submitted 8+ days ago, excludes recent orders and approved orders

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
- Full order lifecycle: create customer, create product, add stock, create order, submit, approve, ship, deliver
- Cancel by non-owner → 403
- Cancel by owner → 200
- Overdue orders query → 200 with correct filtered list
- Missing api-version → 400
- Health check → 200
