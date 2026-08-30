using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Sales;
using StoreControl.Domain.Sales;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public sealed class SalesController(ISaleService saleService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.SalesRead)]
    public async Task<ActionResult<PagedResult<SaleSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? storeId = null,
        [FromQuery] int? customerId = null,
        [FromQuery] string? paymentMethod = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string sortBy = "finalizedAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await saleService.ListAsync(
            User.GetRequiredUserId(),
            new SaleListQuery(
                page,
                pageSize,
                search,
                storeId,
                customerId,
                ParsePaymentMethod(paymentMethod),
                fromUtc,
                toUtc,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("{saleId:long}")]
    [Authorize(Policy = PermissionCodes.SalesRead)]
    public async Task<ActionResult<SaleDetails>> Get(
        long saleId,
        CancellationToken cancellationToken) =>
        Ok(await saleService.GetAsync(User.GetRequiredUserId(), saleId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.SalesManage)]
    [ProducesResponseType<FinalizeSaleResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FinalizeSaleResult>> Finalize(
        FinalizeSaleRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idempotencyKey, out var operationId) || operationId == Guid.Empty)
        {
            throw new RequestValidationException(
                "Idempotency-Key",
                "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var result = await saleService.FinalizeAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            new FinalizeSaleCommand(
                operationId,
                request.StoreId,
                request.CashSessionId,
                request.CustomerId,
                request.DiscountAmount,
                request.Items
                    .Select(item => new FinalizeSaleItemCommand(item.ProductId, item.Quantity))
                    .ToArray(),
                request.Payments
                    .Select(payment => new FinalizeSalePaymentCommand(
                        ParseRequiredPaymentMethod(payment.Method),
                        payment.Amount,
                        payment.Reference))
                    .ToArray()),
            cancellationToken);

        return result.WasAlreadyProcessed
            ? Ok(result)
            : CreatedAtAction(nameof(Get), new { saleId = result.Sale.Id }, result);
    }

    private static PaymentMethod? ParsePaymentMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TryParsePaymentMethod(value, out var method)
            ? method
            : throw new RequestValidationException("paymentMethod", "A forma de pagamento informada é inválida.");
    }

    private static PaymentMethod ParseRequiredPaymentMethod(string value) =>
        TryParsePaymentMethod(value, out var method)
            ? method
            : throw new RequestValidationException("payments.method", "A forma de pagamento informada é inválida.");

    private static bool TryParsePaymentMethod(string? value, out PaymentMethod method) =>
        Enum.TryParse(value, true, out method) && Enum.IsDefined(method);
}

public sealed record FinalizeSaleRequest(
    [Range(1, int.MaxValue)] int StoreId,
    [Range(1, long.MaxValue)] long CashSessionId,
    [Range(1, int.MaxValue)] int? CustomerId,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal DiscountAmount,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<FinalizeSaleItemRequest> Items,
    [Required, MinLength(1), MaxLength(10)] IReadOnlyCollection<FinalizeSalePaymentRequest> Payments);

public sealed record FinalizeSaleItemRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal Quantity);

public sealed record FinalizeSalePaymentRequest(
    [Required, MaxLength(30)] string Method,
    [Range(typeof(decimal), "0.01", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [MaxLength(100)] string? Reference);
