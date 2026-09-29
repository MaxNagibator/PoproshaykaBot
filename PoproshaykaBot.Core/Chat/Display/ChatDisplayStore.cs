using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Persistence;
using System.Globalization;
using System.Text.Json;

namespace PoproshaykaBot.Core.Chat.Display;

public sealed class ChatDisplayStore(ILogger<ChatDisplayStore> logger, string? directory = null)
{
    public const double DefaultZoom = 0.75;
    public const double MinZoom = 0.25;
    public const double MaxZoom = 5.0;

    private const double ZoomTolerance = 0.0005;

    private const string HideClutterScriptTemplate = """
                                                     (function () {
                                                         const selectors = __SELECTORS__;
                                                         const wrapperClasses = ['tw-transition', 'tw-callout', 'consent-banner'];
                                                         const findWrapper = (el) => {
                                                             let node = el;
                                                             for (let i = 0; i < 15 && node; i++) {
                                                                 if (node.classList && wrapperClasses.some((c) => node.classList.contains(c))) {
                                                                     return node;
                                                                 }
                                                                 node = node.parentElement;
                                                             }
                                                             return el;
                                                         };
                                                         const hideAll = () => {
                                                             const consentAccept = document.querySelector('[data-a-target="consent-banner-accept"]');
                                                             if (consentAccept) {
                                                                 consentAccept.click();
                                                             }
                                                             for (const selector of selectors) {
                                                                 document.querySelectorAll(selector).forEach((el) => {
                                                                     const wrapper = findWrapper(el);
                                                                     if (wrapper.dataset.poproshaykaHidden !== '1') {
                                                                         wrapper.dataset.poproshaykaHidden = '1';
                                                                         wrapper.style.setProperty('display', 'none', 'important');
                                                                     }
                                                                 });
                                                             }
                                                         };
                                                         hideAll();
                                                         if (document.readyState === 'loading') {
                                                             document.addEventListener('DOMContentLoaded', hideAll, { once: true });
                                                         }
                                                         const startObserver = () => {
                                                             if (document.documentElement) {
                                                                 new MutationObserver(hideAll).observe(document.documentElement, { childList: true, subtree: true });
                                                             } else {
                                                                 setTimeout(startObserver, 0);
                                                             }
                                                         };
                                                         startObserver();
                                                     })();
                                                     """;

    private static readonly string[] BuiltInClutterSelectors =
    [
        "[data-a-target=\"consent-banner\"]",
        ".consent-banner",
        ".tw-callout-message",
        "[class*=\"channelLeaderboardHeader\"]",
        "[class*=\"channelLeaderboardBottomIconContainer\"]",
        "[class*=\"community-highlight\"]",
    ];

    private readonly string _zoomFilePath = Path.Combine(directory ?? AppPaths.BaseDirectory, "chat-zoom.txt");

    private readonly string _blockersFilePath = Path.Combine(directory ?? AppPaths.BaseDirectory, "chat-blockers.txt");

    private volatile bool _zoomReadFailed;
    private volatile bool _blockersReadFailed;

    public double LoadZoom()
    {
        string text;

        try
        {
            if (!File.Exists(_zoomFilePath))
            {
                _zoomReadFailed = false;
                return DefaultZoom;
            }

            text = File.ReadAllText(_zoomFilePath).Trim();
        }
        catch (Exception exception)
        {
            _zoomReadFailed = true;
            logger.LogWarning(exception, "Не удалось прочитать сохранённый масштаб чата – до удачного чтения он не перезаписывается");
            return DefaultZoom;
        }

        _zoomReadFailed = false;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom)
            && zoom >= MinZoom && zoom <= MaxZoom)
        {
            return zoom;
        }

        return DefaultZoom;
    }

    public void SaveZoom(double zoom)
    {
        if (_zoomReadFailed)
        {
            if (Math.Abs(zoom - DefaultZoom) < ZoomTolerance)
            {
                logger.LogDebug("Масштаб чата {Zoom} не сохранён: чтение {FilePath} сорвалось", zoom, _zoomFilePath);
                return;
            }

            _zoomReadFailed = false;
        }

        try
        {
            AtomicFile.Save(_zoomFilePath, zoom.ToString("F3", CultureInfo.InvariantCulture), logger);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить масштаб чата");
        }
    }

    public bool TryLoadBlockersText(out string text)
    {
        var read = TryReadBlockersText(out text);
        _blockersReadFailed = !read;
        return read;
    }

    public string LoadBlockersText()
    {
        TryLoadBlockersText(out var text);
        return text;
    }

    public bool TrySaveBlockersText(string text)
    {
        if (_blockersReadFailed)
        {
            logger.LogWarning("Список блокираторов баннеров чата не сохранён: чтение {FilePath} сорвалось, и запись стёрла бы непрочитанные правила",
                _blockersFilePath);

            return false;
        }

        try
        {
            AtomicFile.Save(_blockersFilePath, text, logger);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось сохранить список блокираторов баннеров чата");
            return false;
        }
    }

    public string BuildHideClutterScript()
    {
        var selectors = BuiltInClutterSelectors.Concat(LoadUserSelectors()).ToArray();
        return HideClutterScriptTemplate.Replace("__SELECTORS__", JsonSerializer.Serialize(selectors), StringComparison.Ordinal);
    }

    private IEnumerable<string> LoadUserSelectors()
    {
        TryReadBlockersText(out var text);

        return text
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));
    }

    private bool TryReadBlockersText(out string text)
    {
        try
        {
            text = File.Exists(_blockersFilePath) ? File.ReadAllText(_blockersFilePath) : string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось прочитать список блокираторов баннеров чата");
            text = string.Empty;
            return false;
        }
    }
}
