using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Twitch.Auth;
using PoproshaykaBot.Wpf.Infrastructure;

namespace PoproshaykaBot.Wpf.ViewModels.Onboarding;

public sealed class BotAuthorizationPageViewModel : AuthorizationPageViewModelBase
{
    public BotAuthorizationPageViewModel(
        ITwitchOAuthService oauthService,
        SettingsManager settingsManager,
        ILogger<BotAuthorizationPageViewModel> logger,
        IShellLauncher shellLauncher,
        IClipboardService clipboard,
        IEmbeddedTwitchAuthDialog embeddedAuth,
        IUiDispatcher uiDispatcher)
        : base(TwitchOAuthRole.Bot, oauthService, settingsManager, logger, shellLauncher, clipboard, embeddedAuth, uiDispatcher)
    {
    }
}
