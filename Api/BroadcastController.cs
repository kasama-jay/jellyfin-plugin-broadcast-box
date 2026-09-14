using Jellyfin.Plugin.BroadcastBox.Models;
using Jellyfin.Plugin.BroadcastBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.BroadcastBox.Api;

/// <summary>Administrator-only endpoints for Broadcast Box publishing.</summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("BroadcastBox")]
public sealed class BroadcastController : ControllerBase
{
    private readonly BroadcastSessionManager _sessionManager;
    private readonly FfmpegCapabilityProbe _capabilityProbe;

    /// <summary>Initializes a new instance of the <see cref="BroadcastController"/> class.</summary>
    public BroadcastController(BroadcastSessionManager sessionManager, FfmpegCapabilityProbe capabilityProbe)
    {
        _sessionManager = sessionManager;
        _capabilityProbe = capabilityProbe;
    }

    /// <summary>Gets the configured FFmpeg WHIP publishing capabilities.</summary>
    [HttpGet("capabilities")]
    [ProducesResponseType<FfmpegCapabilities>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FfmpegCapabilities>> GetCapabilities(CancellationToken cancellationToken)
        => Ok(await _capabilityProbe.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Gets safe state for the current publishing session.</summary>
    [HttpGet("session")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status200OK)]
    public ActionResult<BroadcastSessionSnapshot> GetSession() => Ok(_sessionManager.GetSnapshot());

    /// <summary>Starts publishing a selected local library item.</summary>
    [HttpPost("session")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BroadcastSessionSnapshot>> Start(
        [FromBody] StartBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await _sessionManager.StartAsync(request, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetSession), session);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    /// <summary>Stops the active publisher.</summary>
    [HttpDelete("session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    {
        await _sessionManager.StopAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
