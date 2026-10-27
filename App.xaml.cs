using Microsoft.UI.Xaml;

namespace ModoWindows;

public partial class App : Application
{
    // Evita que se abran varios botones flotantes a la vez.
    private static Mutex? s_singleInstance;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        s_singleInstance = new Mutex(initiallyOwned: true, @"Local\ModoWindows.BotonFlotante", out bool createdNew);
        if (!createdNew)
        {
            Exit();
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
