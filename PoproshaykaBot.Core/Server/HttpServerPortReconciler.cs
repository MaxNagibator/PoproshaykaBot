using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Core.Server;

public static class HttpServerPortReconciler
{
    public static PortReconcileResult Reconcile(SettingsManager settingsManager, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsManager);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = settingsManager.Current;
        var redirectUri = settings.Twitch.RedirectUri;
        var serverPort = settings.Twitch.HttpServerPort;

        if (!RedirectUriPortResolver.TryResolve(redirectUri, out var redirectPort))
        {
            logger.LogError("Некорректный RedirectUri: {RedirectUri}", redirectUri);

            var failure = new PortReconcileNotice("Ошибка конфигурации",
                $"Некорректный RedirectUri: {redirectUri}\n\nПожалуйста, исправьте URI в настройках OAuth.",
                PortReconcileSeverity.Error);

            return new(false, failure);
        }

        if (redirectPort == serverPort)
        {
            return new(true, null);
        }

        logger.LogInformation("Конфликт портов. Обновление порта с {OldPort} на {NewPort}", serverPort, redirectPort);
        var written = settingsManager.Mutate(current => current.Twitch.HttpServerPort = redirectPort);

        var conflict = $"""
                        Обнаружен конфликт портов:

                        • RedirectUri использует порт: {redirectPort}
                        • HTTP сервер был настроен на порт: {serverPort}
                        """;

        if (!written)
        {
            logger.LogWarning("Новый порт {NewPort} принят только в памяти: файл настроек переписан снаружи", redirectPort);

            var pending = $"""
                           {conflict}

                           Порт HTTP сервера переключён на {redirectPort} и работает до перезапуска. В файл он не записан: туда только что перенесены данные предыдущей версии, поэтому после перезапуска конфликт вернётся.

                           Перезапустите приложение и задайте порт HTTP сервера и RedirectUri вручную в настройках.
                           """;

            return new(true, new("Порт обновлен до перезапуска", pending, PortReconcileSeverity.Warning));
        }

        var message = $"""
                       {conflict}

                       Для корректной работы OAuth порт HTTP сервера был автоматически обновлен до {redirectPort}.

                       Если вы хотите использовать другой порт, пожалуйста, измените его вручную в настройках HTTP сервера и RedirectUri.
                       """;

        return new(true, new("Порт обновлен", message, PortReconcileSeverity.Information));
    }
}
