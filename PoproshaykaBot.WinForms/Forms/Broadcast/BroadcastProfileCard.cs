using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

public sealed partial class BroadcastProfileCard : UserControl
{
    private bool _initialized;
    private Font? _nameFont;
    private Font? _boldFont;
    private Font? _titleFont;

    public BroadcastProfileCard()
    {
        InitializeComponent();
    }

    public event EventHandler? ApplyRequested;
    public event EventHandler? DecrementNumberRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? DuplicateRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? IncrementNumberRequested;

    public BroadcastProfile? Profile { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdateFrom(BroadcastProfile profile, bool isActive, bool hasDrift)
    {
        Profile = profile;
        IsActive = isActive;

        _nameLabel.Text = profile.Name;

        var titleTemplate = MessageTemplate.For(profile.Title);
        var hasNumberPlaceholder = titleTemplate.Contains("n");
        if (hasNumberPlaceholder)
        {
            _numberBadge.Text = $"#{profile.CurrentNumber}";
            _numberBadge.Visible = true;
        }
        else
        {
            _numberBadge.Visible = false;
        }

        _numberMenuSeparator.Visible = hasNumberPlaceholder;
        _incrementNumberMenuItem.Visible = hasNumberPlaceholder;
        _decrementNumberMenuItem.Visible = hasNumberPlaceholder;
        _decrementNumberMenuItem.Enabled = hasNumberPlaceholder && profile.CurrentNumber > 1;

        if (string.IsNullOrWhiteSpace(profile.Title))
        {
            _titleLabel.Visible = false;
            _titleLabel.Text = string.Empty;
        }
        else
        {
            _titleLabel.Text = $"«{profile.Title}»";
            _titleLabel.Visible = true;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.GameName))
        {
            parts.Add("🎮 " + profile.GameName);
        }

        if (profile.Tags.Count > 0)
        {
            parts.Add(string.Join(" ", profile.Tags.Select(t => "#" + t)));
        }

        if (!string.IsNullOrWhiteSpace(profile.ObsSceneName))
        {
            parts.Add("🎬 " + profile.ObsSceneName);
        }

        if (!string.IsNullOrWhiteSpace(profile.BroadcasterLanguage))
        {
            parts.Add(profile.BroadcasterLanguage.ToUpperInvariant());
        }

        _metaLabel.Text = string.Join("  •  ", parts);
        _metaLabel.Visible = _metaLabel.Text.Length > 0;

        _driftLabel.Visible = hasDrift;
        _activeBadge.Visible = isActive;

        BackColor = isActive
            ? Color.FromArgb(240, 255, 240)
            : SystemColors.Window;
    }

    public void SetApplyInFlight(bool inFlight)
    {
        _applyingLabel.Visible = inFlight;
        _applyMenuItem.Enabled = !inFlight;
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

        _nameFont = new(Font.FontFamily, Font.Size + 1, FontStyle.Bold, Font.Unit, Font.GdiCharSet);
        _boldFont = new(Font, FontStyle.Bold);
        _titleFont = new(Font, FontStyle.Italic);

        _nameLabel.Font = _nameFont;
        _activeBadge.Font = _boldFont;
        _numberBadge.Font = _boldFont;
        _titleLabel.Font = _titleFont;

        Disposed += OnDisposedReleaseFonts;

        SubscribeDoubleClickRecursively(this);
    }

    private void OnDisposedReleaseFonts(object? sender, EventArgs e)
    {
        _nameFont?.Dispose();
        _boldFont?.Dispose();
        _titleFont?.Dispose();
    }

    private void OnCardDoubleClick(object? sender, EventArgs e)
    {
        ApplyRequested?.Invoke(this, e);
    }

    private void OnApplyMenuClicked(object? sender, EventArgs e)
    {
        ApplyRequested?.Invoke(this, e);
    }

    private void OnEditMenuClicked(object? sender, EventArgs e)
    {
        EditRequested?.Invoke(this, e);
    }

    private void OnIncrementNumberMenuClicked(object? sender, EventArgs e)
    {
        IncrementNumberRequested?.Invoke(this, e);
    }

    private void OnDecrementNumberMenuClicked(object? sender, EventArgs e)
    {
        DecrementNumberRequested?.Invoke(this, e);
    }

    private void OnDuplicateMenuClicked(object? sender, EventArgs e)
    {
        DuplicateRequested?.Invoke(this, e);
    }

    private void OnDeleteMenuClicked(object? sender, EventArgs e)
    {
        DeleteRequested?.Invoke(this, e);
    }

    private void SubscribeDoubleClickRecursively(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            child.DoubleClick += OnCardDoubleClick;
            SubscribeDoubleClickRecursively(child);
        }
    }
}
