using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using PoproshaykaBot.Core.Settings.Stores;
using System.Security.Cryptography;
using System.Text.Json;

namespace PoproshaykaBot.Core.Update;

public sealed class UpdateInstaller(
    IGitHubReleaseClient client,
    IUpdateEnvironment environment,
    ILogger<UpdateInstaller> logger)
    : IUpdateInstaller
{
    public const string PendingFileName = "apply-update.json";

    public async Task PrepareAsync(
        UpdateCandidate candidate,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        EnsureExecutableDirectoryWritable();

        await EnsureRuntimeCompatibleAsync(candidate, cancellationToken).ConfigureAwait(false);

        var stagingDirectory = environment.StagingDirectory;
        Directory.CreateDirectory(stagingDirectory);

        var partPath = Path.Combine(stagingDirectory, candidate.Asset.Name + ".part");
        var stagedPath = Path.Combine(stagingDirectory, candidate.Asset.Name);

        logger.LogInformation("Загрузка обновления {Version} из {Url}",
            candidate.Version, candidate.Asset.DownloadUrl);

        string expectedHash;
        string actualHash;

        try
        {
            await client
                .DownloadFileAsync(candidate.Asset.DownloadUrl, partPath, progress, cancellationToken)
                .ConfigureAwait(false);

            expectedHash = await ResolveExpectedHashAsync(candidate, cancellationToken).ConfigureAwait(false);
            actualHash = await ComputeSha256Async(partPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(partPath);
            throw;
        }

        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(partPath);
            logger.LogError("Контрольная сумма обновления не совпала. Ожидалось {Expected}, получено {Actual}",
                expectedHash, actualHash);

            throw new UpdateException("Контрольная сумма загруженного обновления не совпала. Файл повреждён или подменён.");
        }

        if (File.Exists(stagedPath))
        {
            File.Delete(stagedPath);
        }

        File.Move(partPath, stagedPath);

        var pending = new PendingUpdate(candidate.Version.ToString(),
            stagedPath,
            environment.CurrentExecutablePath,
            actualHash);

        var json = JsonSerializer.Serialize(pending, JsonStoreOptions.Default);
        AtomicFile.Save(Path.Combine(stagingDirectory, PendingFileName), json, logger);

        logger.LogInformation("Обновление {Version} подготовлено и проверено", candidate.Version);
    }

    public PendingUpdate? ReadPending()
    {
        var pendingPath = Path.Combine(environment.StagingDirectory, PendingFileName);

        if (!File.Exists(pendingPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(pendingPath);
            var pending = JsonSerializer.Deserialize<PendingUpdate>(json, JsonStoreOptions.Default);

            if (pending is null || !File.Exists(pending.StagedExecutablePath))
            {
                return null;
            }

            return pending;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось прочитать {PendingPath}", pendingPath);
            return null;
        }
    }

    public bool DiscardPending(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);

        var pending = ReadPending();

        if (pending is null || !string.Equals(pending.Version, version, StringComparison.Ordinal))
        {
            return false;
        }

        return UpdateApplier.DiscardFiles(environment.StagingDirectory, pending, logger);
    }

    private static string BuildChecksumsUrl(string assetUrl, string architectureMoniker)
    {
        return BuildSiblingUrl(assetUrl, UpdateVersioning.ChecksumsAssetName(architectureMoniker));
    }

    private static string BuildSiblingUrl(string assetUrl, string fileName)
    {
        var lastSlash = assetUrl.LastIndexOf('/');

        if (lastSlash < 0)
        {
            throw new UpdateException("Некорректный URL ресурса релиза.");
        }

        return assetUrl[..(lastSlash + 1)] + fileName;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private async Task<string> ResolveExpectedHashAsync(
        UpdateCandidate candidate,
        CancellationToken cancellationToken)
    {
        var checksumsUrl = BuildChecksumsUrl(candidate.Asset.DownloadUrl, environment.ArchitectureMoniker);

        string checksums;

        try
        {
            checksums = await client.DownloadTextAsync(checksumsUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateException("Не удалось загрузить файл контрольных сумм релиза. Обновление прервано.", exception);
        }

        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[1].EndsWith(candidate.Asset.Name, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }

        throw new UpdateException("В файле контрольных сумм нет записи для скачанной сборки. Обновление прервано.");
    }

    private async Task EnsureRuntimeCompatibleAsync(UpdateCandidate candidate, CancellationToken cancellationToken)
    {
        if (environment.Kind != UpdateKind.FrameworkDependent)
        {
            return;
        }

        var url = BuildSiblingUrl(candidate.Asset.DownloadUrl,
            UpdateVersioning.RuntimeAssetName(environment.ArchitectureMoniker));

        string runtimeText;

        try
        {
            runtimeText = await client.DownloadTextAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateException("Не удалось проверить совместимость версии .NET для обновления. "
                                      + "Скачайте новую версию вручную со страницы релизов.", exception);
        }

        var requiredMajor = UpdateRuntime.ParseMajor(runtimeText.Trim());

        if (requiredMajor is null)
        {
            logger.LogWarning("Не удалось разобрать требуемую версию .NET из '{Value}', проверка пропущена", runtimeText);
            return;
        }

        if (requiredMajor.Value != environment.RuntimeMajor)
        {
            throw new UpdateException($"Обновление требует .NET {requiredMajor.Value}, а текущая сборка работает на .NET {environment.RuntimeMajor}. "
                                      + "Установите нужный .NET Desktop Runtime и скачайте новую версию вручную со страницы релизов.");
        }
    }

    private void EnsureExecutableDirectoryWritable()
    {
        var directory = Path.GetDirectoryName(environment.CurrentExecutablePath);

        if (string.IsNullOrEmpty(directory))
        {
            throw new UpdateException("Не удалось определить каталог исполняемого файла.");
        }

        var probePath = Path.Combine(directory, ".update-write-probe-" + Guid.NewGuid().ToString("N"));

        try
        {
            File.WriteAllText(probePath, "probe");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException("Нет прав на запись в каталог приложения. Переместите программу в папку пользователя "
                                      + "(например, в Документы или на рабочий стол) или скачайте обновление вручную.", exception);
        }
        finally
        {
            TryDelete(probePath);
        }
    }

    private void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось удалить временный файл {Path}", path);
        }
    }
}
