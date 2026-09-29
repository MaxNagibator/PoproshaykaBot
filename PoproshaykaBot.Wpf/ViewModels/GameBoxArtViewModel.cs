using CommunityToolkit.Mvvm.ComponentModel;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Windows.Media;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed partial class GameBoxArtViewModel : ObservableObject
{
    public static readonly GameBoxArtViewModel None = new(string.Empty);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private ImageSource? _image;

    public GameBoxArtViewModel(string game)
    {
        Game = game;

        AutomationName = game.Length > 0
            ? string.Create(UiCulture.Russian, $"Обложка игры «{game}»")
            : string.Empty;
    }

    public string Game { get; }

    public string AutomationName { get; }

    public bool HasGame => Game.Length > 0;

    public bool HasImage => Image is not null;
}
