using System.Diagnostics;
using Timer = System.Windows.Forms.Timer;

namespace PoproshaykaBot.WinForms;

public static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var memoryCheckTimer = new Timer();

        const long ThresholdMb = 1024;
        const long MemoryThreshold = 1024 * 1024 * ThresholdMb;
        const int Interval = 5000;

        memoryCheckTimer.Interval = Interval;
        memoryCheckTimer.Tick += (_, _) =>
        {
            var currentProcess = Process.GetCurrentProcess();
            var memoryBytes = currentProcess.PrivateMemorySize64;

            if (memoryBytes <= MemoryThreshold)
            {
                return;
            }

            memoryCheckTimer.Stop();

            Task.Run(async () =>
            {
                await Task.Delay(Interval);
                currentProcess.Kill();
            });

            var memoryMb = memoryBytes / (1024.0 * 1024.0);
            var message =
                $"""
                 Внимание! Обнаружено аномальное потребление ресурсов.

                 Текущее использование: {memoryMb:F2} MB
                 Установленный лимит: {ThresholdMb:F2} MB

                 У вас есть {Interval / 1000} секунд, чтобы прочитать это сообщение, затем процесс будет убит принудительно.
                 """;

            MessageBox.Show(message, "Критическая нагрузка на память", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Environment.FailFast("Пользователь подтвердил закрытие при утечке памяти.");
        };

        memoryCheckTimer.Start();

        using var composition = new Composition();

        var settingsManager = composition.SettingsManager;
        var statistics = composition.StatisticsCollector;

        var twitchSettings = settingsManager.Current.Twitch;
        var httpServerEnabled = twitchSettings.HttpServerEnabled;

        if (httpServerEnabled)
        {
            var portValidator = composition.PortValidator;
            var portValidationPassed = portValidator.ValidateAndResolvePortConflict();

            if (portValidationPassed)
            {
                try
                {
                    var httpServer = composition.UnifiedHttpServer;
                    httpServer.StartAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка запуска HTTP сервера: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Не удалось разрешить конфликт портов. HTTP сервер не запущен.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        statistics.LoadStatisticsAsync().GetAwaiter().GetResult();

        Application.Run(composition.MainForm);
    }
}
