using Fennec.Api.Security;
using Fennec.Api.Services;
using Fennec.Shared.Dtos.Voice;
using Microsoft.AspNetCore.Mvc;

namespace Fennec.Api.Controllers.FederationApi;

[ApiController]
[Route("federation/v1/voice")]
[FederationAuth]
public class FederationVoiceController(
    VoiceStateService voiceState,
    IVoiceEventService voiceEventService
) : FederationControllerBase
{
    [HttpPost("join")]
    public async Task<ActionResult<FederationVoiceJoinResponseDto>> Join([FromBody] FederationVoiceJoinRequestDto request)
    {
        var participants = voiceState.AddParticipant(
            request.ServerId, request.ChannelId,
            request.UserId, request.Username, request.CallerInstanceUrl,
            connectionId: null);

        var participantDto = new VoiceParticipantDto
        {
            UserId = request.UserId,
            Username = request.Username,
            InstanceUrl = request.CallerInstanceUrl
        };
        await voiceEventService.NotifyParticipantJoined(request.ServerId, request.ChannelId, participantDto);

        return Ok(new FederationVoiceJoinResponseDto { Participants = participants });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave([FromBody] FederationVoiceLeaveRequestDto request)
    {
        voiceState.RemoveParticipant(request.ServerId, request.ChannelId, request.UserId);
        await voiceEventService.NotifyParticipantLeft(request.ServerId, request.ChannelId, request.UserId);
        return Ok();
    }

    [HttpPost("participant-joined")]
    public async Task<IActionResult> ParticipantJoined([FromBody] FederationVoiceParticipantEventDto request)
    {
        await voiceEventService.NotifyParticipantJoined(request.ServerId, request.ChannelId, request.Participant);
        return Ok();
    }

    [HttpPost("participant-left")]
    public async Task<IActionResult> ParticipantLeft([FromBody] FederationVoiceParticipantLeftEventDto request)
    {
        await voiceEventService.NotifyParticipantLeft(request.ServerId, request.ChannelId, request.UserId);
        return Ok();
    }

    [HttpGet("state/{serverId:guid}")]
    public IActionResult GetState(Guid serverId)
    {
        var state = voiceState.GetServerVoiceState(serverId);
        return Ok(state);
    }
}
