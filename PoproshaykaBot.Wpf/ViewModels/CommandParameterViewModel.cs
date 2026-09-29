using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Globalization;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class CommandParameterViewModel : ObservableObject
{
    private readonly Func<AppSettings, string> _read;
    private readonly Action<AppSettings, string> _write;
    private readonly Func<string, string?>? _validate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public CommandParameterViewModel(
        string label,
        string hint,
        CommandParameterKind kind,
        Func<AppSettings, string> read,
        Action<AppSettings, string> write,
        Func<string, string?>? validate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);

        Label = label;
        Hint = hint;
        Kind = kind;

        _read = read;
        _write = write;
        _validate = validate;
    }

    public string Label { get; }

    public string Hint { get; }

    public CommandParameterKind Kind { get; }

    public bool IsMultiline => Kind is CommandParameterKind.Multiline or CommandParameterKind.List;

    public bool HasHint => Hint.Length > 0;

    public bool HasError => Error is not null;

    public string LoadedText { get; private set; } = string.Empty;

    public bool IsDirty => !string.Equals(Text, LoadedText, StringComparison.Ordinal);

    public static string FormatNumber(decimal value)
    {
        return value.ToString(UiCulture.Russian);
    }

    public static bool TryParseNumber(string text, out decimal value)
    {
        var trimmed = text.Trim();

        return decimal.TryParse(trimmed, NumberStyles.Number, UiCulture.Russian, out value)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static IReadOnlyList<string> SplitList(string text)
    {
        return text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Load(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        LoadedText = _read(settings);
        Text = LoadedText;
        Error = null;

        OnPropertyChanged(nameof(IsDirty));
    }

    public bool Validate()
    {
        Error = _validate?.Invoke(Text);

        return Error is null;
    }

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _write(settings, Text);
    }

    partial void OnTextChanged(string value)
    {
        Error = null;
    }
}
