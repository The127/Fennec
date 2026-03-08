using CommunityToolkit.Mvvm.ComponentModel;
using Fennec.App.Routing;

namespace Fennec.App.ViewModels;

public partial class MainViewModel(IServiceProvider serviceProvider) : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly Router _router = new(serviceProvider);
}