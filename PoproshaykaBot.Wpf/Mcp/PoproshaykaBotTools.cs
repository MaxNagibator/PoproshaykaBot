using KeepShell.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace PoproshaykaBot.Wpf.Mcp;

[McpServerToolType]
public sealed class PoproshaykaBotTools
{
    private PoproshaykaBotTools()
    {
    }

    [McpServerTool(Name = "get_bot_state")]
    [Description("Доменное состояние PoproshaykaBot: версия сборки, фаза подключения бота, целевой канал (свой или отладочный, разрешена ли отправка), статус стрима с названием, категорией, зрителями и временем начала, HTTP-сервер оверлея с портом и адресом, подключение к OBS. Звать первым – по этому ответу видно, работает бот или стоит, и есть ли смысл дальше ходить по экранам. Данные читаются в обход окна, поэтому ответ не зависит от открытой страницы.")]
    public static string GetBotState(BotAutomation automation)
    {
        ArgumentNullException.ThrowIfNull(automation);

        return McpFormat.Serialize(automation.GetState());
    }

    [McpServerTool(Name = "connect_bot")]
    [Description("Подключает бота к чату – тот же путь, что кнопка подключения в оболочке, без диалогов подтверждения. Подключение идёт в фоне: ответ несёт фазу сразу после запроса, дождаться Connected можно повторным get_bot_state. Отказывает, если бот уже подключается, подключён или отключается.")]
    public static string ConnectBot(BotAutomation automation, McpPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(automation);
        McpGuards.RequireMutations(preferences, "connect_bot");

        try
        {
            return McpFormat.Serialize(new { accepted = true, phase = automation.Connect() });
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }

    [McpServerTool(Name = "disconnect_bot")]
    [Description("Отключает бота от чата – тот же путь, что кнопка отключения в оболочке, без диалогов подтверждения. Ждёт завершения отключения и возвращает фазу после него; идущее подключение отменяет и ждёт отмены (фаза Cancelled). Отказывает, если бот не подключён.")]
    public static async Task<string> DisconnectBotAsync(BotAutomation automation, McpPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(automation);
        McpGuards.RequireMutations(preferences, "disconnect_bot");

        try
        {
            return McpFormat.Serialize(new { accepted = true, phase = await automation.DisconnectAsync() });
        }
        catch (InvalidOperationException exception)
        {
            throw new McpException(exception.Message);
        }
    }
}
