using System.Text;

namespace PoproshaykaBot.WinForms.Services.Http;

public static class ResourceLoader
{
    public static string LoadResourceText(string resourceName)
    {
        var assembly = typeof(ResourceLoader).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new InvalidOperationException($"Ресурс не найден: {resourceName}");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public static byte[] LoadResourceBytes(string resourceName)
    {
        var assembly = typeof(ResourceLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new InvalidOperationException($"Ресурс не найден: {resourceName}");
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
