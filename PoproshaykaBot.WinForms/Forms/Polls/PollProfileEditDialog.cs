using PoproshaykaBot.Core.Polls;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Polls;

public partial class PollProfileEditDialog : Form
{
    private PollProfile? _editTarget;

    public PollProfileEditDialog()
    {
        InitializeComponent();

        _autoTriggerComboBox.Items.Clear();
        _autoTriggerComboBox.Items.AddRange([
            new AutoTriggerChoice(PollAutoTriggerEvent.None, "– нет –"),
            new AutoTriggerChoice(PollAutoTriggerEvent.StreamOnline, "При начале стрима"),
            new AutoTriggerChoice(PollAutoTriggerEvent.BroadcastProfileApplied, "При применении профиля трансляции"),
        ]);

        _autoTriggerComboBox.SelectedIndex = 0;
    }

    [Inject]
    public PollProfilesManager Manager { get; internal init; } = null!;

    public PollProfile? Result { get; private set; }

    public bool ShouldStartPoll { get; private set; }

    public void LoadFrom(PollProfile profile)
    {
        _editTarget = profile;

        _nameTextBox.Text = profile.Name;
        _titleTextBox.Text = profile.Title;
        _choicesTextBox.Text = string.Join(Environment.NewLine, profile.Choices);
        _durationNumeric.Value = Math.Clamp(profile.DurationSeconds,
            (int)_durationNumeric.Minimum, (int)_durationNumeric.Maximum);

        _channelPointsCheckBox.Checked = profile.ChannelPointsVotingEnabled;
        _channelPointsPerVoteNumeric.Enabled = profile.ChannelPointsVotingEnabled;
        _channelPointsPerVoteNumeric.Value = Math.Clamp(profile.ChannelPointsPerVote,
            (int)_channelPointsPerVoteNumeric.Minimum, (int)_channelPointsPerVoteNumeric.Maximum);

        var triggerIndex = profile.AutoTrigger.Event switch
        {
            PollAutoTriggerEvent.StreamOnline => 1,
            PollAutoTriggerEvent.BroadcastProfileApplied => 2,
            _ => 0,
        };

        _autoTriggerComboBox.SelectedIndex = triggerIndex;

        _cooldownNumeric.Value = Math.Clamp(profile.AutoTrigger.CooldownMinutes,
            (int)_cooldownNumeric.Minimum, (int)_cooldownNumeric.Maximum);

        Text = "Редактировать профиль голосования";
        UpdateButtonsState();
    }

    private void OnFieldChanged(object? sender, EventArgs e)
    {
        UpdateButtonsState();
    }

    private void OnChannelPointsCheckedChanged(object? sender, EventArgs e)
    {
        _channelPointsPerVoteNumeric.Enabled = _channelPointsCheckBox.Checked;
    }

    private void OnSaveAsProfileClicked(object? sender, EventArgs e)
    {
        var profile = ApplyToProfile(forceName: true);

        if (!TrySaveProfile(profile))
        {
            return;
        }

        Result = profile;
        ShouldStartPoll = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnRunClicked(object? sender, EventArgs e)
    {
        var hasUserName = HasUserDefinedName();
        var profile = ApplyToProfile(forceName: false);

        if (hasUserName && !TrySaveProfile(profile))
        {
            return;
        }

        Result = profile;
        ShouldStartPoll = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TrySaveProfile(PollProfile profile)
    {
        try
        {
            Manager.Upsert(profile);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return false;
        }
    }

    private PollProfile ApplyToProfile(bool forceName)
    {
        var profile = _editTarget ?? new PollProfile();

        var name = _nameTextBox.Text.Trim();

        if (name.Length > 0)
        {
            profile.Name = name;
        }
        else if (forceName || string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = $"Свободное голосование {DateTime.Now:HH:mm:ss}";
        }

        profile.Title = _titleTextBox.Text.Trim();
        profile.Choices = ParseChoices();
        profile.DurationSeconds = (int)_durationNumeric.Value;
        profile.ChannelPointsVotingEnabled = _channelPointsCheckBox.Checked;
        profile.ChannelPointsPerVote = (int)_channelPointsPerVoteNumeric.Value;

        var triggerEvent = ((AutoTriggerChoice)_autoTriggerComboBox.SelectedItem!).Event;
        profile.AutoTrigger.Event = triggerEvent;
        profile.AutoTrigger.CooldownMinutes = (int)_cooldownNumeric.Value;

        if (triggerEvent != PollAutoTriggerEvent.BroadcastProfileApplied)
        {
            profile.AutoTrigger.BroadcastProfileId = null;
        }

        return profile;
    }

    private List<string> ParseChoices()
    {
        return _choicesTextBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private bool HasUserDefinedName()
    {
        return _nameTextBox.Text.Trim().Length > 0;
    }

    private void UpdateButtonsState()
    {
        var hasTitle = _titleTextBox.Text.Trim().Length > 0;
        var choiceCount = ParseChoices().Count;

        var basicValid = hasTitle && choiceCount is >= PollProfile.MinChoices and <= PollProfile.MaxChoices;

        _runButton.Enabled = basicValid;
        _saveAsProfileButton.Enabled = basicValid && HasUserDefinedName();
    }

    private sealed record AutoTriggerChoice(PollAutoTriggerEvent Event, string DisplayName)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
