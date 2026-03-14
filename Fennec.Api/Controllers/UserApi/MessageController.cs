using Fennec.Api.Commands;
using Fennec.Api.Models;
using Fennec.Api.Queries;
using Fennec.Api.Security;
using Fennec.Shared.Dtos.Server;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fennec.Api.Controllers.UserApi;

[ApiController]
[UserAuth]
[Route("api/v1/servers/{serverId:guid}/channels/{channelId:guid}/messages")]
public class MessageController : UserControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendMessage(
        Guid serverId,
        Guid channelId,
        [FromBody] SendMessageRequestDto requestDto,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var response = await mediator.Send(new SendMessageCommand
        {
            ChannelId = channelId,
            Content = requestDto.Content,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        return Ok(new SendMessageResponseDto
        {
            MessageId = response.MessageId,
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListMessages(
        Guid serverId,
        Guid channelId,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var src = await mediator.Send(new ListMessagesQuery
        {
            ChannelId = channelId,
            AuthPrincipal = AuthPrincipal,
        }, cancellationToken);

        var items = await src.ToListAsync(cancellationToken);

        return Ok(new ListMessagesResponseDto
        {
            Messages = items.Select(m =>
            {
                var content = m.Type == MessageType.Text
                    ? m.Details.RootElement.GetProperty("content").GetString() ?? ""
                    : "";

                return new ListMessagesResponseItemDto
                {
                    MessageId = m.MessageId,
                    Content = content,
                    AuthorId = m.AuthorId,
                    AuthorName = m.AuthorName,
                    AuthorInstanceUrl = m.AuthorInstanceUrl,
                    CreatedAt = m.CreatedAt,
                };
            }).ToList(),
        });
    }
}
