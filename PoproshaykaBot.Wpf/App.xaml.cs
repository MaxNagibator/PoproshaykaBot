using KeepShell.Diagnostics;
using KeepShell.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Debugging;
using PoproshaykaBot.Core.Diagnostics;
using PoproshaykaBot.Core.Infrastructure;
using PoproshaykaBot.Core.Infrastructure.Di;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Hosting;
using PoproshaykaBot.Core.Infrastructure.Logging;
using PoproshaykaBot.Core.Infrastructure.Runtime;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Settings;
using PoproshaykaBot.Core.Settings.Migrations;
using PoproshaykaBot.Core.Statistics;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.GameArt;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.Wpf.Bootstrap;
using PoproshaykaBot.Wpf.Infrastructure;
using PoproshaykaBot.Wpf.Infrastructure.Diagnostics;
using PoproshaykaBot.Wpf.Mcp;
using PoproshaykaBot.Wpf.ViewModels;
using PoproshaykaBot.Wpf.ViewModels.Diagnostics;
using PoproshaykaBot.Wpf.ViewModels.Controls;
using PoproshaykaBot.Wpf.ViewModels.Dialogs;
using PoproshaykaBot.Wpf.ViewModels.Onboarding;
using PoproshaykaBot.Wpf.ViewModels.Settings;
using PoproshaykaBot.Wpf.ViewModels.Tiles;
using PoproshaykaBot.Wpf.Views;
using PoproshaykaBot.Wpf.Views.Onboarding;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace PoproshaykaBot.Wpf;

public partial class App : Application
{
    private const string OutputTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    private static int _fatalErrorHandled;
    private static int _restartRequested;
    private static string[] _startupArguments = [];

    private ServiceProvider? _services;
    private KeepShellLogging? _logging;
    private KeepShellLoggingOptions? _loggingOptions;
    private AppLifetime? _appLifetime;
    private StreamMonitoringHost? _streamMonitoringHost;
    private MemoryWatchdog? _memoryWatchdog;
    private BindingErrorSink? _bindingErrors;
    private Mutex? _singleInstanceMutex;

    private bool _isFinalizeUpdate;
    private string[]? _galleryArgs;
    private GalleryWorkspace? _galleryWorkspace;
    private GalleryArguments? _galleryArguments;
    private bool _appLifetimeStarted;
    private bool _streamMonitoringStarted;
    private AppExitReason _exitReason = AppExitReason.None;

    internal static bool IsFatalShutdown => Volatile.Read(ref _fatalErrorHandled) == 1;

    internal static bool IsRestartRequested => Volatile.Read(ref _restartRequested) == 1;

    private static Serilog.ILogger HostLog => Log.ForContext<App>();

    internal static bool IsHeadless { get; private set; }

    internal static bool IsGallery { get; private set; }

    internal static DebugChannelOverride DebugChannel { get; private set; } = DebugChannelOverride.None;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplyUiCulture();

        _startupArguments = e.Args;
        var previousInstance = AppRestart.FindPreviousProcessId(e.Args);
        var previousInstanceExited = previousInstance is not { } previousProcessId
                                     || AppRestart.WaitForExit(previousProcessId, AppRestart.HandoffTimeout);

        IsHeadless = e.Args.Any(arg => string.Equals(arg, "--ui-smoke", StringComparison.OrdinalIgnoreCase));
        DebugChannel = DebugChannelOverride.Parse(e.Args);
        _isFinalizeUpdate = e.Args.Any(arg => string.Equals(arg, UpdateApplier.FinalizeArgument, StringComparison.OrdinalIgnoreCase));

        var galleryIndex = Array.FindIndex(e.Args, static arg => string.Equals(arg, GalleryHost.ArgumentName, StringComparison.OrdinalIgnoreCase));

        if (galleryIndex >= 0)
        {
            _galleryArgs = [.. e.Args.Skip(galleryIndex + 1)];
            IsHeadless = true;
            IsGallery = true;
            _galleryWorkspace = GalleryWorkspace.Redirect();
        }

        var coreUiLogSink = new UiLogSink();
        SelfLog.Enable(message => Debug.WriteLine($"[Serilog] {message}"));

