using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class ColorPickerDialogViewModel : ObservableObject, IDialogViewModel, IAcceptableDialog
{
    private bool _syncing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    private byte _alpha;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    private byte _red;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    private byte _green;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewColor))]
    [NotifyPropertyChangedFor(nameof(SelectedColor))]
    private byte _blue;

    [ObservableProperty]
    private string _hexText = "#FF000000";

    public ColorPickerDialogViewModel()
        : this(DrawingColor.Black, null)
    {
    }

    public ColorPickerDialogViewModel(DrawingColor initialColor, string? title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Выбор цвета" : title!;
        _alpha = initialColor.A;
        _red = initialColor.R;
        _green = initialColor.G;
        _blue = initialColor.B;
        _hexText = ToHex(initialColor.A, initialColor.R, initialColor.G, initialColor.B);
    }

    public event EventHandler<bool>? RequestClose;

    public string Title { get; }

    public MediaColor PreviewColor => MediaColor.FromArgb(Alpha, Red, Green, Blue);

    public DrawingColor SelectedColor => DrawingColor.FromArgb(Alpha, Red, Green, Blue);

    public void Load(DrawingColor color)
    {
        _syncing = true;
        Alpha = color.A;
        Red = color.R;
        Green = color.G;
        Blue = color.B;
        HexText = ToHex(color.A, color.R, color.G, color.B);
        _syncing = false;
    }

    public bool TryAccept()
    {
        RequestClose?.Invoke(this, true);
        return true;
    }

    partial void OnAlphaChanged(byte value)
    {
        SyncHexFromChannels();
    }

    partial void OnRedChanged(byte value)
    {
        SyncHexFromChannels();
    }

    partial void OnGreenChanged(byte value)
    {
        SyncHexFromChannels();
    }

    partial void OnBlueChanged(byte value)
    {
        SyncHexFromChannels();
    }

    private void SyncHexFromChannels()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        HexText = ToHex(Alpha, Red, Green, Blue);
        _syncing = false;
    }

    partial void OnHexTextChanged(string value)
    {
        if (_syncing)
        {
            return;
        }

        if (!TryParseHex(value, out var a, out var r, out var g, out var b))
        {
            return;
        }

        _syncing = true;
        Alpha = a;
        Red = r;
        Green = g;
        Blue = b;
        _syncing = false;
    }

    [RelayCommand]
    private void Confirm()
    {
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    private static string ToHex(byte a, byte r, byte g, byte b)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{a:X2}{r:X2}{g:X2}{b:X2}");
    }

    private static bool TryParseHex(string? text, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255;
        r = 0;
        g = 0;
        b = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim().AsSpan();

        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length == 6)
        {
            return TryByte(span.Slice(0, 2), out r)
                && TryByte(span.Slice(2, 2), out g)
                && TryByte(span.Slice(4, 2), out b);
        }

        if (span.Length == 8)
        {
            return TryByte(span.Slice(0, 2), out a)
                && TryByte(span.Slice(2, 2), out r)
                && TryByte(span.Slice(4, 2), out g)
                && TryByte(span.Slice(6, 2), out b);
        }

        return false;
    }

    private static bool TryByte(ReadOnlySpan<char> span, out byte value)
    {
        return byte.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
