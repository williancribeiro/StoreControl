using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Application.Inventory;
using StoreControl.Domain.Inventory;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/inventory/transfers")]
[Authorize]
public sealed class StockTransfersController(IStockTransferService transferService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<PagedResult<StockTransferSummary>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? originStoreId = null,
        [FromQuery] int? destinationStoreId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await transferService.ListAsync(
            User.GetRequiredUserId(),
            new StockTransferListQuery(
                page,
                pageSize,
                search,
                originStoreId,
                destinationStoreId,
                ParseStatus(status),
                fromUtc,
                toUtc,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("{stockTransferId:long}")]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<StockTransferDetails>> Get(
        long stockTransferId,
        CancellationToken cancellationToken) =>
        Ok(await transferService.GetAsync(User.GetRequiredUserId(), stockTransferId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    [ProducesResponseType<StockTransferOperationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StockTransferOperationResult>> Create(
        CreateStockTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await transferService.CreateAsync(
            User.GetRequiredUserId(),
            new CreateStockTransferCommand(
                ParseOperationId(idempotencyKey),
                request.OriginStoreId,
                request.DestinationStoreId,
                request.Notes,
                request.Items.Select(item =>
                    new CreateStockTransferItemCommand(item.ProductId, item.Quantity)).ToArray()),
            cancellationToken);
        return result.WasAlreadyProcessed
            ? Ok(result)
            : CreatedAtAction(nameof(Get), new { stockTransferId = result.Transfer.Id }, result);
    }

    [HttpPost("{stockTransferId:long}/dispatch")]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    public async Task<ActionResult<StockTransferOperationResult>> Dispatch(
        long stockTransferId,
        StockTransferTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Ok(await transferService.DispatchAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            stockTransferId,
            new StockTransferOperationCommand(ParseOperationId(idempotencyKey), request.RowVersion),
            cancellationToken));

    [HttpPost("{stockTransferId:long}/receive")]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    public async Task<ActionResult<StockTransferOperationResult>> Receive(
        long stockTransferId,
        StockTransferTransitionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Ok(await transferService.ReceiveAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            stockTransferId,
            new StockTransferOperationCommand(ParseOperationId(idempotencyKey), request.RowVersion),
            cancellationToken));

    [HttpPost("{stockTransferId:long}/cancel")]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    public async Task<ActionResult<StockTransferOperationResult>> Cancel(
        long stockTransferId,
        CancelStockTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        Ok(await transferService.CancelAsync(
            User.GetRequiredUserId(),
            stockTransferId,
            new CancelStockTransferCommand(
                ParseOperationId(idempotencyKey),
                request.RowVersion,
                request.Reason),
            cancellationToken));

    private static Guid ParseOperationId(string? value) =>
        Guid.TryParse(value, out var operationId) && operationId != Guid.Empty
            ? operationId
            : throw new RequestValidationException(
                "Idempotency-Key",
                "Informe um UUID válido no cabeçalho Idempotency-Key.");

    private static StockTransferStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<StockTransferStatus>(value, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new RequestValidationException("status", "O status informado é inválido.");
    }
}

public sealed record CreateStockTransferRequest(
    [Range(1, int.MaxValue)] int OriginStoreId,
    [Range(1, int.MaxValue)] int DestinationStoreId,
    [MaxLength(1000)] string? Notes,
    [Required, MinLength(1), MaxLength(100)] IReadOnlyCollection<CreateStockTransferItemRequest> Items);

public sealed record CreateStockTransferItemRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal Quantity);

public sealed record StockTransferTransitionRequest([Required] string RowVersion);

public sealed record CancelStockTransferRequest(
    [Required] string RowVersion,
    [Required, MaxLength(500)] string Reason);
