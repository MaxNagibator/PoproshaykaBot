using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Broadcast.Profiles;
using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.ViewModels.Controls;

public sealed partial class GameAutocompleteViewModel : ObservableObject, IDisposable
{
    private const int MinQueryLength = 2;

    private readonly IGameCategoryResolver _resolver;
    private readonly IUiTimer _debounce;
    private CancellationTokenSource? _cts;
    private bool _suppress;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _isSuggestionsOpen;

    [ObservableProperty]
    private GameSuggestion? _selected;

    public GameAutocompleteViewModel(IGameCategoryResolver resolver, IUiDispatcher uiDispatcher)
    {
        _resolver = resolver;
        _debounce = uiDispatcher.CreateTimer(TimeSpan.FromMilliseconds(300), OnDebounceTick);
    }

    public ObservableCollection<GameSuggestion> Suggestions { get; } = [];

    public void SetSelected(string gameId, string gameName)
    {
        _suppress = true;
        _debounce.Stop();
        Selected = string.IsNullOrEmpty(gameId) ? null : new GameSuggestion(gameId, gameName, string.Empty);
        Query = gameName ?? string.Empty;
        Suggestions.Clear();
        IsSuggestionsOpen = false;
        _suppress = false;
    }

    public void Dispose()
    {
        _debounce.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    partial void OnQueryChanged(string value)
    {
        if (_suppress)
        {
            return;
        }

        Selected = null;
        _debounce.Stop();
        _debounce.Start();
    }

    private void OnDebounceTick()
    {
        _debounce.Stop();
        _ = SearchAsync();
    }

    private async Task SearchAsync()
    {
        var query = Query.Trim();

        if (query.Length < MinQueryLength)
        {
            Suggestions.Clear();
            IsSuggestionsOpen = false;
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var results = await _resolver.SearchAsync(query, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Suggestions.Clear();

            foreach (var suggestion in results)
            {
                Suggestions.Add(suggestion);
            }

            IsSuggestionsOpen = Suggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            Suggestions.Clear();
            IsSuggestionsOpen = false;
        }
    }

    [RelayCommand]
    private void Choose(GameSuggestion? suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        _suppress = true;
        Selected = suggestion;
        Query = suggestion.Name;
        Suggestions.Clear();
        IsSuggestionsOpen = false;
        _suppress = false;
    }
}
