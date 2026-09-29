using Microsoft.Extensions.DependencyInjection;

namespace PoproshaykaBot.WinForms.Infrastructure.Di;

public sealed class FormFactory(IServiceScopeFactory scopeFactory) : IFormFactory
{
    public T Create<T>() where T : Form
    {
        var scope = scopeFactory.CreateScope();
        var form = scope.ServiceProvider.GetRequiredService<T>();

        scope.ServiceProvider.HydrateDescendants(form);

        form.Disposed += (_, _) => scope.Dispose();

        OffScreenForm.PrepareIfHeadless(form);

        return form;
    }
}
