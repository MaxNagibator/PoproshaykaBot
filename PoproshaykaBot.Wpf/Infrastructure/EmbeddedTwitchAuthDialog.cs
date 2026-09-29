using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using PoproshaykaBot.Wpf.Views.Dialogs;
using System.Windows;

namespace PoproshaykaBot.Wpf.Infrastructure;

public sealed class EmbeddedTwitchAuthDialog(
    IServiceScopeFactory scopeFactory,
    ILogger<EmbeddedTwitchAuthDialog> logger) : IEmbeddedTwitchAuthDialog
{
    public Task<EmbeddedTwitchAuthResult> AuthorizeAsync(EmbeddedTwitchAuthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (App.IsHeadless)
        {
            logger.EmbeddedAuthSuppressedHeadless(request.Role);

            return Task.FromResult(EmbeddedTwitchAuthResult.Unavailable(
                "Встроенный вход недоступен в автоматическом режиме."));
        }

        using var scope = scopeFactory.CreateScope();
        var viewModel = scope.ServiceProvider.GetRequiredService<EmbeddedTwitchAuthDialogViewModel>();
        viewModel.Configure(request);

        var window = new EmbeddedTwitchAuthWindow(viewModel)
        {
            Owner = DialogOwner.Resolve(),
        };

        window.ShowDialog();

        return Task.FromResult(viewModel.Result);
    }
}
