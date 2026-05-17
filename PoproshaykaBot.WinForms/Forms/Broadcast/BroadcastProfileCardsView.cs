using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Streaming;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

internal sealed class BroadcastProfileCardsView
{
    private const int MinCardWidth = 220;
    private const int CardMargin = 6;

    private readonly FlowLayoutPanel _cardsFlow;
    private readonly Label _emptyLabel;

    public BroadcastProfileCardsView(FlowLayoutPanel cardsFlow, Label emptyLabel)
    {
        _cardsFlow = cardsFlow;
        _emptyLabel = emptyLabel;

        _cardsFlow.ClientSizeChanged += (_, _) => RecomputeWidths();
    }

    public event EventHandler<BroadcastProfileCard>? ApplyRequested;
    public event EventHandler<BroadcastProfileCard>? DecrementNumberRequested;
    public event EventHandler<BroadcastProfileCard>? DeleteRequested;
    public event EventHandler<BroadcastProfileCard>? DuplicateRequested;
    public event EventHandler<BroadcastProfileCard>? EditRequested;
    public event EventHandler<BroadcastProfileCard>? IncrementNumberRequested;

    public void Reload(IReadOnlyList<BroadcastProfile> profiles, Guid? activeId, StreamInfo? currentStream)
    {
        _cardsFlow.SuspendLayout();

        try
        {
            foreach (var child in _cardsFlow.Controls.Cast<Control>().ToList())
            {
                if (child is not BroadcastProfileCard oldCard)
                {
                    continue;
                }

                Detach(oldCard);
                oldCard.Dispose();
            }

            _cardsFlow.Controls.Clear();

            var cardWidth = ComputeWidth();

            foreach (var profile in profiles)
            {
                var isActive = activeId.HasValue && activeId.Value == profile.Id;
                var hasDrift = isActive && BroadcastProfileStreamComparer.ProfileDivergesFromStream(profile, currentStream);

                var card = new BroadcastProfileCard { Width = cardWidth };
                card.UpdateFrom(profile, isActive, hasDrift);
                Attach(card);
                _cardsFlow.Controls.Add(card);
            }

            var hasProfiles = _cardsFlow.Controls.Count > 0;
            _emptyLabel.Visible = !hasProfiles;
            _cardsFlow.Visible = hasProfiles;
        }
        finally
        {
            _cardsFlow.ResumeLayout();
        }
    }

    public void SetApplyInFlight(Guid profileId)
    {
        foreach (var control in _cardsFlow.Controls)
        {
            if (control is not BroadcastProfileCard card || card.Profile?.Id != profileId)
            {
                continue;
            }

            card.SetApplyInFlight(true);
            return;
        }
    }

    public void ClearInFlightStates()
    {
        foreach (var control in _cardsFlow.Controls)
        {
            if (control is BroadcastProfileCard card)
            {
                card.SetApplyInFlight(false);
            }
        }
    }

    private void OnApplyRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            ApplyRequested?.Invoke(this, card);
        }
    }

    private void OnEditRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            EditRequested?.Invoke(this, card);
        }
    }

    private void OnDuplicateRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            DuplicateRequested?.Invoke(this, card);
        }
    }

    private void OnDeleteRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            DeleteRequested?.Invoke(this, card);
        }
    }

    private void OnIncrementNumberRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            IncrementNumberRequested?.Invoke(this, card);
        }
    }

    private void OnDecrementNumberRequested(object? sender, EventArgs e)
    {
        if (sender is BroadcastProfileCard card)
        {
            DecrementNumberRequested?.Invoke(this, card);
        }
    }

    private int ComputeWidth()
    {
        var available = _cardsFlow.ClientSize.Width - _cardsFlow.Padding.Horizontal;
        if (available <= MinCardWidth)
        {
            return Math.Max(MinCardWidth, available);
        }

        var columns = Math.Max(1, (available + CardMargin) / (MinCardWidth + CardMargin));
        return Math.Max(MinCardWidth, (available - columns * CardMargin) / columns);
    }

    private void RecomputeWidths()
    {
        var width = ComputeWidth();

        foreach (var control in _cardsFlow.Controls)
        {
            if (control is BroadcastProfileCard card)
            {
                card.Width = width;
            }
        }
    }

    private void Attach(BroadcastProfileCard card)
    {
        card.ApplyRequested += OnApplyRequested;
        card.EditRequested += OnEditRequested;
        card.IncrementNumberRequested += OnIncrementNumberRequested;
        card.DecrementNumberRequested += OnDecrementNumberRequested;
        card.DuplicateRequested += OnDuplicateRequested;
        card.DeleteRequested += OnDeleteRequested;
    }

    private void Detach(BroadcastProfileCard card)
    {
        card.ApplyRequested -= OnApplyRequested;
        card.EditRequested -= OnEditRequested;
        card.IncrementNumberRequested -= OnIncrementNumberRequested;
        card.DecrementNumberRequested -= OnDecrementNumberRequested;
        card.DuplicateRequested -= OnDuplicateRequested;
        card.DeleteRequested -= OnDeleteRequested;
    }
}
