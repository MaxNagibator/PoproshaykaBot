using KeepShell.Services;
using KeepShell.Services.Modal;
using KeepShell.Services.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using System.IO;

namespace PoproshaykaBot.Wpf.Tests.Tiles;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ChatBlockersEditTests
{
    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("chat-blockers-edit");
        _blockersPath = Path.Combine(_directory.FullName, "chat-blockers.txt");
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Delete(recursive: true);
    }

    private const string Selectors = ".banner\n.promo";

    private DirectoryInfo _directory = null!;
    private string _blockersPath = null!;

    [Test]
    public async Task Правила_на_сорвавшемся_чтении_не_открываются_пустыми_и_не_перезаписываются()
    {
        File.WriteAllText(_blockersPath, Selectors);
        var fileLock = new FileStream(_blockersPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var dialogs = new FakeDialogService(() => fileLock.Dispose());
        var tile = CreateTile(dialogs);

        try
        {
            await tile.EditBlockersCommand.ExecuteAsync(null);
        }
        finally
        {
            fileLock.Dispose();
            tile.Dispose();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dialogs.Shown, Is.Zero,
                "Пустой редактор на месте непрочитанных правил – это и есть путь, которым их стирало первое же сохранение.");

            Assert.That(dialogs.Errors, Has.Count.EqualTo(1), "Пользователю говорят, почему правила не открылись.");
            Assert.That(File.ReadAllText(_blockersPath), Is.EqualTo(Selectors));
        }
    }

    private ChatDisplayTileViewModel CreateTile(IDialogService dialogs)
    {
        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, Path.Combine(_directory.FullName, "settings.json"));

        return new(settings,
            new FakeTargetChannelProvider(),
            NullLogger<ChatDisplayTileViewModel>.Instance,
            new FakeShellLauncher(),
            dialogs,
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            new ChatDisplayStore(NullLogger<ChatDisplayStore>.Instance, _directory.FullName));
    }

    private sealed class FakeDialogService(Action onShow) : IDialogService
    {
        public int Shown { get; private set; }

        public List<string> Errors { get; } = [];

        public Task<bool> ShowAsync(IDialogViewModel viewModel)
        {
            Shown++;
            onShow();
            return Task.FromResult(true);
        }

        public Task<bool> ReplaceAsync(IDialogViewModel viewModel) => ShowAsync(viewModel);

        public bool Confirm(string title, string message, bool defaultYes = false) => true;

        public bool ConfirmWarning(string title, string message, bool defaultYes = false) => true;

        public void Info(string title, string message)
        {
        }

        public void Warning(string title, string message)
        {
        }

        public void Error(string title, string message) => Errors.Add(message);
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
