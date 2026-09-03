using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StoreControl.Api.Security;
using StoreControl.Application.Common;
using StoreControl.Application.Reporting;
using StoreControl.Domain.Security;

namespace StoreControl.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCodes.ReportsRead)]
public sealed class ReportingController(IReportingService reportingService) : ControllerBase
{
    [HttpGet("api/dashboard")]
    public async Task<ActionResult<DashboardSummary>> Dashboard(
        [FromQuery] int? storeId = null,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default) =>
        Ok(await reportingService.GetDashboardAsync(
            User.GetRequiredUserId(),
            new DashboardQuery(storeId, date),
            cancellationToken));

    [HttpGet("api/reports/sales")]
    public async Task<ActionResult<SalesReport>> Sales(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var period = DefaultPeriod(fromDate, toDate);
        return Ok(await reportingService.GetSalesAsync(
            User.GetRequiredUserId(),
            new SalesReportQuery(period.FromDate, period.ToDate, storeId),
            cancellationToken));
    }

    [HttpGet("api/reports/inventory")]
    public async Task<ActionResult<PagedResult<InventoryReportRow>>> Inventory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? storeId = null,
        [FromQuery] bool? belowMinimum = null,
        [FromQuery] string sortBy = "product",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default) =>
        Ok(await reportingService.GetInventoryAsync(
            User.GetRequiredUserId(),
            new InventoryReportQuery(
                page,
                pageSize,
                search,
                storeId,
                belowMinimum,
                sortBy,
                sortDirection),
            cancellationToken));

    [HttpGet("api/reports/movements")]
    public async Task<ActionResult<MovementReport>> Movements(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var period = DefaultPeriod(fromDate, toDate);
        return Ok(await reportingService.GetMovementsAsync(
            User.GetRequiredUserId(),
            new MovementReportQuery(period.FromDate, period.ToDate, storeId),
            cancellationToken));
    }

    [HttpGet("api/reports/purchases")]
    public async Task<ActionResult<PurchaseReport>> Purchases(
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] int? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var period = DefaultPeriod(fromDate, toDate);
        return Ok(await reportingService.GetPurchasesAsync(
            User.GetRequiredUserId(),
            new PurchaseReportQuery(period.FromDate, period.ToDate, storeId),
            cancellationToken));
    }

    private static (DateOnly FromDate, DateOnly ToDate) DefaultPeriod(
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var defaultTo = DateOnly.FromDateTime(DateTime.UtcNow);
        var selectedTo = toDate ?? defaultTo;
        return (fromDate ?? selectedTo.AddDays(-29), selectedTo);
    }
}
