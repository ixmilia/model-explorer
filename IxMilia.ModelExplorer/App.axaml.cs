using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using IxMilia.ModelExplorer.ViewModels;
using IxMilia.ModelExplorer.Views;

namespace IxMilia.ModelExplorer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var interactionManager = new UserInteractionManager();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.MainView.Renderer.InteractionManager = interactionManager;
            var mainViewModel = new MainViewModel(mainWindow, interactionManager);
            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var mainView = new MainView();
            mainView.Renderer.InteractionManager = interactionManager;
            var mainViewModel = new MainViewModel(mainView, interactionManager);
            singleViewPlatform.MainView = mainView;
            mainView.DataContext = mainViewModel;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
