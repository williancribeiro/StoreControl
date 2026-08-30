using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Purchasing;
using StoreControl.Domain.Purchasing;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
public sealed class PurchasesController(IPurchaseService purchaseService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.PurchasesRead)]
    public async Task<ActionResult<PagedResult<PurchaseOrderSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? storeId = null,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await purchaseService.ListAsync(
            User.GetRequiredUserId(),
            new PurchaseOrderListQuery(
                page,
                pageSize,
                search,
                storeId,
                supplierId,
                ParseStatus(status),
                fromUtc,
                toUtc,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("{purchaseOrderId:long}")]
    [Authorize(Policy = PermissionCodes.PurchasesRead)]
    public async Task<ActionResult<PurchaseOrderDetails>> Get(
        long purchaseOrderId,
        CancellationToken cancellationToken) =>
        Ok(await purchaseService.GetAsync(
            User.GetRequiredUserId(),
            purchaseOrderId,
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [ProducesResponseType<PurchaseOrderDetails>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PurchaseOrderDetails>> Create(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await purchaseService.CreateAsync(
            User.GetRequiredUserId(),
            new CreatePurchaseOrderCommand(
                request.StoreId,
                request.SupplierId,
                request.ExpectedDate,
                request.Notes,
                request.Items
                    .Select(item => new CreatePurchaseOrderItemCommand(
                        item.ProductId,
                        item.Quantity,
                        item.UnitCost))
                    .ToArray()),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { purchaseOrderId = result.Id }, result);
    }

    [HttpPost("{purchaseOrderId:long}/submit")]
    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    public async Task<ActionResult<PurchaseOrderDetails>> Submit(
        long purchaseOrderId,
        PurchaseTransitionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await purchaseService.SubmitAsync(
            User.GetRequiredUserId(),
            purchaseOrderId,
            new PurchaseTransitionCommand(request.RowVersion),
            cancellationToken));

    [HttpPost("{purchaseOrderId:long}/receipts")]
    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    [ProducesResponseType<ReceivePurchaseResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReceivePurchaseResult>> Receive(
        long purchaseOrderId,
        ReceivePurchaseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idempotencyKey, out var receiptId) || receiptId == Guid.Empty)
        {
            throw new RequestValidationException(
                "Idempotency-Key",
                "Informe um UUID válido no cabeçalho Idempotency-Key.");
        }

        var result = await purchaseService.ReceiveAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            purchaseOrderId,
            new ReceivePurchaseCommand(
                receiptId,
                request.RowVersion,
                request.Notes,
                request.Items
                    .Select(item => new ReceivePurchaseItemCommand(
                        item.PurchaseOrderItemId,
                        item.Quantity))
                    .ToArray()),
            cancellationToken);

        return result.WasAlreadyProcessed
            ? Ok(result)
            : Created($"/api/purchases/{purchaseOrderId}#receipt-{receiptId:D}", result);
    }

    [HttpPost("{purchaseOrderId:long}/finalize")]
    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    public async Task<ActionResult<PurchaseOrderDetails>> Finalize(
        long purchaseOrderId,
        PurchaseTransitionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await purchaseService.FinalizeAsync(
            User.GetRequiredUserId(),
            purchaseOrderId,
            new PurchaseTransitionCommand(request.RowVersion),
            cancellationToken));

    [HttpPost("{purchaseOrderId:long}/cancel")]
    [Authorize(Policy = PermissionCodes.PurchasesManage)]
    public async Task<ActionResult<PurchaseOrderDetails>> Cancel(
        long purchaseOrderId,
        CancelPurchaseRequest request,
        CancellationToken cancellationToken) =>
        Ok(await purchaseService.CancelAsync(
            User.GetRequiredUserId(),
            purchaseOrderId,
            new PurchaseTransitionCommand(request.RowVersion, request.Reason),
            cancellationToken));

    private static PurchaseOrderStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<PurchaseOrderStatus>(status, true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new RequestValidationException("status", "O status do pedido informado é inválido.");
    }
}

public sealed record CreatePurchaseRequest(
    [Range(1, int.MaxValue)] int StoreId,
    [Range(1, int.MaxValue)] int SupplierId,
    DateOnly? ExpectedDate,
    [MaxLength(1000)] string? Notes,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<CreatePurchaseItemRequest> Items);

public sealed record CreatePurchaseItemRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal Quantity,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal UnitCost);

public sealed record PurchaseTransitionRequest([Required] string RowVersion);

public sealed record ReceivePurchaseRequest(
    [Required] string RowVersion,
    [MaxLength(500)] string? Notes,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<ReceivePurchaseItemRequest> Items);

public sealed record ReceivePurchaseItemRequest(
    [Range(1, long.MaxValue)] long PurchaseOrderItemId,
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal Quantity);

public sealed record CancelPurchaseRequest(
    [Required] string RowVersion,
    [Required, MaxLength(250)] string Reason);
