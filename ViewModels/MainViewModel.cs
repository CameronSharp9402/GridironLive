
using GridironLive.Models;
using GridironLive.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace GridironLive.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly EspnService _espnService = new();

    private Game? _selectedGame;
    private GameSituation? _gameSituation;
    private string _connectionStatus = "CONNECTING...";
    private bool _isLoading;
    private bool _isLoadingPlays;
    private bool _isRefreshingPlays;
    private int _playRequestVersion;

    public ObservableCollection<Game> Games { get; } = new();
    public ObservableCollection<Play> Plays { get; } = new();

    public Game? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (_selectedGame?.Id == value?.Id)
            {
                // Update the reference without reloading the entire play history
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    OnPropertyChanged();
                }

                return;
            }

            _selectedGame = value;
            OnPropertyChanged();

            if (value != null)
            {
                GameSituation = null;

                _ = LoadPlaysAsync(value);
                _ = LoadGameSituationAsync(value);
            }
        }
    }
    public GameSituation? GameSituation
    {
        get => _gameSituation;
        private set
        {
            if (_gameSituation == value)
                return;

            _gameSituation = value;
            OnPropertyChanged();
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set
        {
            if (_connectionStatus == value)
                return;

            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingPlays
    {
        get => _isLoadingPlays;
        set
        {
            _isLoadingPlays = value;
            OnPropertyChanged();
        }
    }

    private string _playByPlayStatus = "Select a game to view play-by-play.";
    public string PlayByPlayStatus
    {
        get => _playByPlayStatus;
        set
        {
            _playByPlayStatus = value;
            OnPropertyChanged();
        }
    }

    public async Task RefreshGamesAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            ConnectionStatus = "CONNECTING...";

            var previousGameId = SelectedGame?.Id;

            var games = await _espnService.GetGamesAsync();

            // Update existing games without clearing the collection.
            foreach (var game in games)
            {
                var existingGame = Games.FirstOrDefault(
                    g => g.Id == game.Id);

                if (existingGame != null)
                {
                    existingGame.Name = game.Name;
                    existingGame.ShortName = game.ShortName;
                    existingGame.StartTime = game.StartTime;

                    existingGame.StatusState = game.StatusState;
                    existingGame.StatusDetail = game.StatusDetail;
                    existingGame.DisplayClock = game.DisplayClock;
                    existingGame.Period = game.Period;

                    existingGame.AwayTeam.Abbreviation =
                        game.AwayTeam.Abbreviation;

                    existingGame.AwayTeam.Name =
                        game.AwayTeam.Name;

                    existingGame.AwayTeam.DisplayName =
                        game.AwayTeam.DisplayName;

                    existingGame.AwayTeam.Score =
                        game.AwayTeam.Score;

                    existingGame.AwayTeam.LogoUrl =
                        game.AwayTeam.LogoUrl;

                    existingGame.AwayTeam.Record =
                        game.AwayTeam.Record;

                    existingGame.HomeTeam.Abbreviation =
                        game.HomeTeam.Abbreviation;

                    existingGame.HomeTeam.Name =
                        game.HomeTeam.Name;

                    existingGame.HomeTeam.DisplayName =
                        game.HomeTeam.DisplayName;

                    existingGame.HomeTeam.Score =
                        game.HomeTeam.Score;

                    existingGame.HomeTeam.LogoUrl =
                        game.HomeTeam.LogoUrl;

                    existingGame.HomeTeam.Record =
                        game.HomeTeam.Record;
                }
                else
                {
                    Games.Add(game);
                }
            }
        

            // Remove games that are no longer returned by ESPN.
            var currentGameIds = games
                .Select(g => g.Id)
                .ToHashSet();

            for (int i = Games.Count - 1; i >= 0; i--)
            {
                if (!currentGameIds.Contains(Games[i].Id))
                {
                    Games.RemoveAt(i);
                }
            }

            // Preserve the currently selected game.
            var selectedGame = Games.FirstOrDefault(
                g => g.Id == previousGameId);

            if (selectedGame != null)
            {
                // Keep the same selected object.
                SelectedGame = selectedGame;
            }
            else
            {
                // Only select a different game if the current one disappeared.
                SelectedGame = Games.FirstOrDefault(g => g.IsLive)
                    ?? Games.FirstOrDefault();
            }

            ConnectionStatus = "CONNECTED";
        }
        catch (Exception ex)
        {
            ConnectionStatus = "CONNECTION ERROR";

            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    private async Task LoadPlaysAsync(Game game)
    {
        int requestVersion = ++_playRequestVersion;

        Plays.Clear();

        IsLoadingPlays = true;
        PlayByPlayStatus = "Loading play-by-play...";

        try
        {
            var plays = await _espnService.GetPlayByPlayAsync(game);

            // Ignore results from an older game selection.
            if (requestVersion != _playRequestVersion)
                return;

            foreach (var play in plays)
            {
                Plays.Add(play);
            }

            // Update possession using the loaded plays.
            ApplyPossessionFromPlays(game);

            PlayByPlayStatus = Plays.Count == 0
                ? "No play-by-play data available for this game."
                : $"{Plays.Count} plays loaded";
        }
        catch (Exception ex)
        {
            if (requestVersion != _playRequestVersion)
                return;

            PlayByPlayStatus = "Unable to load play-by-play.";

            Debug.WriteLine(ex);
        }
        finally
        {
            if (requestVersion == _playRequestVersion)
                IsLoadingPlays = false;
        }
    }

    public async Task RefreshSelectedGamePlaysAsync()
    {
        Game? game = SelectedGame;

        if (game == null || !game.IsLive || _isRefreshingPlays)
            return;

        _isRefreshingPlays = true;

        try
        {
            // Refresh down, distance, field position, and timeouts.
            await LoadGameSituationAsync(game);

            var latestPlays =
                await _espnService.GetPlayByPlayAsync(game);

            // Ignore results if another game was selected.
            if (SelectedGame?.Id != game.Id)
                return;

            // ESPN returns newest first.
            var validLatestPlays = latestPlays
                .Where(play => !string.IsNullOrWhiteSpace(play.Id))
                .GroupBy(play => play.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            var latestIds = validLatestPlays
                .Select(play => play.Id)
                .ToHashSet(StringComparer.Ordinal);

            // Remove plays that ESPN no longer reports.
            for (int i = Plays.Count - 1; i >= 0; i--)
            {
                if (!latestIds.Contains(Plays[i].Id))
                {
                    Plays.RemoveAt(i);
                }
            }

            // Synchronize ESPN's complete snapshot.
            for (int i = 0; i < validLatestPlays.Count; i++)
            {
                Play latestPlay = validLatestPlays[i];

                if (i < Plays.Count &&
                    Plays[i].Id == latestPlay.Id)
                {
                    // Same play, same position.
                    // Replace the object so corrected values update bindings.
                    Plays[i] = latestPlay;
                }
                else
                {
                    // Look for this play later in the collection.
                    int existingIndex = -1;

                    for (int j = i + 1; j < Plays.Count; j++)
                    {
                        if (Plays[j].Id == latestPlay.Id)
                        {
                            existingIndex = j;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        // Move the existing play into its correct position.
                        Plays.RemoveAt(existingIndex);
                        Plays.Insert(i, latestPlay);
                    }
                    else
                    {
                        // ESPN has added a new play.
                        Plays.Insert(i, latestPlay);
                    }
                }
            }

            // Remove any extra entries beyond ESPN's snapshot.
            while (Plays.Count > validLatestPlays.Count)
            {
                Plays.RemoveAt(Plays.Count - 1);
            }

            // Update possession after the latest play snapshot is synchronized.
            ApplyPossessionFromPlays(game);

            PlayByPlayStatus =
                $"{Plays.Count} plays loaded · Last updated {DateTime.Now:h:mm:ss tt}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Play refresh error: {ex}");
        }
        finally
        {
            _isRefreshingPlays = false;
        }
    }

    private async Task LoadGameSituationAsync(Game game)
    {
        Debug.WriteLine($"SITUATION REQUEST START: {game.Id}");

        try
        {
            var situation =
                await _espnService.GetGameSituationAsync(game);

            Debug.WriteLine(
                $"SITUATION REQUEST COMPLETE: {game.Id}, " +
                $"Possession={situation?.PossessionTeam}, " +
                $"FieldPosition={situation?.FieldPosition}");

            // Ignore results if another game was selected.
            if (SelectedGame?.Id != game.Id)
                return;

            if (situation == null)
                return;

            // The situation endpoint does not provide possession.
            // Preserve possession from the existing play list.
            Play? latestPlay = Plays.FirstOrDefault(
                play => !string.IsNullOrWhiteSpace(play.TeamAbbreviation));

            if (latestPlay != null)
            {
                string possessionTeam = latestPlay.TeamAbbreviation.Trim();

                if (possessionTeam == game.HomeTeam.Abbreviation ||
                    possessionTeam == game.AwayTeam.Abbreviation)
                {
                    situation.PossessionTeam = possessionTeam;

                    situation.FieldPosition =
                        situation.YardLine.HasValue
                            ? $"{possessionTeam} {situation.YardLine.Value}"
                            : "--";
                }
            }

            GameSituation = situation;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Game situation error: {ex}");
        }
    }

    private void ApplyPossessionFromPlays(Game game)
    {
        if (SelectedGame?.Id != game.Id)
            return;

        if (Plays.Count == 0)
            return;

        // ESPN returns the newest play first.
        Play? latestPlay = Plays.FirstOrDefault(
            play => !string.IsNullOrWhiteSpace(play.TeamAbbreviation));

        if (latestPlay == null)
            return;

        string possessionTeam = latestPlay.TeamAbbreviation.Trim();

        // Only accept a team belonging to this game.
        if (possessionTeam != game.HomeTeam.Abbreviation &&
            possessionTeam != game.AwayTeam.Abbreviation)
        {
            Debug.WriteLine(
                $"POSSESSION IGNORED: Unknown team '{possessionTeam}'");

            return;
        }

        GameSituation? currentSituation = GameSituation;

        if (currentSituation == null)
            return;

        if (currentSituation.PossessionTeam == possessionTeam)
            return;

        Debug.WriteLine(
            $"POSSESSION UPDATED: {currentSituation.PossessionTeam} -> {possessionTeam}");

        GameSituation = new GameSituation
        {
            GameId = currentSituation.GameId,

            Down = currentSituation.Down,
            Distance = currentSituation.Distance,

            PossessionTeam = possessionTeam,

            YardLine = currentSituation.YardLine,

            FieldPosition =
                currentSituation.YardLine.HasValue
                    ? $"{possessionTeam} {currentSituation.YardLine.Value}"
                    : "--",

            AwayTimeouts = currentSituation.AwayTimeouts,
            HomeTimeouts = currentSituation.HomeTimeouts
        };
    }
}