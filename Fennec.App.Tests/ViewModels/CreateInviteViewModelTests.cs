using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Routing;
using Fennec.App.ViewModels;
using Fennec.Client;
using Fennec.Client.Clients;
using Fennec.Shared.Dtos.Server;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShadUI;

namespace Fennec.App.Tests.ViewModels;

public class CreateInviteViewModelTests
{
    private readonly IFennecClient _client = Substitute.For<IFennecClient>();
    private readonly IServerClient _serverClient = Substitute.For<IServerClient>();
    private readonly IRouter _router = Substitute.For<IRouter>();
    private readonly Guid _serverId = Guid.NewGuid();

    public CreateInviteViewModelTests()
    {
        _client.Server.Returns(_serverClient);
        _serverClient.CreateInviteAsync(
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<CreateServerInviteRequestDto>(),
                Arg.Any<CancellationToken>())
            .Returns(new CreateServerInviteResponseDto
            {
                InviteId = Guid.NewGuid(),
                Code = "aBcD1234",
            });
    }

    private CreateInviteViewModel CreateViewModel() =>
        new(_client, _router, new ToastManager(), _serverId, "fennec.chat", NullLogger<CreateInviteViewModel>.Instance);

    [Fact]
    public async Task Creates_invite_and_shows_link()
    {
        var vm = CreateViewModel();

        await vm.CreateInviteCommand.ExecuteAsync(null);

        await _serverClient.Received().CreateInviteAsync(
            "fennec.chat",
            _serverId,
            Arg.Is<CreateServerInviteRequestDto>(r =>
                r.ExpiresAt == null && r.MaxUses == null),
            Arg.Any<CancellationToken>());
        Assert.Equal("https://fennec.chat/invite/aBcD1234", vm.InviteUrl);
        Assert.True(vm.IsCreated);
    }

    [Fact]
    public async Task Passes_max_uses_when_set()
    {
        var vm = CreateViewModel();
        vm.MaxUsesText = "10";

        await vm.CreateInviteCommand.ExecuteAsync(null);

        await _serverClient.Received().CreateInviteAsync(
            "fennec.chat",
            _serverId,
            Arg.Is<CreateServerInviteRequestDto>(r => r.MaxUses == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Passes_expiry_when_set()
    {
        var vm = CreateViewModel();
        vm.SelectedExpiry = "1 hour";

        await vm.CreateInviteCommand.ExecuteAsync(null);

        await _serverClient.Received().CreateInviteAsync(
            "fennec.chat",
            _serverId,
            Arg.Is<CreateServerInviteRequestDto>(r => r.ExpiresAt != null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_max_uses_shows_error()
    {
        var vm = CreateViewModel();
        vm.MaxUsesText = "abc";

        await vm.CreateInviteCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _serverClient.DidNotReceive().CreateInviteAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CreateServerInviteRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Api_failure_shows_error()
    {
        _serverClient.CreateInviteAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CreateServerInviteRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CreateServerInviteResponseDto>(new Exception("Server error")));

        var vm = CreateViewModel();

        await vm.CreateInviteCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        await _router.DidNotReceive().NavigateBackAsync();
    }

    [Fact]
    public async Task Never_expires_sends_null_expiry()
    {
        var vm = CreateViewModel();
        vm.SelectedExpiry = "Never";

        await vm.CreateInviteCommand.ExecuteAsync(null);

        await _serverClient.Received().CreateInviteAsync(
            "fennec.chat",
            _serverId,
            Arg.Is<CreateServerInviteRequestDto>(r => r.ExpiresAt == null),
            Arg.Any<CancellationToken>());
    }
}
