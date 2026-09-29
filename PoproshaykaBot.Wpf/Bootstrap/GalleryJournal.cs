using Serilog;

namespace PoproshaykaBot.Wpf.Bootstrap;

public sealed class GalleryJournal : IGalleryJournal
{
    private static Serilog.ILogger HostLog => Log.ForContext<GalleryJournal>();

    public void Started(int cases, int themes, string directory)
    {
        HostLog.Information("Съёмка галереи: кейсов {Cases}, тем {Themes}, каталог {Directory}", cases, themes, directory);
    }

    public void CaseFailed(Exception exception, string caseName)
    {
        HostLog.Warning(exception, "Кадр {Case} не снят", caseName);
    }

    public void Finished(int frames, int skipped, long elapsedMs)
    {
        HostLog.Information("Съёмка галереи завершена: кадров {Frames}, пропущено {Skipped}, за {Elapsed} мс", frames, skipped, elapsedMs);
    }
}
