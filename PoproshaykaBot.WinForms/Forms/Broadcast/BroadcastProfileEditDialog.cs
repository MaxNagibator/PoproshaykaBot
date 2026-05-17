using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

public partial class BroadcastProfileEditDialog : Form
{
    private string _originalLanguage = string.Empty;
    private bool _nameRequired = true;
    private bool _initialized;
    private bool _sceneBindingEnabled = true;

    public BroadcastProfileEditDialog()
    {
        InitializeComponent();
        UpdatePreview();
    }

    [Inject]
    public ObsIntegrationService ObsIntegration { get; internal init; } = null!;

    [Inject]
    public ObsIntegrationStore ObsIntegrationSettingsStore { get; internal init; } = null!;

    [Inject]
    public ILogger<BroadcastProfileEditDialog> Logger { get; internal init; } = null!;

    public string NewProfileName => _nameTextBox.Text.Trim();

    public void LoadFrom(BroadcastProfile profile)
    {
        _nameTextBox.Text = profile.Name;
        _titleTextBox.Text = profile.Title;
        _currentNumberSpinner.Value = Math.Clamp(profile.CurrentNumber, (int)_currentNumberSpinner.Minimum, (int)_currentNumberSpinner.Maximum);
        _gameBox.SetSelected(profile.GameId, profile.GameName);
        _tagsTextBox.Text = string.Join(", ", profile.Tags);
        _obsSceneComboBox.Text = profile.ObsSceneName;
        _originalLanguage = profile.BroadcasterLanguage ?? string.Empty;
        _languageComboBox.SelectedItem = _languageComboBox.Items
            .Cast<string>()
            .FirstOrDefault(item => string.Equals(item, _originalLanguage, StringComparison.OrdinalIgnoreCase));

        UpdateOkButtonState();
        UpdatePreview();
    }

    public void ConfigureCurrentSettingsMode()
    {
        Text = "Текущие настройки эфира";
        _nameRequired = false;
        _nameLbl.Text = "Сохранить как:";
        _nameTextBox.Text = string.Empty;
        _nameTextBox.PlaceholderText = "имя нового профиля (необязательно)";
        _okButton.Text = "Применить";

        _sceneBindingEnabled = false;
        _obsSceneLbl.Visible = false;
        _obsSceneComboBox.Visible = false;

        UpdateOkButtonState();
    }

    public void SaveTo(BroadcastProfile profile)
    {
        if (_nameRequired)
        {
            profile.Name = _nameTextBox.Text.Trim();
        }

        profile.Title = _titleTextBox.Text.Trim();
        profile.CurrentNumber = (int)_currentNumberSpinner.Value;
        if (_gameBox.Selected != null)
        {
            profile.GameId = _gameBox.Selected.Id;
            profile.GameName = _gameBox.Selected.Name;
        }

        profile.Tags = _tagsTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        profile.BroadcasterLanguage = _languageComboBox.SelectedItem?.ToString() ?? _originalLanguage;
        profile.ObsSceneName = _obsSceneComboBox.Text.Trim();
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
        _ = PopulateObsScenesAsync();
    }

    private void OnNameTextChanged(object? sender, EventArgs e)
    {
        UpdateOkButtonState();
    }

    private void OnTitleOrNumberChanged(object? sender, EventArgs e)
    {
        UpdatePreview();
    }

    private async Task PopulateObsScenesAsync()
    {
        try
        {
            if (!_sceneBindingEnabled)
            {
                return;
            }

            var settings = ObsIntegrationSettingsStore.Load();
            if (!settings.Enabled)
            {
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var scenes = await ObsIntegration.ListScenesAsync(settings, cts.Token);

            if (IsDisposed || scenes.Count == 0)
            {
                return;
            }

            var current = _obsSceneComboBox.Text;
            _obsSceneComboBox.BeginUpdate();
            _obsSceneComboBox.Items.Clear();
            _obsSceneComboBox.Items.AddRange([.. scenes]);
            _obsSceneComboBox.Text = current;
            _obsSceneComboBox.EndUpdate();
        }
        catch (ObjectDisposedException)
        {
            // диалог закрыт пока грузились сцены
        }
        catch (Exception exception)
        {
            Logger.LogDebug(exception, "Не удалось загрузить список сцен OBS для привязки профиля");
        }
    }

    private void UpdateOkButtonState()
    {
        _okButton.Enabled = !_nameRequired || _nameTextBox.Text.Trim().Length > 0;
    }

    private void UpdatePreview()
    {
        var title = _titleTextBox.Text;
        var template = MessageTemplate.For(title);
        if (!template.Contains("n"))
        {
            _previewLabel.Visible = false;
            return;
        }

        var rendered = template.With("n", ((int)_currentNumberSpinner.Value).ToString()).Render();
        _previewLabel.Text = $"Текущая серия в эфире: {rendered}";
        _previewLabel.Visible = true;
    }
}
