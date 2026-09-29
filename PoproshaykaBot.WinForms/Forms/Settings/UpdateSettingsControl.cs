using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Update;
using PoproshaykaBot.Core.Settings.Update;
using PoproshaykaBot.Core.Update;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Globalization;

namespace PoproshaykaBot.WinForms.Forms.Settings;

public sealed partial class UpdateSettingsControl : UserControl
{
    private readonly List<IDisposable> _subs = [];
    private bool _initialized;
    private bool _busy;

    public UpdateSettingsControl()
    {
        InitializeComponent();
    }

    public event EventHandler? SettingChanged;

    [Inject]
    public IUpdateCoordinator Updates { get; internal init; } = null!;

    [Inject]
    public IEventBus Bus { get; internal init; } = null!;

    public void LoadSettings(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _autoCheckCheckBox.Checked = settings.AutoCheckEnabled;
        _intervalNumeric.Value = Math.Clamp(settings.CheckIntervalHours, (int)_intervalNumeric.Minimum, (int)_intervalNumeric.Maximum);
        _autoInstallCheckBox.Checked = settings.ApplyMode == UpdateApplyMode.SilentOnExit;
        _allowFrameworkDependentCheckBox.Checked = settings.AllowFrameworkDependentUpdate;
        _allowFrameworkDependentCheckBox.Visible = Updates.Kind == UpdateKind.FrameworkDependent;
        _repositoryTextBox.PlaceholderText = Updates.DefaultRepositorySlug;
        _repositoryTextBox.Text = settings.RepositoryOverride ?? string.Empty;

        RefreshRepositoryHint();
        RefreshStatus(settings);
    }

    public void SaveSettings(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.AutoCheckEnabled = _autoCheckCheckBox.Checked;
        settings.CheckIntervalHours = (int)_intervalNumeric.Value;
        settings.ApplyMode = _autoInstallCheckBox.Checked ? UpdateApplyMode.SilentOnExit : UpdateApplyMode.NotifyAndConfirm;
        settings.AllowFrameworkDependentUpdate = _allowFrameworkDependentCheckBox.Checked;

        var repository = _repositoryTextBox.Text.Trim();
        settings.RepositoryOverride = repository.Length == 0 ? null : repository;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            return;
        }

        _initialized = true;

        _subs.Add(Bus.SubscribeOnUi<UpdateAvailable>(this, _ => RefreshStatus(null)));
        _subs.DisposeOnClose(this);
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAllowFrameworkDependentChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
        RefreshStatus(null);
    }

    private void OnRepositoryTextChanged(object? sender, EventArgs e)
    {
        SettingChanged?.Invoke(this, EventArgs.Empty);
        RefreshRepositoryHint();
    }

    private async void OnCheckNowButtonClicked(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        _statusLabel.ForeColor = Color.Blue;
        _statusLabel.Text = "Проверка обновлений…";

        try
        {
            var candidate = await Updates.CheckNowAsync(CancellationToken.None);

            if (candidate is null)
            {
                _statusLabel.ForeColor = Color.Gray;
                _statusLabel.Text = "У вас установлена актуальная версия.";
            }
            else
            {
                RefreshStatus(null);
            }
        }
        catch (Exception exception)
        {
            _statusLabel.ForeColor = Color.Red;
            _statusLabel.Text = $"Ошибка проверки: {exception.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnInstallButtonClicked(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var candidate = Updates.LatestCandidate;

        if (candidate is null)
        {
            return;
        }

        var answer = MessageBox.Show(this,
            $"Загрузить и установить версию {candidate.Version}?\n\nПриложение перезапустится автоматически.",
            "Установка обновления",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);

        var progress = new Progress<int>(percent =>
        {
            _statusLabel.ForeColor = Color.Blue;
            _statusLabel.Text = $"Загрузка обновления… {percent}%";
        });

        try
        {
            await Updates.PrepareAsync(candidate, progress, CancellationToken.None);

            MessageBox.Show(this,
                "Обновление загружено. Приложение закроется и установит новую версию.",
                "Установка обновления",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Application.Exit();
        }
        catch (Exception exception)
        {
            _statusLabel.ForeColor = Color.Red;
            _statusLabel.Text = $"Ошибка загрузки: {exception.Message}";

            MessageBox.Show(this,
                $"Не удалось установить обновление: {exception.Message}",
                "Ошибка обновления",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            SetBusy(false);
        }
    }

    private static string FormatLastCheck(UpdateSettings? settings)
    {
        if (settings?.LastCheckUtc is { } lastCheck)
        {
            var local = lastCheck.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return $"Последняя проверка: {local}. Обновлений не найдено.";
        }

        return "Проверка обновлений ещё не выполнялась.";
    }

    private void RefreshRepositoryHint()
    {
        var text = _repositoryTextBox.Text.Trim();

        if (text.Length == 0)
        {
            _repositoryHintLabel.ForeColor = Color.Gray;
            _repositoryHintLabel.Text = $"Пусто – используется {Updates.DefaultRepositorySlug} (из сборки).";
        }
        else if (UpdateRepository.IsValidSlug(text))
        {
            _repositoryHintLabel.ForeColor = Color.Green;
            _repositoryHintLabel.Text = $"✓ {text}";
        }
        else
        {
            _repositoryHintLabel.ForeColor = Color.Red;
            _repositoryHintLabel.Text = "✗ Неверный формат. Ожидается owner/repo.";
        }
    }

    private bool IsUpdatableNow()
    {
        return Updates.Kind switch
        {
            UpdateKind.Portable => true,
            UpdateKind.FrameworkDependent => _allowFrameworkDependentCheckBox.Checked,
            _ => false,
        };
    }

    private void RefreshStatus(UpdateSettings? settings)
    {
        _currentVersionLabel.Text = $"Текущая версия: {Updates.CurrentVersion} (.NET {Environment.Version.Major})";

        if (Updates.Kind == UpdateKind.Unsupported)
        {
            _statusLabel.ForeColor = Color.Gray;
            _statusLabel.Text = "Автообновление недоступно для этой сборки (запуск из среды разработки).";
            _checkNowButton.Enabled = false;
            _installButton.Enabled = false;
            return;
        }

        if (!IsUpdatableNow())
        {
            _statusLabel.ForeColor = Color.Gray;
            _statusLabel.Text = "Автообновление выключено для сборки, требующей .NET. Включите опцию ниже.";
            _checkNowButton.Enabled = false;
            _installButton.Enabled = false;
            return;
        }

        var candidate = Updates.LatestCandidate;

        if (candidate is not null)
        {
            _statusLabel.ForeColor = Color.Green;
            _statusLabel.Text = $"Доступно обновление: {candidate.Version}";
            _installButton.Enabled = !_busy;
        }
        else
        {
            _statusLabel.ForeColor = Color.Gray;
            _statusLabel.Text = FormatLastCheck(settings);
            _installButton.Enabled = false;
        }

        _checkNowButton.Enabled = !_busy;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        var updatable = !busy && IsUpdatableNow();
        _checkNowButton.Enabled = updatable;
        _installButton.Enabled = updatable && Updates.LatestCandidate is not null;
    }
}
