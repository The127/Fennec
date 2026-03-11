using CommunityToolkit.Mvvm.Messaging;
using Fennec.App.Messages;
using Fennec.App.Routing;
using Fennec.App.Services.Auth;
using Fennec.App.ViewModels;
using NSubstitute;

namespace Fennec.App.Tests.ViewModels;

public class MainAppViewModelTests
{
    private readonly IAuthService _authService = Substitute.For<IAuthService>();
    private readonly IMessenger _messenger = new WeakReferenceMessenger();

    private MainAppViewModel CreateViewModel() =>
        new(Substitute.For<IRouter>(), Substitute.For<IAuthStore>(), _messenger, _authService);

    [Fact]
    public async Task Logging_out_calls_the_auth_service()
    {
        var vm = CreateViewModel();

        await vm.LogoutCommand.ExecuteAsync(null);

        await _authService.Received(1).LogoutAsync(Arg.Any<CancellationToken>());
    }
}
