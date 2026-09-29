namespace PoproshaykaBot.Core.Users;

public sealed class PointTerm
{
    public string Singular { get; set; } = "балл";

    public string Few { get; set; } = "балла";

    public string Many { get; set; } = "баллов";

    public string ForCount(long count)
    {
        var abs = count < 0 ? unchecked((ulong)-count) : (ulong)count;
        var mod100 = abs % 100;
        var mod10 = abs % 10;

        if (mod100 is >= 11 and <= 14)
        {
            return Many;
        }

        return mod10 switch
        {
            1 => Singular,
            >= 2 and <= 4 => Few,
            _ => Many,
        };
    }
}
