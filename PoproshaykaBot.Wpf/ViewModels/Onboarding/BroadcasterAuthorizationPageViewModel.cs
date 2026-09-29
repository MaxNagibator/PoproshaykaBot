using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed class BroadcasterAuthorizationPageViewModel : AuthorizationPageViewModelBase
{
    public BroadcasterAuthorizationPageViewModel(
        ITwitchOAuthService oauthService,
        SettingsManager settingsManager,
        ILogger<BroadcasterAuthorizationPageViewModel> logger,
        IShellLauncher shellLauncher,
        IClipboardService clipboard,
        IEmbeddedTwitchAuthDialog embeddedAuth,
        IUiDispatcher uiDispatcher)
        : base(TwitchOAuthRole.Broadcaster, oauthService, settingsManager, logger, shellLauncher, clipboard, embeddedAuth, uiDispatcher)
    {
    }
}
