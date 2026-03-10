namespace Api.Tests.v2026_11_12;

using System.Net;
using System.Net.Http.Json;

[Collection(TestWebApplicationFactoryCollectionFixture.Id)]
public class OrderManagementIntegrationTests
{
    private const string ApiVersion = "2026-11-12";
    private readonly HttpClient _client;

    public OrderManagementIntegrationTests(TestWebApplicationFactoryFixture factory, ITestOutputHelper output)
    {
        factory.OutputHelper = output;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Full_order_lifecycle_happy_path()
    {
        // Create a customer
        var email = "john@example.com";
        var customer = await CreateCustomerAsync(email);
        customer.FirstName.Should().Be("John");
        customer.LastName.Should().Be("Doe");
        customer.Email.Should().Be(email);

        // Create a product
        var product = await CreateProductAsync("Widget", "WDG001", 19.99m);
        product.ProductName.Should().Be("Widget");
        product.Sku.Should().Be("WDG001");
        product.UnitPrice.Should().Be(19.99m);

        // Add stock
        var stockedProduct = await AddStockAsync(product.Id, 100);
        stockedProduct.StockQuantity.Should().Be(100);

        // Create draft order
        var order = await CreateDraftOrderAsync(customer.Id, product.Id, 3);
        order.Status.Should().Be("Draft");
        order.LineItems.Should().HaveCount(1);
        order.LineItems[0].Quantity.Should().Be(3);

        // Submit order
        var submittedOrder = await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/submission?api-version={ApiVersion}");
        submittedOrder.Status.Should().Be("Submitted");
        submittedOrder.SubmittedAt.Should().NotBeNull();

        // Verify stock was reserved
        var productAfterSubmit = await GetAsync<ProductResponse>($"api/Products/{product.Id}?api-version={ApiVersion}");
        productAfterSubmit.StockQuantity.Should().Be(97); // 100 - 3

        // Approve order
        var approvedOrder = await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/approval?api-version={ApiVersion}");
        approvedOrder.Status.Should().Be("Approved");

        // Ship order
        var shippedOrder = await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/shipment?api-version={ApiVersion}");
        shippedOrder.Status.Should().Be("Shipped");
        shippedOrder.ShippedAt.Should().NotBeNull();

        // Deliver order
        var deliveredOrder = await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/delivery?api-version={ApiVersion}");
        deliveredOrder.Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task Create_customer_returns_201_with_location_header()
    {
        var response = await _client.PostAsJsonAsync(
            $"api/Customers?api-version={ApiVersion}",
            new
            {
                FirstName = "Alice",
                LastName = "Smith",
                Email = $"alice-{Guid.NewGuid():N}@example.com",
                ShippingAddress = new
                {
                    Street = "456 Oak Ave",
                    City = "Portland",
                    State = "OR",
                    PostalCode = "97201",
                    Country = "US"
                }
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_customer_duplicate_email_returns_409()
    {
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        await CreateCustomerAsync(email);

        var response = await _client.PostAsJsonAsync(
            $"api/Customers?api-version={ApiVersion}",
            new
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = email,
                ShippingAddress = new
                {
                    Street = "789 Pine St",
                    City = "Seattle",
                    State = "WA",
                    PostalCode = "98101",
                    Country = "US"
                }
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_product_returns_201()
    {
        var sku = $"TST{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var product = await CreateProductAsync("Test Product", sku, 5.00m);

        product.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task Submit_order_insufficient_stock_returns_error()
    {
        var customer = await CreateCustomerAsync();
        var product = await CreateProductAsync("Rare Item", $"RI{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 100.00m);
        await AddStockAsync(product.Id, 2);

        var order = await CreateDraftOrderAsync(customer.Id, product.Id, 5);

        var response = await _client.PostAsync(
            $"api/Orders/{order.Id}/submission?api-version={ApiVersion}",
            null,
            TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_submitted_order_releases_stock()
    {
        var customer = await CreateCustomerAsync();
        var product = await CreateProductAsync("Cancelable", $"CNC{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 25.00m);
        await AddStockAsync(product.Id, 50);

        var order = await CreateDraftOrderAsync(customer.Id, product.Id, 10);
        await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/submission?api-version={ApiVersion}");

        // Stock should be 40 (50 - 10)
        var productAfterSubmit = await GetAsync<ProductResponse>($"api/Products/{product.Id}?api-version={ApiVersion}");
        productAfterSubmit.StockQuantity.Should().Be(40);

        // Cancel
        await PostAsync<OrderResponse>(
            $"api/Orders/{order.Id}/cancellation?api-version={ApiVersion}");

        // Stock should be restored to 50
        var productAfterCancel = await GetAsync<ProductResponse>($"api/Products/{product.Id}?api-version={ApiVersion}");
        productAfterCancel.StockQuantity.Should().Be(50);
    }

    [Fact]
    public async Task Get_order_by_id_returns_order()
    {
        var customer = await CreateCustomerAsync();
        var product = await CreateProductAsync("Fetchable", $"FTC{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 15.00m);
        await AddStockAsync(product.Id, 10);

        var order = await CreateDraftOrderAsync(customer.Id, product.Id, 2);

        var fetched = await GetAsync<OrderResponse>($"api/Orders/{order.Id}?api-version={ApiVersion}");

        fetched.Id.Should().Be(order.Id);
        fetched.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Get_order_by_nonexistent_id_returns_404()
    {
        var response = await _client.GetAsync(
            $"api/Orders/{Guid.NewGuid()}?api-version={ApiVersion}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_line_item_to_draft_order_succeeds()
    {
        var customer = await CreateCustomerAsync();
        var product1 = await CreateProductAsync("P1", $"P1{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 10.00m);
        var product2 = await CreateProductAsync("P2", $"P2{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 20.00m);
        await AddStockAsync(product1.Id, 10);
        await AddStockAsync(product2.Id, 10);

        var order = await CreateDraftOrderAsync(customer.Id, product1.Id, 1);

        var response = await _client.PostAsJsonAsync(
            $"api/Orders/{order.Id}/line-items?api-version={ApiVersion}",
            new { ProductId = product2.Id, Quantity = 2 },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken);
        updated!.LineItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task Remove_line_item_from_draft_order_succeeds()
    {
        var customer = await CreateCustomerAsync();
        var product1 = await CreateProductAsync("R1", $"R1{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 10.00m);
        var product2 = await CreateProductAsync("R2", $"R2{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 20.00m);
        await AddStockAsync(product1.Id, 10);
        await AddStockAsync(product2.Id, 10);

        var order = await CreateDraftOrderAsync(customer.Id, product1.Id, 1);

        // Add second item
        var addResponse = await _client.PostAsJsonAsync(
            $"api/Orders/{order.Id}/line-items?api-version={ApiVersion}",
            new { ProductId = product2.Id, Quantity = 2 },
            TestContext.Current.CancellationToken);
        var updated = await addResponse.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken);

        // Remove first item
        var firstLineItemId = updated!.LineItems[0].Id;
        var deleteResponse = await _client.DeleteAsync(
            $"api/Orders/{order.Id}/line-items/{firstLineItemId}?api-version={ApiVersion}",
            TestContext.Current.CancellationToken);

        deleteResponse.EnsureSuccessStatusCode();
        var afterDelete = await deleteResponse.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken);
        afterDelete!.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_orders_by_customer_returns_orders()
    {
        var customer = await CreateCustomerAsync();
        var product = await CreateProductAsync("LO", $"LO{Guid.NewGuid():N}"[..8].ToUpperInvariant(), 10.00m);
        await AddStockAsync(product.Id, 100);

        await CreateDraftOrderAsync(customer.Id, product.Id, 1);
        await CreateDraftOrderAsync(customer.Id, product.Id, 2);

        var orders = await GetAsync<List<OrderResponse>>(
            $"api/Customers/{customer.Id}/orders?api-version={ApiVersion}");

        orders.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Health_check_returns_healthy()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Helper methods
    private async Task<CustomerResponse> CreateCustomerAsync(string? email = null)
    {
        email ??= $"test-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync(
            $"api/Customers?api-version={ApiVersion}",
            new
            {
                FirstName = "John",
                LastName = "Doe",
                Email = email,
                ShippingAddress = new
                {
                    Street = "123 Main St",
                    City = "Anytown",
                    State = "WA",
                    PostalCode = "98052",
                    Country = "US"
                }
            },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<ProductResponse> CreateProductAsync(string name, string sku, decimal unitPrice)
    {
        var response = await _client.PostAsJsonAsync(
            $"api/Products?api-version={ApiVersion}",
            new { ProductName = name, Sku = sku, UnitPrice = unitPrice },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<ProductResponse> AddStockAsync(Guid productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync(
            $"api/Products/{productId}/stock-additions?api-version={ApiVersion}",
            new { Quantity = quantity },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<OrderResponse> CreateDraftOrderAsync(Guid customerId, Guid productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync(
            $"api/Orders?api-version={ApiVersion}",
            new
            {
                CustomerId = customerId,
                LineItems = new[]
                {
                    new { ProductId = productId, Quantity = quantity }
                }
            },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<T> PostAsync<T>(string url) where T : class
    {
        var response = await _client.PostAsync(url, null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    private async Task<T> GetAsync<T>(string url) where T : class
    {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    // Response DTOs for deserialization
    private record CustomerResponse(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        ShippingAddressResponse ShippingAddress);

    private record ShippingAddressResponse(
        string Street,
        string City,
        string State,
        string PostalCode,
        string Country);

    private record ProductResponse(
        Guid Id,
        string ProductName,
        string Sku,
        decimal UnitPrice,
        string UnitPriceCurrency,
        int StockQuantity);

    private record OrderResponse(
        Guid Id,
        Guid CustomerId,
        string CreatedByActorId,
        string Status,
        List<LineItemResponse> LineItems,
        decimal OrderTotal,
        string OrderTotalCurrency,
        DateTime CreatedAt,
        DateTime? SubmittedAt,
        DateTime? ShippedAt);

    private record LineItemResponse(
        Guid Id,
        Guid ProductId,
        string ProductName,
        int Quantity,
        decimal UnitPrice,
        string UnitPriceCurrency);
}
