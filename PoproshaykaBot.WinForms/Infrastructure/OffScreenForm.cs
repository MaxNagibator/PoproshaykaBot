namespace PoproshaykaBot.WinForms.Infrastructure;

internal static class OffScreenForm
{
    public const int Position = -32000;

    public static void PrepareIfHeadless(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (!Program.IsUiSmoke)
        {
            return;
        }

        if (form.Visible)
        {
            throw new ArgumentException("Окно уже показано: защитные свойства обязаны встать до Show", nameof(form));
        }

        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(Position, Position);
        form.ShowInTaskbar = false;
    }
}