        _loggingOptions = new()
        {
            LogsDirectory = AppPaths.Combine("logs"),
            FileNamePrefix = AppInfo.LogFilePrefix,
            OutputTemplate = OutputTemplate,
            RetainedFileCountLimit = 31,
            FileSizeLimitBytes = 50L * 1024 * 1024,
            RollOnFileSizeLimit = true,
            MinimumLevel = LogEventLevel.Debug,
            MinimumLevelOverrides = new Dictionary<string, LogEventLevel>
            {
                ["Microsoft"] = LogEventLevel.Information,
                ["Microsoft.AspNetCore"] = LogEventLevel.Warning,
                ["System.Net.Http.HttpClient"] = LogEventLevel.Warning,
            },
            WriteToDebug = true,
            Configure = configuration => configuration
                .WriteTo.Console(outputTemplate: OutputTemplate)
                .WriteTo.Sink(coreUiLogSink, LogEventLevel.Information),
        };

        _logging = KeepShellLogging.Bootstrap(_loggingOptions);

        HostLog.Information(AppInfo.SessionStartMarker + "...");

        if (previousInstance is { } restartedAfter)
        {
            ReportRestartHandoff(restartedAfter, previousInstanceExited);
        }

        _bindingErrors = BindingErrorSink.Attach();
        _bindingErrors.Captured += OnBindingErrorCaptured;

        AttachFatalExceptionTrap();

        StyledMessageBox.DefaultTitle = AppInfo.Name;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var uiSettingsPath = AppPaths.SettingsFile("ui-preferences.toml");
            ISettingsStore uiSettings = new SettingsStore(uiSettingsPath);

            AppThemes.Register();

            if (_galleryArgs is not null && _galleryWorkspace is not null)
            {
                _galleryArguments = GalleryHost.Parse(_galleryArgs, System.IO.Path.Combine(_galleryWorkspace.OutputRoot, GalleryRunner.FolderName));
            }

            var themeKey = uiSettings.GetStringValue(SettingsKeys.Theme);
            ThemeManager.Apply(string.IsNullOrWhiteSpace(themeKey) ? AppThemes.LightKey : themeKey);
            FontScaleManager.Initialize(uiSettings.GetDouble(SettingsKeys.FontScale, FontScaleManager.DefaultScale));
            AppScaledSizes.Register(this);

            _singleInstanceMutex = SingleInstanceGate.TryAcquire(_isFinalizeUpdate);

