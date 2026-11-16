using System.Windows;
using System.Windows.Threading;

namespace DiskTune;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Ocorreu um erro na interface:\n{e.Exception.Message}",
            "DiskTune - Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // Impede que o processo seja encerrado
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"Erro não tratado no aplicativo:\n{ex.Message}",
                "DiskTune - Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
