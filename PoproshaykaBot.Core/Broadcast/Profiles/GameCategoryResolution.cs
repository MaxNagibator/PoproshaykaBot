namespace PoproshaykaBot.Core.Broadcast.Profiles;

public sealed record GameCategoryResolution(GameSuggestion? Match, IReadOnlyList<GameSuggestion> Candidates);
