using PoproshaykaBot.Core.Settings.Obs;

namespace PoproshaykaBot.Core.Infrastructure.Events.Settings;

public sealed record ObsIntegrationSettingsChangedEvent(ObsIntegrationSettings Settings) : EventBase;
