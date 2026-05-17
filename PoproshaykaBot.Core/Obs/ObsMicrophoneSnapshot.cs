namespace PoproshaykaBot.Core.Obs;

public sealed record ObsMicrophoneSnapshot(
    string Name,
    string Kind,
    bool IsMuted,
    double? VolumeDecibels,
    double? VolumeMultiplier);
