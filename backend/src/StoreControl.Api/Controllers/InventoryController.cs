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
[Route("api/inventory")]
[Authorize]
public sealed class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet("balances")]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<PagedResult<StockPositionSummary>>> ListPositions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? storeId = null,
        [FromQuery] int? productId = null,
        [FromQuery] bool? belowMinimum = null,
        [FromQuery] string sortBy = "product",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await inventoryService.ListPositionsAsync(
            User.GetRequiredUserId(),
            new StockPositionQuery(
                page,
                pageSize,
                search,
                storeId,
                productId,
                belowMinimum,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("balances/{storeId:int}/{productId:int}")]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<StockPositionSummary>> GetPosition(
        int storeId,
        int productId,
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetPositionAsync(
            User.GetRequiredUserId(),
            storeId,
            productId,
            cancellationToken));

    [HttpPut("balances/{storeId:int}/{productId:int}/minimum")]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    public async Task<ActionResult<StockPositionSummary>> UpdateMinimum(
        int storeId,
        int productId,
        UpdateMinimumStockRequest request,
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.UpdateMinimumAsync(
            User.GetRequiredUserId(),
            storeId,
            productId,
            new UpdateMinimumStockCommand(request.MinimumQuantity, request.RowVersion),
            cancellationToken));

    [HttpGet("movements")]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<PagedResult<StockMovementSummary>>> ListMovements(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? storeId = null,
        [FromQuery] int? productId = null,
        [FromQuery] string? type = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string sortBy = "occurredAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await inventoryService.ListMovementsAsync(
            User.GetRequiredUserId(),
            new StockMovementQuery(
                page,
                pageSize,
                search,
                storeId,
                productId,
                ParseMovementType(type),
                fromUtc,
                toUtc,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("movements/{movementId:long}")]
    [Authorize(Policy = PermissionCodes.InventoryRead)]
    public async Task<ActionResult<StockMovementSummary>> GetMovement(
        long movementId,
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetMovementAsync(
            User.GetRequiredUserId(),
            movementId,
            cancellationToken));

    [HttpPost("adjustments")]
    [Authorize(Policy = PermissionCodes.InventoryManage)]
    [ProducesResponseType<StockAdjustmentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StockAdjustmentResult>> CreateAdjustment(
        CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await inventoryService.AdjustAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            new CreateStockAdjustmentCommand(
                request.StoreId,
                request.ProductId,
                ParseAdjustmentDirection(request.Direction),
                request.Quantity,
                request.Reason,
                request.RowVersion),
            cancellationToken);
        return Created($"/api/inventory/movements/{result.Movement.Id}", result);
    }

    private static StockMovementType ParseAdjustmentDirection(string direction) =>
        direction.Trim().ToLowerInvariant() switch
        {
            "positive" => StockMovementType.PositiveAdjustment,
            "negative" => StockMovementType.NegativeAdjustment,
            _ => throw new RequestValidationException(
                "direction",
                "Use positive ou negative para a direção do ajuste.")
        };

    private static StockMovementType? ParseMovementType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (Enum.TryParse<StockMovementType>(type, true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new RequestValidationException("type", "O tipo de movimentação informado é inválido.");
    }
}

public sealed record CreateStockAdjustmentRequest(
    [Range(1, int.MaxValue)] int StoreId,
    [Range(1, int.MaxValue)] int ProductId,
    [Required, RegularExpression("^(?i:positive|negative)$")] string Direction,
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal Quantity,
    [Required, MaxLength(500)] string Reason,
    string? RowVersion);

public sealed record UpdateMinimumStockRequest(
    [Range(typeof(decimal), "0", "999999999999.999", ParseLimitsInInvariantCulture = true)] decimal MinimumQuantity,
    string? RowVersion);
