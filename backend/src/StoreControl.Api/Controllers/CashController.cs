using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Cash;
using StoreControl.Application.Common;
using StoreControl.Application.Common.Exceptions;
using StoreControl.Domain.Cash;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/cash")]
[Authorize]
public sealed class CashController(ICashService cashService) : ControllerBase
{
    [HttpGet("registers")]
    [Authorize(Policy = PermissionCodes.CashRead)]
    public async Task<ActionResult<IReadOnlyCollection<CashRegisterSummary>>> ListRegisters(
        [FromQuery] int? storeId,
        CancellationToken cancellationToken) =>
        Ok(await cashService.ListRegistersAsync(User.GetRequiredUserId(), storeId, cancellationToken));

    [HttpGet("sessions/current")]
    [Authorize(Policy = PermissionCodes.CashRead)]
    public async Task<ActionResult<CashSessionDetails>> GetCurrent(
        [FromQuery, Range(1, int.MaxValue)] int cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await cashService.GetCurrentAsync(
            User.GetRequiredUserId(),
            cashRegisterId,
            cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("sessions")]
    [Authorize(Policy = PermissionCodes.CashRead)]
    public async Task<ActionResult<PagedResult<CashSessionSummary>>> ListSessions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? storeId = null,
        [FromQuery] int? cashRegisterId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string sortBy = "openedAt",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await cashService.ListSessionsAsync(
            User.GetRequiredUserId(),
            new CashSessionListQuery(
                page,
                pageSize,
                storeId,
                cashRegisterId,
                ParseStatus(status),
                fromUtc,
                toUtc,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("sessions/{cashSessionId:long}")]
    [Authorize(Policy = PermissionCodes.CashRead)]
    public async Task<ActionResult<CashSessionDetails>> GetSession(
        long cashSessionId,
        CancellationToken cancellationToken) =>
        Ok(await cashService.GetSessionAsync(
            User.GetRequiredUserId(),
            cashSessionId,
            cancellationToken));

    [HttpPost("sessions")]
    [Authorize(Policy = PermissionCodes.CashManage)]
    [ProducesResponseType<CashOperationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CashOperationResult>> Open(
        OpenCashSessionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var operationId = ParseIdempotencyKey(idempotencyKey);
        var result = await cashService.OpenAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            new OpenCashSessionCommand(operationId, request.CashRegisterId, request.OpeningAmount),
            cancellationToken);

        return result.WasAlreadyProcessed
            ? Ok(result)
            : CreatedAtAction(nameof(GetSession), new { cashSessionId = result.Session.Id }, result);
    }

    [HttpPost("sessions/{cashSessionId:long}/movements")]
    [Authorize(Policy = PermissionCodes.CashManage)]
    public async Task<ActionResult<CashOperationResult>> AddMovement(
        long cashSessionId,
        AddCashMovementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CashMovementType>(request.Type, true, out var type) ||
            type is not (CashMovementType.CashIn or CashMovementType.CashOut))
        {
            throw new RequestValidationException(
                "type",
                "Movimentações manuais devem usar CashIn ou CashOut.");
        }

        return Ok(await cashService.AddMovementAsync(
            User.GetRequiredUserId(),
            HttpContext.TraceIdentifier,
            cashSessionId,
            new AddCashMovementCommand(
                ParseIdempotencyKey(idempotencyKey),
                request.RowVersion,
                type,
                request.Amount,
                request.Reason),
            cancellationToken));
    }

    [HttpPost("sessions/{cashSessionId:long}/close")]
    [Authorize(Policy = PermissionCodes.CashManage)]
    public async Task<ActionResult<CashSessionDetails>> Close(
        long cashSessionId,
        CloseCashSessionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cashService.CloseAsync(
            User.GetRequiredUserId(),
            cashSessionId,
            new CloseCashSessionCommand(request.RowVersion, request.DeclaredClosingAmount),
            cancellationToken));

    private static CashSessionStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<CashSessionStatus>(value, true, out var status) && Enum.IsDefined(status)
            ? status
            : throw new RequestValidationException("status", "O status da sessão informado é inválido.");
    }

    private static Guid ParseIdempotencyKey(string? value) =>
        Guid.TryParse(value, out var operationId) && operationId != Guid.Empty
            ? operationId
            : throw new RequestValidationException(
                "Idempotency-Key",
                "Informe um UUID válido no cabeçalho Idempotency-Key.");
}

public sealed record OpenCashSessionRequest(
    [Range(1, int.MaxValue)] int CashRegisterId,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal OpeningAmount);

public sealed record AddCashMovementRequest(
    [Required] string RowVersion,
    [Required, MaxLength(30)] string Type,
    [Range(typeof(decimal), "0.01", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal Amount,
    [Required, MaxLength(250)] string Reason);

public sealed record CloseCashSessionRequest(
    [Required] string RowVersion,
    [Range(typeof(decimal), "0", "999999999999.99", ParseLimitsInInvariantCulture = true)] decimal DeclaredClosingAmount);
