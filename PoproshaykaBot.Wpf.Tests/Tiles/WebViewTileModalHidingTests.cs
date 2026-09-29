using KeepShell.Services;
using KeepShell.Services.Modal;
using KeepShell.Services.Platform;
using KeepShell.Testing;
using KeepShell.Views.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Wpf;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using PoproshaykaBot.Wpf.Views.Tiles;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public sealed class WebViewTileModalHidingTests
{
    private static readonly string[] Dictionaries =
    [
        "pack://application:,,,/KeepShell;component/Resources/Themes/Light.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Themes/Tokens.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Converters.xaml",
        "pack://application:,,,/KeepShell;component/Resources/Styles/Controls.xaml",
        "pack://application:,,,/PoproshaykaBot.Wpf;component/Infrastructure/Converters/AppConverters.xaml",
    ];

    private DirectoryInfo _directory = null!;
    private IDisposable? _disposable;

    public enum Tile
    {
        None = 0,
        Chat = 1,
        OverlayPreview = 2,
    }

    [OneTimeSetUp]
    public void EnsureApplication()
    {
        TestResources.Ensure(Dictionaries);
    }

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("webview-tile-modal");
    }

    [TearDown]
    public void TearDown()
    {
        _disposable?.Dispose();
        _disposable = null;
        _directory.Delete(recursive: true);
    }

    [TestCase(Tile.Chat, "Чат скрыт")]
    [TestCase(Tile.OverlayPreview, "Превью скрыто")]
    public void Браузер_прячется_под_диалогом_и_возвращается_после_него(Tile tile, string heading)
    {
        var viewModel = CreateViewModel(tile);
        var view = CreateView(tile, viewModel);
        var browser = (WebView2)view.FindName("WebView");
        var placeholder = Placeholder(view, heading);

        viewModel.IsModalDialogOpen = true;
        Layout(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(browser.Visibility, Is.EqualTo(Visibility.Collapsed),
                "HWND браузера рисуется поверх оверлея диалога – под открытым диалогом его быть не должно");
            Assert.That(placeholder.Visibility, Is.EqualTo(Visibility.Visible),
                "На месте спрятанного браузера нужна заглушка, иначе плитка выглядит сломанной");
            Assert.That(placeholder.Description, Does.Contain("диалог"),
                "Заглушка под диалогом обязана назвать причину – диалог, а не настройку дашборда");
        }

        viewModel.IsModalDialogOpen = false;
        Layout(view);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(browser.Visibility, Is.EqualTo(Visibility.Visible),
                "Закрытый диалог обязан вернуть браузер");
            Assert.That(placeholder.Visibility, Is.EqualTo(Visibility.Collapsed),
                "Заглушка после закрытия диалога легла бы поверх вернувшегося браузера");
        }
    }

    [TestCase(Tile.Chat, true)]
    [TestCase(Tile.Chat, false)]
    [TestCase(Tile.OverlayPreview, true)]
    [TestCase(Tile.OverlayPreview, false)]
    public void Диалог_и_режим_правки_вместе_держат_браузер_скрытым(Tile tile, bool dialogClosesFirst)
    {
        var viewModel = CreateViewModel(tile);
        var view = CreateView(tile, viewModel);
        var browser = (WebView2)view.FindName("WebView");

        viewModel.IsLayoutEditing = true;
        viewModel.IsModalDialogOpen = true;
        Layout(view);

        Assert.That(browser.Visibility, Is.EqualTo(Visibility.Collapsed),
            "Диалог поверх режима правки – браузер скрыт");

        if (dialogClosesFirst)
        {
            viewModel.IsModalDialogOpen = false;
        }
        else
        {
            viewModel.IsLayoutEditing = false;
        }

        Layout(view);

        Assert.That(browser.Visibility, Is.EqualTo(Visibility.Collapsed),
            dialogClosesFirst
                ? "Закрытие диалога не должно возвращать браузер, пока идёт правка раскладки"
                : "Выход из правки не должен возвращать браузер, пока открыт диалог");
    }

    private static UserControl CreateView(Tile tile, DashboardTileViewModel viewModel)
    {
        UserControl view = tile switch
        {
            Tile.Chat => new ChatDisplayTileView(),
            Tile.OverlayPreview => new ChatOverlayPreviewTileView(),
            _ => throw new ArgumentOutOfRangeException(nameof(tile)),
        };

        view.DataContext = viewModel;
        Layout(view);

        return view;
    }

    private static EmptyState Placeholder(UserControl view, string heading)
    {
        var grid = (Grid)((FrameworkElement)view.FindName("WebView")).Parent;

        return grid.Children.OfType<EmptyState>().Single(state => state.Heading == heading);
    }

    private static void Layout(UserControl view)
    {
        view.Measure(new(380, 460));
        view.Arrange(new(0, 0, 380, 460));
        view.UpdateLayout();
    }

    private DashboardTileViewModel CreateViewModel(Tile tile)
    {
        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory.FullName, "settings.json"));

        DashboardTileViewModel viewModel = tile switch
        {
            Tile.Chat => new ChatDisplayTileViewModel(settings,
                new FakeTargetChannelProvider(),
                NullLogger<ChatDisplayTileViewModel>.Instance,
                new FakeShellLauncher(),
                new FakeDialogService(),
                new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
                new ChatDisplayStore(NullLogger<ChatDisplayStore>.Instance, _directory.FullName)),
            Tile.OverlayPreview => new ChatOverlayPreviewTileViewModel(settings, NullLogger<ChatOverlayPreviewTileViewModel>.Instance),
            _ => throw new ArgumentOutOfRangeException(nameof(tile)),
        };

        _disposable = viewModel as IDisposable;

        return viewModel;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public Task<bool> ShowAsync(IDialogViewModel viewModel) => Task.FromResult(false);

        public Task<bool> ReplaceAsync(IDialogViewModel viewModel) => Task.FromResult(false);

        public bool Confirm(string title, string message, bool defaultYes = false) => false;

        public bool ConfirmWarning(string title, string message, bool defaultYes = false) => false;

        public void Info(string title, string message)
        {
        }

        public void Warning(string title, string message)
        {
        }

        public void Error(string title, string message)
        {
        }
    }

    private sealed class FakeTargetChannelProvider : ITargetChannelProvider
    {
        public TargetChannelState Current { get; } = new("poproshayka", "poproshayka", false, true);

        public void BeginSession()
        {
        }

        public void EndSession()
        {
        }
    }

    private sealed class FakeShellLauncher : IShellLauncher
    {
        public bool Open(string pathOrUrl) => true;

        public bool Reveal(string path) => true;

        public bool Start(string executable, params string[] arguments) => true;
    }
}
