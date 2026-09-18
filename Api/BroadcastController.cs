using Jellyfin.Plugin.BroadcastBox.Models;
using Jellyfin.Plugin.BroadcastBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.BroadcastBox.Api;

/// <summary>Administrator-only endpoints for Broadcast Box publishing.</summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("BroadcastBox")]
public sealed class BroadcastController : ControllerBase
{
    private static readonly Action<ILogger, Exception?> LogCapabilitiesRequested = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1, "CapabilitiesRequested"),
        "Broadcast Box capability check requested");
    private static readonly Action<ILogger, BroadcastSessionStatus, Exception?> LogSessionRequested = LoggerMessage.Define<BroadcastSessionStatus>(
        LogLevel.Information,
        new EventId(2, "SessionRequested"),
        "Broadcast Box session status requested; state is {Status}");
    private static readonly Action<ILogger, Guid, BroadcastQualityPreset, Exception?> LogStartRequested = LoggerMessage.Define<Guid, BroadcastQualityPreset>(
        LogLevel.Information,
        new EventId(3, "StartRequested"),
        "Broadcast Box start requested for item {ItemId} with preset {Preset}");
    private static readonly Action<ILogger, string, Exception?> LogStartRejected = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(4, "StartRejected"),
        "Broadcast Box start rejected: {Reason}");
    private static readonly Action<ILogger, Exception?> LogStartFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5, "StartFailed"),
        "Broadcast Box start failed unexpectedly");
    private static readonly Action<ILogger, Exception?> LogStopRequested = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(6, "StopRequested"),
        "Broadcast Box stop requested");

    private readonly BroadcastSessionManager _sessionManager;
    private readonly FfmpegCapabilityProbe _capabilityProbe;
    private readonly ILogger<BroadcastController> _logger;

    /// <summary>Initializes a new instance of the <see cref="BroadcastController"/> class.</summary>
    public BroadcastController(
        BroadcastSessionManager sessionManager,
        FfmpegCapabilityProbe capabilityProbe,
        ILogger<BroadcastController> logger)
    {
        _sessionManager = sessionManager;
        _capabilityProbe = capabilityProbe;
        _logger = logger;
    }

    /// <summary>Gets the configured FFmpeg WHIP publishing capabilities.</summary>
    [HttpGet("capabilities")]
    [ProducesResponseType<FfmpegCapabilities>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FfmpegCapabilities>> GetCapabilities(CancellationToken cancellationToken)
    {
        LogCapabilitiesRequested(_logger, null);
        return Ok(await _capabilityProbe.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Gets safe state for the current publishing session.</summary>
    [HttpGet("session")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status200OK)]
    public ActionResult<BroadcastSessionSnapshot> GetSession()
    {
        var snapshot = _sessionManager.GetSnapshot();
        LogSessionRequested(_logger, snapshot.Status, null);
        return Ok(snapshot);
    }

    /// <summary>Starts publishing a selected local library item.</summary>
    [HttpPost("session")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BroadcastSessionSnapshot>> Start(
        [FromBody] StartBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        LogStartRequested(_logger, request.ItemId, request.Preset, null);
        try
        {
            var session = await _sessionManager.StartAsync(request, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetSession), session);
        }
        catch (InvalidOperationException exception)
        {
            LogStartRejected(_logger, exception.Message, null);
            return Conflict(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (ArgumentException exception)
        {
            LogStartRejected(_logger, exception.Message, null);
            return BadRequest(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
        catch (Exception exception)
        {
            LogStartFailed(_logger, exception);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, detail: "Broadcast Box failed to start; see the Jellyfin server log.");
        }
    }

    /// <summary>Pauses or resumes the active publisher for an item.</summary>
    [HttpPost("items/{itemId}/pause")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BroadcastSessionSnapshot>> Pause(Guid itemId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _sessionManager.TogglePauseAsync(itemId, cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException exception)
        {
            LogStartRejected(_logger, exception.Message, null);
            return Conflict(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
    }

    /// <summary>Stops the active publisher.</summary>
    [HttpDelete("session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    {
        LogStopRequested(_logger, null);
        await _sessionManager.StopAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
