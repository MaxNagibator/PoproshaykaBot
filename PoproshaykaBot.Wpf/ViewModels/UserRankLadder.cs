using PoproshaykaBot.Core.Users;
using PoproshaykaBot.Wpf.Infrastructure;
using System.Windows;

namespace PoproshaykaBot.Wpf.ViewModels;

public sealed record UserRankLadderStep(string Emoji, string Name, string ThresholdText, bool IsReached, bool IsCurrent)
{
    public int HiddenCount { get; init; }
    public bool IsGap => HiddenCount > 0;
    public bool IsPassed => IsReached && !IsCurrent && !IsGap;
    public bool IsAhead => !IsReached && !IsGap;
    public bool HasAbove { get; init; }
    public bool HasBelow { get; init; }
    public bool IsAboveReached { get; init; }
    public bool HasProgress { get; init; }
    public GridLength ProgressTrack { get; init; } = new(0, GridUnitType.Star);
    public GridLength RemainderTrack { get; init; } = new(1, GridUnitType.Star);
    public string ProgressText { get; init; } = string.Empty;
    public string AutomationName { get; init; } = string.Empty;
}

public sealed class UserRankLadder
{
    public const int CollapsedLimit = 8;

    private const int Neighbours = 2;

    private static readonly PointTerm StepTerm = new() { Singular = "ступень", Few = "ступени", Many = "ступеней" };

    private readonly int _currentIndex;

    private UserRankLadder(IReadOnlyList<UserRankLadderStep> steps, int currentIndex, string summary)
    {
        Steps = steps;
        _currentIndex = currentIndex;
        Summary = summary;
    }

    public IReadOnlyList<UserRankLadderStep> Steps { get; }

    public UserRankLadderStep Current => Steps[_currentIndex];

    public string Summary { get; }

    public bool CanCollapse => Steps.Count > CollapsedLimit && Collapse().Count < Steps.Count;

    public string ExpandText => $"Все ступени ({Steps.Count})";

    public static UserRankLadder? Build(long points, UserRankStanding standing, PointTerm pointTerm)
    {
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(pointTerm);

        if (standing.Ranks.Count == 0)
        {
            return null;
        }

        var ordered = standing.Ranks.OrderByDescending(rank => rank.MinMessages).ToList();
        var reachedIndex = points < 0 ? -1 : ordered.FindIndex(rank => (long)rank.MinMessages <= points);
        var steps = new List<UserRankLadderStep>(ordered.Count + 1);

        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = ordered[i];
            var isReached = reachedIndex >= 0 && i >= reachedIndex;
            var isCurrent = i == reachedIndex;
            var threshold = (long)rank.MinMessages;
            var thresholdText = threshold.ToString("N0", UiCulture.Russian);
            var from = $"от {thresholdText} {pointTerm.ForCount(threshold)}";

            var automationName = isCurrent
                ? $"{rank.DisplayName}, {from}, текущий ранг"
                : isReached
                    ? $"{rank.DisplayName}, {from}, пройден"
                    : $"{rank.DisplayName}, {from}";

            steps.Add(new(rank.Emoji, rank.DisplayName, thresholdText, isReached, isCurrent) { AutomationName = automationName });
        }

        if (reachedIndex < 0)
        {
            var current = standing.Current;
            steps.Add(new(current.Emoji, current.DisplayName, string.Empty, true, true) { AutomationName = $"{current.DisplayName}, текущий ранг" });
        }

        var currentIndex = reachedIndex >= 0 ? reachedIndex : steps.Count - 1;
        var currentStep = steps[currentIndex];
        var next = currentIndex > 0 ? steps[currentIndex - 1] : null;

        if (next is null || standing.IsTopRank)
        {
            steps[currentIndex] = currentStep with { ProgressText = "Максимальный ранг" };

            return new(steps, currentIndex, $"Путь по рангам: ранг {currentStep.Name}, это максимальный ранг");
        }

        var toNext = standing.PointsToNext;
        var remainder = string.Create(UiCulture.Russian, $"ещё {toNext:N0} {pointTerm.ForCount(toNext)}");
        var nextThreshold = toNext + points;

        steps[currentIndex] = currentStep with
        {
            HasProgress = true,
            ProgressTrack = new(standing.Progress, GridUnitType.Star),
            RemainderTrack = new(1 - standing.Progress, GridUnitType.Star),
            ProgressText = string.Create(UiCulture.Russian, $"{remainder} · {points:N0} из {nextThreshold:N0}"),
        };

        return new(steps, currentIndex, $"Путь по рангам: ранг {currentStep.Name}, до ранга {next.Name} {remainder}");
    }

    public IReadOnlyList<UserRankLadderStep> Arrange(bool expanded)
    {
        if (expanded || !CanCollapse)
        {
            return Link(Steps);
        }

        return Link(Collapse());
    }

    private List<UserRankLadderStep> Collapse()
    {
        var keep = new bool[Steps.Count];
        keep[0] = true;
        keep[^1] = true;

        for (var i = Math.Max(0, _currentIndex - Neighbours); i <= Math.Min(Steps.Count - 1, _currentIndex + Neighbours); i++)
        {
            keep[i] = true;
        }

        var visible = new List<UserRankLadderStep>();
        var index = 0;

        while (index < Steps.Count)
        {
            if (keep[index])
            {
                visible.Add(Steps[index]);
                index++;
                continue;
            }

            var start = index;

            while (index < Steps.Count && !keep[index])
            {
                index++;
            }

            var hidden = index - start;

            if (hidden == 1)
            {
                visible.Add(Steps[start]);
                continue;
            }

            var gapText = $"ещё {hidden} {StepTerm.ForCount(hidden)}";

            visible.Add(new("⋯", gapText, string.Empty, Steps[start].IsReached, false)
            {
                HiddenCount = hidden,
                AutomationName = $"Свёрнуто: {gapText}",
            });
        }

        return visible;
    }

    private static List<UserRankLadderStep> Link(IReadOnlyList<UserRankLadderStep> steps)
    {
        var linked = new List<UserRankLadderStep>(steps.Count);

        for (var i = 0; i < steps.Count; i++)
        {
            linked.Add(steps[i] with
            {
                HasAbove = i > 0,
                HasBelow = i < steps.Count - 1,
                IsAboveReached = i > 0 && steps[i - 1].IsReached,
            });
        }

        return linked;
    }
}
