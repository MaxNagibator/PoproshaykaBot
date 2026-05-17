using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Infrastructure.Events;
using PoproshaykaBot.Core.Infrastructure.Events.Broadcasting;
using PoproshaykaBot.Core.Infrastructure.Events.Streaming;
using PoproshaykaBot.Core.Settings.Stores;
using PoproshaykaBot.Core.Streaming;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.WinForms.Controls;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using PoproshaykaBot.WinForms.Tiles;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

public partial class BroadcastProfilesPanel : UserControl, IDashboardTileHeaderProvider
{
    private readonly List<IDisposable> _subs = [];
    private readonly ToolStripStatusIndicator _status = new();
    private readonly BroadcastProfileCardsView _cards;
    private BroadcastProfileActionCoordinator? _actions;
    private bool _initialized;
    private Guid? _activeProfileId;
    private ToolStripButton? _addButton;
    private ToolStripButton? _editCurrentButton;

    public BroadcastProfilesPanel()
    {
        InitializeComponent();

        _cards = new(_cardsFlow, _emptyLabel);
    }

    [Inject]
    public BroadcastProfilesManager Manager { get; internal init; } = null!;

    [Inject]
    public IFormFactory Forms { get; internal init; } = null!;

    [Inject]
    public IEventBus Bus { get; internal init; } = null!;

    [Inject]
    public BroadcastProfilesStore Profiles { get; internal init; } = null!;

    [Inject]
    public IStreamStatus Stream { get; internal init; } = null!;

    [Inject]
    public IChannelInformationApplier Applier { get; internal init; } = null!;

    [Inject]
    public ITwitchChannelsApi ChannelsApi { get; internal init; } = null!;

    [Inject]
    public IBroadcasterIdProvider BroadcasterId { get; internal init; } = null!;

    public IReadOnlyList<ToolStripItem> CreateHeaderItems()
    {
        _addButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "+ Добавить",
        };

        _addButton.Click += OnAddClicked;

        _editCurrentButton = new()
        {
            AutoToolTip = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Text = "✎ Текущие",
        };

        _editCurrentButton.Click += OnEditCurrentClicked;

        return [_addButton, _editCurrentButton, _status.Label];
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

        _activeProfileId = Profiles.Load().LastAppliedProfileId;
        _actions = new(Manager, Applier, ChannelsApi, BroadcasterId, Forms, _status, this);

        _cards.ApplyRequested += async (_, card) => await _actions.ApplyAsync(card);
        _cards.EditRequested += (_, card) => _actions.Edit(card);
        _cards.DuplicateRequested += (_, card) => _actions.Duplicate(card);
        _cards.DeleteRequested += (_, card) => _actions.Delete(card);
        _cards.IncrementNumberRequested += async (_, card) => await _actions.AdjustNumberAsync(card, +1);
        _cards.DecrementNumberRequested += async (_, card) => await _actions.AdjustNumberAsync(card, -1);

        _subs.Add(Bus.SubscribeOnUi<BroadcastProfilesChanged>(this, _ => ReloadCards()));
        _subs.Add(Bus.SubscribeOnUi<BroadcastProfileApplying>(this, OnProfileApplying));
        _subs.Add(Bus.SubscribeOnUi<BroadcastProfileApplied>(this, OnProfileApplied));
        _subs.Add(Bus.SubscribeOnUi<BroadcastProfileApplyFailed>(this, OnProfileApplyFailed));
        _subs.Add(Bus.SubscribeOnUi<ChannelInformationPatched>(this, OnChannelInformationPatched));
        _subs.Add(Bus.SubscribeOnUi<ChannelInformationPatchFailed>(this, OnChannelInformationPatchFailed));
        _subs.Add(Bus.SubscribeOnUi<StreamWentOnline>(this, _ => ReloadCards()));
        _subs.Add(Bus.SubscribeOnUi<StreamWentOffline>(this, _ => ReloadCards()));
        _subs.Add(Bus.SubscribeOnUi<StreamMetadataResolved>(this, _ => ReloadCards()));
        _subs.Add(Bus.SubscribeOnUi<ChannelUpdated>(this, _ => ReloadCards()));
        _subs.DisposeOnClose(this);

        Disposed += (_, _) => _status.Dispose();

        ReloadCards();
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        _actions?.Add();
    }

    private async void OnEditCurrentClicked(object? sender, EventArgs e)
    {
        if (_actions == null)
        {
            return;
        }

        await _actions.EditCurrentAsync(busy =>
        {
            if (_editCurrentButton != null)
            {
                _editCurrentButton.Enabled = !busy;
            }
        });
    }

    private void OnProfileApplying(BroadcastProfileApplying @event)
    {
        _cards.SetApplyInFlight(@event.Profile.Id);
        _status.Show($"⏳ Применяется «{@event.Profile.Name}»…", false);
    }

    private void OnProfileApplied(BroadcastProfileApplied @event)
    {
        _activeProfileId = @event.Profile.Id;
        _cards.ClearInFlightStates();
        ReloadCards();
        _status.Show($"✓ Применён профиль «{@event.Profile.Name}»", false);
    }

    private void OnProfileApplyFailed(BroadcastProfileApplyFailed @event)
    {
        _cards.ClearInFlightStates();
        _status.Show($"✗ {@event.ErrorMessage}", true);
    }

    private void OnChannelInformationPatched(ChannelInformationPatched _)
    {
        _cards.ClearInFlightStates();
        _status.Show("✓ Применено", false);
    }

    private void OnChannelInformationPatchFailed(ChannelInformationPatchFailed @event)
    {
        _cards.ClearInFlightStates();
        _status.Show($"✗ {@event.ErrorMessage}", true);
    }

    private void ReloadCards()
    {
        var profiles = Manager.GetAll();
        var activeId = _activeProfileId ?? Profiles.Load().LastAppliedProfileId;

        if (activeId.HasValue && profiles.All(p => p.Id != activeId.Value))
        {
            _activeProfileId = null;
            activeId = null;
        }

        _cards.Reload(profiles, activeId, Stream.CurrentStream);
    }
}
