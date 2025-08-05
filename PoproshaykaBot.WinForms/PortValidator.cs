using PoproshaykaBot.WinForms.Settings;

namespace PoproshaykaBot.WinForms;

public enum PortConflictResolution
{
    UseRedirectPort,
    UpdateRedirectUri,
    OpenSettings,
}

public static class PortValidator
{
    public static bool ValidateAndResolvePortConflictAsync(AppSettings settings)
    {
        var redirectUri = settings.Twitch.RedirectUri;
        var serverPort = settings.Twitch.HttpServerPort;

        if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) == false)
        {
            MessageBox.Show($"Некорректный RedirectUri: {redirectUri}\n\nПожалуйста, исправьте URI в настройках OAuth.",
                "Ошибка конфигурации",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return false;
        }

        var redirectPort = uri.Port == -1 ? uri.Scheme == "https" ? 443 : 80 : uri.Port;

        if (redirectPort == serverPort)
        {
            return true;
        }

        var result = ShowPortConflictDialog(redirectPort, serverPort);

        switch (result)
        {
            case PortConflictResolution.UseRedirectPort:
                settings.Twitch.HttpServerPort = redirectPort;
                break;

            case PortConflictResolution.UpdateRedirectUri:
                settings.Twitch.RedirectUri = $"{uri.Scheme}://localhost:{serverPort}";
                break;

            case PortConflictResolution.OpenSettings:
                return false;
        }

        SettingsManager.SaveSettings(settings);
        return true;
    }

    private static PortConflictResolution ShowPortConflictDialog(int redirectPort, int serverPort)
    {
        var message = $"""
                       Обнаружен конфликт портов:

                       • RedirectUri использует порт: {redirectPort}
                       • HTTP сервер настроен на порт: {serverPort}

                       Для корректной работы OAuth авторизации и HTTP сервера необходимо использовать один и тот же порт.

                       Нажмите:
                       • ДА - использовать порт {redirectPort} для HTTP сервера
                       • НЕТ - обновить RedirectUri на порт {serverPort}
                       • ОТМЕНА - открыть настройки для ручного исправления
                       """;

        var result = MessageBox.Show(message,
            "Конфликт портов",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1);

        return result switch
        {
            DialogResult.Yes => PortConflictResolution.UseRedirectPort,
            DialogResult.No => PortConflictResolution.UpdateRedirectUri,
            _ => PortConflictResolution.OpenSettings,
        };
    }
}
