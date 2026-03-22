namespace OrderManagement.Api.v2026_11_12.Controllers;

using Mediator;
using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.v2026_11_12.Models;
using OrderManagement.Application.Commands;
using OrderManagement.Domain;
using ServiceLevelIndicators;
using Trellis.Asp;
using Trellis.Primitives;

/// <summary>
/// Customers management controller.
/// </summary>
[ApiController]
[Consumes("application/json")]
[Produces("application/json")]
[Route("api/[controller]")]
public class CustomersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Create a new customer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var firstNameResult = FirstName.TryCreate(request.FirstName);
        var lastNameResult = LastName.TryCreate(request.LastName);
        var emailResult = EmailAddress.TryCreate(request.Email);
        var shippingAddressResult = ShippingAddress.TryCreate(
            request.Street, request.City, request.State, request.PostalCode, request.Country);

        var combined = firstNameResult
            .Combine(lastNameResult)
            .Combine(emailResult)
            .Combine(shippingAddressResult);

        if (!combined.TryGetValue(out var values))
            return combined.Error.ToActionResult<CustomerDto>(this);

        var (firstName, lastName, email, address) = values;

        Maybe<PhoneNumber> phone = Maybe<PhoneNumber>.None;
        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            var phoneResult = PhoneNumber.TryCreate(request.PhoneNumber);
            if (!phoneResult.TryGetValue(out var p))
                return phoneResult.Error.ToActionResult<CustomerDto>(this);
            phone = p;
        }

        var command = new CreateCustomerCommand(firstName, lastName, email, phone, address);
        return await sender.Send(command, cancellationToken)
            .MapAsync(MapToDto)
            .ToCreatedAtActionResultAsync(this, nameof(GetCustomer), dto => new { id = dto.Id });
    }

    /// <summary>
    /// Get a customer by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<CustomerDto> GetCustomer(Guid id) =>
        NotFound(new { error = $"Customer '{id}' not found" });

    private static CustomerDto MapToDto(Customer c) => new(
        c.Id.Value,
        c.FirstName.Value,
        c.LastName.Value,
        c.Email.Value,
        c.PhoneNumber.HasValue ? c.PhoneNumber.Value.Value : null,
        new ShippingAddressDto(
            c.ShippingAddress.Street,
            c.ShippingAddress.City,
            c.ShippingAddress.State,
            c.ShippingAddress.PostalCode,
            c.ShippingAddress.Country));
}
