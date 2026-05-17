using PoproshaykaBot.Core.Broadcast.Profiles;
using PoproshaykaBot.Core.Twitch;
using PoproshaykaBot.Core.Twitch.Helix;
using PoproshaykaBot.WinForms.Controls;
using PoproshaykaBot.WinForms.Infrastructure.Di;

namespace PoproshaykaBot.WinForms.Forms.Broadcast;

internal sealed class BroadcastProfileActionCoordinator(
    BroadcastProfilesManager manager,
    IChannelInformationApplier applier,
    ITwitchChannelsApi channelsApi,
    IBroadcasterIdProvider id,
    IFormFactory forms,
    ToolStripStatusIndicator status,
    IWin32Window dialogOwner)
{
    public void Add()
    {
        var profile = new BroadcastProfile { Name = $"Новый профиль {DateTime.Now:HH:mm:ss}" };

        if (!ShowEditDialog(profile))
        {
            return;
        }

        Persist(profile);
    }

    public async Task EditCurrentAsync(Action<bool> setBusy)
    {
        setBusy(true);
        status.Show("Загружаем текущие настройки…", false);

        BroadcastProfile? draft;

        try
        {
            var broadcasterId = await id.GetAsync(CancellationToken.None);

            if (string.IsNullOrEmpty(broadcasterId))
            {
                status.Show("✗ Не удалось определить канал", true);
                return;
            }

            var info = await channelsApi.GetChannelInformationAsync(broadcasterId, CancellationToken.None);

            if (info == null)
            {
                status.Show("✗ Не удалось загрузить настройки канала", true);
                return;
            }

            draft = new()
            {
                Id = Guid.Empty,
                Name = "(текущие настройки)",
                Title = info.Title ?? string.Empty,
                GameId = info.GameId ?? string.Empty,
                GameName = info.GameName ?? string.Empty,
                BroadcasterLanguage = string.IsNullOrEmpty(info.BroadcasterLanguage) ? "ru" : info.BroadcasterLanguage,
                Tags = info.Tags?.ToList() ?? [],
            };

            status.Clear();
        }
        catch (Exception ex)
        {
            status.Show($"✗ {HelixErrorMessages.SafeMessage(ex)}", true);
            return;
        }
        finally
        {
            setBusy(false);
        }

        using var dialog = forms.Create<BroadcastProfileEditDialog>();
        dialog.LoadFrom(draft);
        dialog.ConfigureCurrentSettingsMode();

        if (dialog.ShowDialog(dialogOwner) != DialogResult.OK)
        {
            return;
        }

        dialog.SaveTo(draft);

        var newProfileName = dialog.NewProfileName;

        if (string.IsNullOrEmpty(newProfileName))
        {
            status.Show("⏳ Применяется текущая конфигурация…", false);
            await applier.ApplyAsync(draft, CancellationToken.None);
            return;
        }

        var saved = new BroadcastProfile
        {
            Name = newProfileName,
            Title = draft.Title,
            GameId = draft.GameId,
            GameName = draft.GameName,
            BroadcasterLanguage = draft.BroadcasterLanguage,
            Tags = draft.Tags.ToList(),
        };

        try
        {
            manager.Upsert(saved);
        }
        catch (InvalidOperationException ex)
        {
            status.Show(ex.Message, true);
            return;
        }

        status.Show($"⏳ Применяется «{saved.Name}»…", false);
        await manager.ApplyAsync(saved.Id, CancellationToken.None);
    }

    public void Edit(BroadcastProfileCard card)
    {
        if (card.Profile is not { } original)
        {
            return;
        }

        var copy = Clone(original);

        if (ShowEditDialog(copy))
        {
            Persist(copy);
        }
    }

    public void Duplicate(BroadcastProfileCard card)
    {
        if (card.Profile is not { } source)
        {
            return;
        }

        var copy = new BroadcastProfile
        {
            Name = source.Name + " (копия)",
            Title = source.Title,
            GameId = source.GameId,
            GameName = source.GameName,
            BroadcasterLanguage = source.BroadcasterLanguage,
            Tags = source.Tags.ToList(),
        };

        try
        {
            manager.Upsert(copy);
        }
        catch (InvalidOperationException ex)
        {
            status.Show(ex.Message, true);
        }
    }

    public void Delete(BroadcastProfileCard card)
    {
        if (card.Profile is not { } profile)
        {
            return;
        }

        var result = MessageBox.Show(dialogOwner,
            $"Удалить профиль «{profile.Name}»?",
            "Удаление профиля",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        manager.Remove(profile.Id);
    }

    public async Task ApplyAsync(BroadcastProfileCard card)
    {
        if (card.Profile is not { } profile)
        {
            return;
        }

        card.SetApplyInFlight(true);
        status.Show($"⏳ Применяется «{profile.Name}»…", false);

        try
        {
            await manager.ApplyAsync(profile.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            card.SetApplyInFlight(false);
            status.Show($"✗ {HelixErrorMessages.SafeMessage(ex)}", true);
        }
    }

    public async Task AdjustNumberAsync(BroadcastProfileCard card, int delta)
    {
        if (card.Profile is not { } profile)
        {
            return;
        }

        var newNumber = profile.CurrentNumber + delta;
        if (newNumber < 1)
        {
            return;
        }

        var wasActive = card.IsActive;
        var profileId = profile.Id;
        var profileName = profile.Name;

        var copy = Clone(profile);
        copy.CurrentNumber = newNumber;

        try
        {
            manager.Upsert(copy);
        }
        catch (InvalidOperationException ex)
        {
            status.Show(ex.Message, true);
            return;
        }

        if (!wasActive)
        {
            return;
        }

        status.Show($"⏳ Применяется «{profileName}»…", false);

        try
        {
            await manager.ApplyAsync(profileId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            status.Show($"✗ {HelixErrorMessages.SafeMessage(ex)}", true);
        }
    }

    private static BroadcastProfile Clone(BroadcastProfile source)
    {
        return new()
        {
            Id = source.Id,
            Name = source.Name,
            Title = source.Title,
            GameId = source.GameId,
            GameName = source.GameName,
            BroadcasterLanguage = source.BroadcasterLanguage,
            Tags = source.Tags.ToList(),
            ObsSceneName = source.ObsSceneName,
            CurrentNumber = source.CurrentNumber,
            LastApplyAt = source.LastApplyAt,
            LastAutoAdvanceAt = source.LastAutoAdvanceAt,
        };
    }

    private bool ShowEditDialog(BroadcastProfile profile)
    {
        using var dialog = forms.Create<BroadcastProfileEditDialog>();
        dialog.LoadFrom(profile);

        if (dialog.ShowDialog(dialogOwner) != DialogResult.OK)
        {
            return false;
        }

        dialog.SaveTo(profile);
        return true;
    }

    private void Persist(BroadcastProfile profile)
    {
        try
        {
            manager.Upsert(profile);
        }
        catch (InvalidOperationException ex)
        {
            status.Show(ex.Message, true);
        }
    }
}