            if (_singleInstanceMutex is null)
            {
                HostLog.Information("Обнаружен уже запущенный экземпляр приложения. Завершение работы");
                _exitReason = AppExitReason.AlreadyRunning;

                if (!IsHeadless)
                {
                    StyledMessageBox.Show("PoproshaykaBot уже запущен.\n\nОдновременно может работать только один экземпляр приложения.",
                        "Приложение уже запущено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                Shutdown();
                return;
            }

            ReportEnvironment(e.Args);

            if (_isFinalizeUpdate)
            {
                FinalizeUpdate();
            }

            ViewLocator.InstallIntoApplication();

            if (!IsHeadless)
            {
                uiSettings = OfferLegacyDataImport(uiSettings, uiSettingsPath);
            }

            MigrateLegacySettingsLayout();

            var services = new ServiceCollection();
            ConfigureServices(services, coreUiLogSink, uiSettings);
            services.AddSingleton(_logging!.Sink);
            services.AddSingleton(_loggingOptions!);
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            _services.ActivateEventSubscribers(typeof(InfrastructureServiceCollectionExtensions).Assembly);
            ReportConfiguration(_services);
            StatisticsBootstrap.LoadUserStatistics(_services);

            _appLifetime = _services.GetRequiredService<AppLifetime>();
            _streamMonitoringHost = _services.GetRequiredService<StreamMonitoringHost>();

            if (IsHeadless)
            {
                HostLog.Information("Запуск в режиме UI smoke-теста. HTTP сервер и сетевые подсистемы отключены");
            }
            else
            {
                var settingsManager = _services.GetRequiredService<SettingsManager>();
                _appLifetimeStarted = StartHttpServerIfNeeded(settingsManager, _appLifetime);
                _streamMonitoringStarted = StartStreamMonitoring(_streamMonitoringHost);
                _memoryWatchdog = new(new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(MemoryWatchdog)),
                    Log.CloseAndFlush,
                    _services.GetRequiredService<MemoryUsageSink>());
            }

            if (_galleryArguments is not null)
            {
                RunGallery(_galleryArguments, _services);
                return;
            }

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;

            if (IsHeadless)
            {
                OffScreenWindow.Prepare(window, window.Width, window.Height);
            }

            window.Show();

            if (!IsHeadless)
            {
                _services.GetRequiredService<McpServerHost>().Apply();
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            MaybeLaunchOnboardingWizard();
        }
        catch (Exception ex)
        {
            HostLog.Fatal(ex, "{App} не смог запуститься", AppInfo.Name);
            _exitReason = AppExitReason.Failed;
            Interlocked.Exchange(ref _fatalErrorHandled, 1);

            if (!IsHeadless)
            {
                StyledMessageBox.Show(ex.ToString(), $"{AppInfo.Name} – ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _memoryWatchdog?.Dispose();
        _services?.GetService<ISettingsStore>()?.Close();

        if (_services is not null)
        {
            Task.Run(StopAllComponents).GetAwaiter().GetResult();

            if (_exitReason is AppExitReason.None)
            {
                _exitReason = IsRestartRequested ? AppExitReason.Restart : AppExitReason.Normal;
            }

            var updateApplied = !IsHeadless && !IsFatalShutdown && ApplyPendingUpdate();

            if (updateApplied)
            {
                _exitReason = AppExitReason.UpdatePending;
            }

            if (AppRestart.ShouldLaunch(IsRestartRequested, updateApplied, IsFatalShutdown))
            {
                LaunchReplacement();
            }
        }

        if (_bindingErrors is { } bindingErrors)
        {
            bindingErrors.Captured -= OnBindingErrorCaptured;

            if (bindingErrors.Count > 0)
            {
                HostLog.Warning("Ошибок привязки данных за прогон: {BindingErrorCount}", bindingErrors.Count);
            }
        }

        HostLog.Information("Завершение работы приложения ({Reason}), код выхода {ExitCode}", _exitReason, e.ApplicationExitCode);
        _bindingErrors?.Dispose();
        _logging?.Dispose();

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static void ApplyUiCulture()
    {
        var language = XmlLanguage.GetLanguage(UiCulture.Russian.IetfLanguageTag);

        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(language));
    }

    private static void OnBindingErrorCaptured(object? sender, BindingErrorRecord record)
    {
        HostLog.Error("Ошибка привязки данных: {BindingError}", record.Message);
    }

    private void RunGallery(GalleryArguments arguments, IServiceProvider services)
    {
        FontScaleManager.Apply(arguments.FontScale);

        _ = Dispatcher.InvokeAsync(async () =>
        {
            var code = 1;

            try
            {
                code = await GalleryRunner.RunAsync(GalleryHost.Create(services, arguments), arguments, new GalleryJournal());
            }
            catch (Exception exception)
            {
                HostLog.Error(exception, "Съёмка галереи упала");
            }

            Shutdown(code);
        });
    }

    private static void MigrateLegacySettingsLayout()
    {
        using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        var logger = loggerFactory.CreateLogger(nameof(LegacySettingsLayoutMigrator));

        try
        {
            LegacySettingsLayoutMigrator.Run(AppPaths.BaseDirectory, AppPaths.SettingsDirectory, logger);
        }
        catch (Exception exception)
        {
            HostLog.Error(exception, "Ошибка пред-миграции layout-а настроек");
        }
    }

    private static void ConfigureServices(IServiceCollection services, UiLogSink uiLogSink, ISettingsStore uiSettings)
    {
        var uiSettingsGuard = new UiSettingsWriteGuard(uiSettings);
        services.AddSingleton(uiSettingsGuard);
        services.AddSingleton<ISettingsStore>(uiSettingsGuard);

        services
            .AddCoreInfrastructure(uiLogSink, disposeSerilog: false)
            .AddStatistics()
            .AddSettingsStores()
            .AddDebugChannel(DebugChannel)
            .AddTwitchClients()
            .AddGameBoxArt(cacheOnly: IsGallery)
            .AddChatPipeline()
            .AddStreamMonitoring()
            .AddBroadcasting()
            .AddPolls()
            .AddHttpServer()
            .AddObsIntegration()
            .AddSelfUpdate()
            .AddDiagnostics();

        services.AddSingleton<BotConnectionManager>();
        services.AddSingleton<IBotConnectionController>(provider => provider.GetRequiredService<BotConnectionManager>());

        services.AddKeepShell();

        services.AddKeepShellDiagnostics(new DiagnosticsOptions
        {
            AppName = AppInfo.Name,
            AppVersion = AppInfo.Version,
            DataDirectory = AppPaths.BaseDirectory,
            PortableStorage = AppPaths.IsPortable,
        });

        services.AddSingleton(static provider => new DiagnosticsSnapshotPublisher(
            provider.GetRequiredService<IUiDispatcher>(),
            provider.GetRequiredService<DiagnosticsSnapshotSource>().Capture,
            provider.GetRequiredService<ILogger<DiagnosticsSnapshotPublisher>>()));

        services.AddSingleton<IDiagnosticsBundleSource, BotDiagnosticsBundleSource>();
        services.AddSingleton<IDiagnosticsSecretSource, BotDiagnosticsSecretSource>();

        services.AddSingleton<IDiagnosticsCard, MemoryDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, EventSubDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, ChatDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, ObsDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, SseDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, QueueDiagnosticsCardViewModel>();
        services.AddSingleton<IDiagnosticsCard, EventBusDiagnosticsCardViewModel>();

        services.AddSingleton<BotAutomation>();
        services.AddKeepShellMcp<ShellViewModel>(new McpServerOptions
        {
            ServerName = "poproshaykabot",
            AppName = AppInfo.Name,
            AppVersion = AppInfo.Version,
            DefaultPort = 7656,
            ShotsDirectory = AppPaths.Combine("shots"),
            ToolTypes = [typeof(PoproshaykaBotTools)],
            ConfigureTools = static (provider, tools) => tools.AddSingleton(provider.GetRequiredService<BotAutomation>()),
        });

        services.AddSingleton(static provider => new ErrorReportOptions
        {
            IssueRepo = () => provider.GetRequiredService<IUpdateRepositoryProvider>().Slug,
            LogFileGlobs = [AppInfo.LogFileGlob],
            SessionStartMarker = AppInfo.SessionStartMarker,
        });
        services.AddSingleton<ErrorReportService>();

        services.AddSingleton<DashboardTileViewModel, StreamInfoTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, StreamPreviewTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, BroadcastStatusTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, BroadcastProfilesTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, PollsTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, ObsInfoTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, ChatDisplayTileViewModel>();
        services.AddSingleton<DashboardTileViewModel, ChatOverlayPreviewTileViewModel>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<UserStatisticsPageViewModel>();
        services.AddSingleton<GameBoxArtProvider>();
        services.AddSingleton<StreamHistoryPageViewModel>();
        services.AddSingleton<CommandsPageViewModel>();
        services.AddSingleton<ThemeViewModel>();
        services.AddSingleton<ShellPreferences>();
        services.AddSingleton<UpdateBannerViewModel>();
        services.AddSingleton<OnboardingBannerViewModel>();
        services.AddSingleton<DebugBannerViewModel>();
        services.AddSingleton<IUnsavedChangesPrompt, UnsavedChangesPrompt>();
        services.AddSingleton<IOnboardingWizardLauncher, OnboardingWizardLauncher>();
        services.AddSingleton<IEmbeddedTwitchAuthDialog, EmbeddedTwitchAuthDialog>();
        services.AddSingleton<ILegacyImportDialog, LegacyImportDialog>();
        services.AddSingleton<StreamMonitoringViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddTransient<GameAutocompleteViewModel>();
        services.AddTransient<BroadcastProfileEditDialogViewModel>();
        services.AddTransient<PollProfileEditDialogViewModel>();
        services.AddTransient<PollFromProfileDialogViewModel>();
        services.AddTransient<EmbeddedTwitchAuthDialogViewModel>();

        services.AddTransient<BasicSettingsSectionViewModel>();
        services.AddTransient<RateLimitingSettingsViewModel>();
        services.AddTransient<MessagesSettingsSectionViewModel>();
        services.AddTransient<HttpServerSectionViewModel>();
        services.AddTransient<OAuthSettingsViewModel>();
        services.AddSingleton<ObsChatSettingsSectionViewModel>();
        services.AddSingleton<ObsIntegrationSectionViewModel>();
        services.AddTransient<AutoBroadcastSettingsViewModel>();
        services.AddTransient<BotLifecycleAutomationSectionViewModel>();
        services.AddTransient<MiscSettingsSectionViewModel>();
        services.AddSingleton<PollsSettingsSectionViewModel>();
        services.AddSingleton<UpdateSettingsSectionViewModel>();
        services.AddTransient<DashboardLayoutSectionViewModel>();
        services.AddTransient<DebugChannelSectionViewModel>();
        services.AddSingleton<McpSettingsSectionViewModel>();

        services.AddSingleton<SettingsPageViewModel>();

        services.AddTransient<IOnboardingPageViewModel, WelcomePageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, CredentialsPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, HttpServerCheckPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, BotAuthorizationPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, BroadcasterAuthorizationPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, BotConnectionPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, HealthCheckPageViewModel>();
        services.AddTransient<IOnboardingPageViewModel, CompletionPageViewModel>();

        services.AddTransient<OnboardingWizardViewModel>();
        services.AddTransient<OnboardingWizardWindow>();
    }

    private void StopAllComponents()
    {
        using var shutdownWatchdog = IsHeadless
            ? null
            : new ShutdownWatchdog(new SerilogLoggerFactory(Log.Logger).CreateLogger(nameof(ShutdownWatchdog)),
                Log.CloseAndFlush,
                ShutdownDeadlines.HardDeadline);

        using var shutdownTimeout = IsHeadless ? null : new CancellationTokenSource(ShutdownDeadlines.SoftDeadline);
        var shutdownToken = shutdownTimeout?.Token ?? CancellationToken.None;

        if (_streamMonitoringStarted && _streamMonitoringHost is not null)
        {
            StopStreamMonitoring(_streamMonitoringHost, shutdownToken);
        }

        if (_appLifetimeStarted && _appLifetime is not null)
        {
            StopAppLifetime(_appLifetime, shutdownToken);
        }

        _services!.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void AttachFatalExceptionTrap()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                HostLog.Fatal(exception, "Приложение завершило работу из-за непредвиденной ошибки");
            }

            Log.CloseAndFlush();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        HostLog.Fatal(args.Exception, "Необработанное исключение в UI-потоке");
        args.Handled = true;
        _exitReason = AppExitReason.Failed;

        if (Interlocked.Exchange(ref _fatalErrorHandled, 1) == 1)
        {
            return;
        }

        // TODO: после фатальной ошибки хост только закрывается – передачу мьютекса новому процессу
        //  уже умеет LaunchReplacement (--restart-after), но падение на старте зациклило бы перезапуск;
        //  заводить вместе со счётчиком падений подряд
        ShowFatalErrorNotice();

        try
        {
            Shutdown(1);
        }
        catch (InvalidOperationException exception)
        {
            HostLog.Error(exception, "Не удалось штатно завершить работу после фатальной ошибки");
        }
    }

    private void ShowFatalErrorNotice()
    {
        if (IsHeadless)
        {
            return;
        }

        var logsDirectory = _loggingOptions?.LogsDirectory ?? AppPaths.Combine("logs");
        var caption = $"{AppInfo.Name} – критическая ошибка";
        var message = $"Произошла непредвиденная ошибка, приложение будет закрыто.\n\nПодробности – в журнале: {logsDirectory}";

        try
        {
            StyledMessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception styledFailure)
        {
            HostLog.Error(styledFailure, "Не удалось показать сообщение о фатальной ошибке средствами каркаса");

            try
            {
                MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception fallbackFailure)
            {
                HostLog.Error(fallbackFailure, "Не удалось показать системное сообщение о фатальной ошибке");
            }
        }
    }
}
