using Microsoft.Extensions.DependencyInjection;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat.Display;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;

namespace PoproshaykaBot.Wpf.Bootstrap;

public sealed record GalleryDialogCase(
    string Key,
    string Page,
    string? SettingsSection,
    Func<IServiceProvider, IDialogViewModel> Create);

public static class GalleryDialogs
{
    public const string Kind = "dialog";
    public const string KeyPrefix = "dialog:";

    public const string BroadcastProfile = KeyPrefix + "broadcast-profile";
    public const string PollProfile = KeyPrefix + "poll-profile";
    public const string PollFromProfile = KeyPrefix + "poll-from-profile";
    public const string PointTerm = KeyPrefix + "point-term";
    public const string ChatBlockers = KeyPrefix + "chat-blockers";
    public const string ColorPicker = KeyPrefix + "color-picker";

    // TODO: мастера первого запуска в матрице нет – он отдельное окно, а кадр снимается с
    //  IGalleryHost.Window; заводить кейс, когда галерея каркаса примет произвольный Visual
    public static IReadOnlyList<GalleryDialogCase> All { get; } =
    [
        new(BroadcastProfile, SectionKeys.Overview, null, CreateBroadcastProfile),
        new(PollProfile, SectionKeys.Overview, null, CreatePollProfile),
        new(PollFromProfile, SectionKeys.Overview, null, CreatePollFromProfile),
        new(PointTerm, SectionKeys.Users, null, CreatePointTerm),
        new(ChatBlockers, SectionKeys.Overview, null, CreateChatBlockers),
        new(ColorPicker, SectionKeys.Settings, "obs", CreateColorPicker),
    ];

    public static bool IsDialogKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static GalleryDialogCase? Find(string key)
    {
        return All.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static IDialogViewModel CreateBroadcastProfile(IServiceProvider services)
    {
        var dialog = services.GetRequiredService<BroadcastProfileEditDialogViewModel>();

        dialog.LoadFrom(services.GetRequiredService<BroadcastProfilesStore>().Load().Profiles.FirstOrDefault() ?? new BroadcastProfile());
        dialog.ConfigureCurrentSettingsMode();

        return dialog;
    }

    private static IDialogViewModel CreatePollProfile(IServiceProvider services)
    {
        var profiles = services.GetRequiredService<PollProfilesManager>();
        var dialog = new PollProfileEditDialogViewModel(profiles);

        if (profiles.GetAll().FirstOrDefault() is { } profile)
        {
            dialog.Load(profile);
        }

        return dialog;
    }

    private static IDialogViewModel CreatePollFromProfile(IServiceProvider services)
    {
        return new PollFromProfileDialogViewModel(
            services.GetRequiredService<PollProfilesManager>(),
            services.GetRequiredService<IDialogService>());
    }

    private static IDialogViewModel CreatePointTerm(IServiceProvider services)
    {
        return new PointTermDialogViewModel(services.GetRequiredService<SettingsManager>().Current.Ranks.PointTerm);
    }

    private static IDialogViewModel CreateChatBlockers(IServiceProvider services)
    {
        return new ChatBlockersDialogViewModel(services.GetRequiredService<ChatDisplayStore>().LoadBlockersText());
    }

    private static IDialogViewModel CreateColorPicker(IServiceProvider services)
    {
        return new ColorPickerDialogViewModel(services.GetRequiredService<ObsChatStore>().Load().TextColor, "Цвет текста");
    }
}
