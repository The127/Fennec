using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;
using Fennec.App.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Fennec.App.Routes;

public record FriendsRoute : IRoute
{
    public ObservableObject GetViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<FriendsViewModel>(serviceProvider);
    }
}
