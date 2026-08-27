using Microsoft.AspNetCore.Mvc;
using StoreControl.Application.Abstractions.Persistence;

namespace StoreControl.Api.Controllers;

[ApiController]
[Route("api/smoke")]
public sealed class SmokeController(IDatabaseSmokeCheck databaseSmokeCheck) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SmokeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SmokeResponse>> Get(CancellationToken cancellationToken)
    {
        var database = await databaseSmokeCheck.CheckAsync(cancellationToken);

        return Ok(new SmokeResponse(
            "ok",
            "StoreControl.Api",
            DateTime.UtcNow,
            new DatabaseStatus(
                database.Status,
                database.ServerUtcTime,
                database.FoundationVersion)));
    }
}

public sealed record SmokeResponse(
    string Status,
    string Application,
    DateTime ApiUtcTime,
    DatabaseStatus Database);

public sealed record DatabaseStatus(
    string Status,
    DateTime ServerUtcTime,
    string FoundationVersion);
