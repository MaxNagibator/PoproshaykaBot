using PoproshaykaBot.Core.Infrastructure;
using System.IO;

namespace PoproshaykaBot.Wpf.Bootstrap;

public sealed class GalleryWorkspace
{
    private const string StatePrefix = "PoproshaykaBot-gallery-";

    private GalleryWorkspace(string outputRoot) => OutputRoot = outputRoot;

    public string OutputRoot { get; }

    public static GalleryWorkspace Redirect()
    {
        var existingOverride = Environment.GetEnvironmentVariable(AppPaths.BaseDirectoryEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(existingOverride))
        {
            return new(AppPaths.BaseDirectory);
        }

        var outputRoot = AppPaths.DefaultBaseDirectory;

        // TODO: каталоги прогонов копятся в %TEMP% – съёмка их не убирает, иначе журнал упавшего
        //  прогона уходит вместе с ними; заводить уборку по возрасту, когда галерея начнёт гоняться
        //  в CI на каждый коммит
        var stateDirectory = Directory.CreateTempSubdirectory(StatePrefix).FullName;

        Environment.SetEnvironmentVariable(AppPaths.BaseDirectoryEnvironmentVariable, stateDirectory);

        return new(outputRoot);
    }
}
