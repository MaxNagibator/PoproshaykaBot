using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Chat.Commands;
using PoproshaykaBot.Core.Settings;
using System.ComponentModel;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed class CommandParametersViewModel : ObservableObject
{
    private static readonly MessageSettings MessageDefaults = new();
    private static readonly SpecialCommandsSettings SpecialDefaults = new();

    private CommandParametersViewModel(string heading, IReadOnlyList<CommandParameterViewModel> items)
    {
        Heading = heading;
        Items = items;

        foreach (var item in items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    public string Heading { get; }

    public IReadOnlyList<CommandParameterViewModel> Items { get; }

    public bool IsDirty => Items.Any(item => item.IsDirty);

    public bool HasErrors => Items.Any(item => item.HasError);

    public static CommandParametersViewModel? TryCreate(IChatCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            DonateCommand => new("Параметры команды", [DonateMessage()]),
            TrumpCommand => new("Параметры команды",
            [
                Coins(),
                PurchasePrice(),
                AllowedUsers(),
                SuccessMessage(),
                UnauthorizedMessage(),
            ]),
            _ => null,
        };
    }

    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var item in Items)
        {
            item.Load(settings);
        }

        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasErrors));
    }

    public bool Validate()
    {
        var failures = Items.Count(item => !item.Validate());

        OnPropertyChanged(nameof(HasErrors));

        return failures == 0;
    }

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var item in Items)
        {
            item.Apply(settings);
        }
    }

    private static CommandParameterViewModel DonateMessage()
    {
        return new("Текст ответа",
            "Тот же текст, что и в настройках: «Основные» – «Шаблоны сообщений»",
            CommandParameterKind.Multiline,
            settings => settings.Twitch.Messages.DonateCommandMessage,
            (settings, text) => settings.Twitch.Messages.DonateCommandMessage =
                TrimOrDefault(text, MessageDefaults.DonateCommandMessage));
    }

    private static CommandParameterViewModel Coins()
    {
        return new("Монет на руках",
            string.Empty,
            CommandParameterKind.Number,
            settings => CommandParameterViewModel.FormatNumber(settings.SpecialCommands.X2IllsonCoins),
            (settings, text) => settings.SpecialCommands.X2IllsonCoins = ParseNumber(text, SpecialDefaults.X2IllsonCoins),
            text => CommandParameterViewModel.TryParseNumber(text, out var value) && value >= 0
                ? null
                : "Введите число не меньше нуля");
    }

    private static CommandParameterViewModel PurchasePrice()
    {
        return new("Цена покупки, $",
            "На неё делится прибыль, поэтому ноль недопустим",
            CommandParameterKind.Number,
            settings => CommandParameterViewModel.FormatNumber(settings.SpecialCommands.X2IllsonPurchasePrice),
            (settings, text) => settings.SpecialCommands.X2IllsonPurchasePrice = ParseNumber(text, SpecialDefaults.X2IllsonPurchasePrice),
            text => CommandParameterViewModel.TryParseNumber(text, out var value) && value > 0
                ? null
                : "Введите число больше нуля");
    }

    private static CommandParameterViewModel AllowedUsers()
    {
        return new("Особый список",
            "По одному логину в строке; остальным команда отвечает отказом",
            CommandParameterKind.List,
            settings => string.Join(Environment.NewLine, settings.SpecialCommands.AllowedUsers),
            (settings, text) => settings.SpecialCommands.AllowedUsers = [.. CommandParameterViewModel.SplitList(text)]);
    }

    private static CommandParameterViewModel SuccessMessage()
    {
        return new("Ответ своим",
            string.Empty,
            CommandParameterKind.Text,
            settings => settings.SpecialCommands.SuccessMessage,
            (settings, text) => settings.SpecialCommands.SuccessMessage =
                TrimOrDefault(text, SpecialDefaults.SuccessMessage));
    }

    private static CommandParameterViewModel UnauthorizedMessage()
    {
        return new("Ответ остальным",
            string.Empty,
            CommandParameterKind.Text,
            settings => settings.SpecialCommands.UnauthorizedMessage,
            (settings, text) => settings.SpecialCommands.UnauthorizedMessage =
                TrimOrDefault(text, SpecialDefaults.UnauthorizedMessage));
    }

    private static string TrimOrDefault(string text, string fallback)
    {
        var trimmed = text.Trim();

        return trimmed.Length > 0 ? trimmed : fallback;
    }

    private static decimal ParseNumber(string text, decimal fallback)
    {
        return CommandParameterViewModel.TryParseNumber(text, out var value) ? value : fallback;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(CommandParameterViewModel.IsDirty), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(IsDirty));
        }

        if (string.Equals(args.PropertyName, nameof(CommandParameterViewModel.HasError), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(HasErrors));
        }
    }
}
