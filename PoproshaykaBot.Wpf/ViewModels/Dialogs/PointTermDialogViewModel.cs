using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Users;

namespace PoproshaykaBot.Wpf.ViewModels.Dialogs;

public sealed partial class PointTermDialogViewModel : ObservableObject, IDialogViewModel, IAcceptableDialog
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string _singular = "балл";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string _few = "балла";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private string _many = "баллов";

    public PointTermDialogViewModel()
    {
        Title = "Склонение формы балла";
    }

    public PointTermDialogViewModel(PointTerm initial) : this()
    {
        Load(initial);
    }

    public event EventHandler<bool>? RequestClose;

    public string Title { get; }

    public string Preview
    {
        get
        {
            var sample = BuildResult();
            return $"1 {sample.ForCount(1)} · 3 {sample.ForCount(3)} · 7 {sample.ForCount(7)} · 21 {sample.ForCount(21)}";
        }
    }

    public PointTerm BuildResult()
    {
        return new()
        {
            Singular = Sanitize(Singular, "балл"),
            Few = Sanitize(Few, "балла"),
            Many = Sanitize(Many, "баллов"),
        };
    }

    public void Load(PointTerm initial)
    {
        Singular = initial.Singular;
        Few = initial.Few;
        Many = initial.Many;
    }

    public bool TryAccept()
    {
        RequestClose?.Invoke(this, true);
        return true;
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = new PointTerm();
        Singular = defaults.Singular;
        Few = defaults.Few;
        Many = defaults.Many;
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

    private static string Sanitize(string? input, string fallback)
    {
        var trimmed = input?.Trim();
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
    }
}
