using System.Net.Http;
using System.Text.Json;
using GridironLive.Models;

namespace GridironLive.Services;

public class EspnService
{
    private const string ScoreboardUrl =
        "https://site.api.espn.com/apis/site/v2/sports/football/nfl/scoreboard";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Tracks the newest play seen for each game so diagnostics remain accurate when switching between games.
    private readonly Dictionary<string, string> _lastNewestPlayIds = new();

    public async Task<List<Game>> GetGamesAsync()
    {
        using var response = await _httpClient.GetAsync(ScoreboardUrl);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;

        var games = new List<Game>();

        if (!root.TryGetProperty("events", out var events))
            return games;

        foreach (var gameEvent in events.EnumerateArray())
        {
            if (!gameEvent.TryGetProperty(
                    "competitions", out var competitions))
                continue;

            var competition = competitions
                .EnumerateArray()
                .FirstOrDefault();

            if (competition.ValueKind != JsonValueKind.Object)
                continue;

            var game = new Game
            {
                Id = GetString(gameEvent, "id"),
                Name = GetString(gameEvent, "name"),
                ShortName = GetString(gameEvent, "shortName"),

                StartTime = DateTimeOffset.TryParse(
                    GetString(gameEvent, "date"),
                    out var date)
                    ? date
                    : DateTimeOffset.MinValue
            };

            // Game status
            if (competition.TryGetProperty(
                    "status", out var status))
            {
                game.DisplayClock =
                    GetString(status, "displayClock");

                game.Period = GetInt(status, "period");

                if (status.TryGetProperty(
                        "type", out var statusType))
                {
                    game.StatusState =
                        GetString(statusType, "state");

                    game.StatusDetail =
                        GetString(statusType, "detail");
                }
            }

            // Teams and scores
            if (competition.TryGetProperty(
                    "competitors", out var competitors))
            {
                foreach (var competitor in competitors.EnumerateArray())
                {
                    var team = ParseTeam(competitor);

                    var homeAway = GetString(
                        competitor, "homeAway");

                    if (homeAway == "home")
                        game.HomeTeam = team;

                    else if (homeAway == "away")
                        game.AwayTeam = team;
                }
            }

            // Only include games with both teams identified.
            if (!string.IsNullOrWhiteSpace(
                    game.HomeTeam.Abbreviation) &&
                !string.IsNullOrWhiteSpace(
                    game.AwayTeam.Abbreviation))
            {
                games.Add(game);
            }
        }

        return games;
    }

    private static Team ParseTeam(JsonElement competitor)
    {
        var team = new Team();

        if (!competitor.TryGetProperty(
                "team", out var teamData))
            return team;

        team.Id = GetString(teamData, "id");
        team.Name = GetString(teamData, "name");
        team.DisplayName = GetString(teamData, "displayName");
        team.Abbreviation = GetString(teamData, "abbreviation");
        team.Score = GetString(competitor, "score");

        team.LogoUrl = GetString(teamData, "logo");

        if (competitor.TryGetProperty(
                "records", out var records))
        {
            var overall = records.EnumerateArray()
                .FirstOrDefault(r =>
                    GetString(r, "name") == "overall");

            if (overall.ValueKind == JsonValueKind.Object)
                team.Record = GetString(overall, "summary");
        }

        return team;
    }

    private static string GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return "";

        if (!element.TryGetProperty(property, out JsonElement value))
            return "";

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    private static int GetInt(
        JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
            return 0;

        return value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    public async Task<List<Play>> GetPlayByPlayAsync(Game game)
    {
        string url =
            $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={game.Id}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.CacheControl =
            new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();

        System.Diagnostics.Debug.WriteLine(
            $"ESPN PBP received: {DateTime.Now:HH:mm:ss}");

        System.Diagnostics.Debug.WriteLine(
            $"ESPN response Date: {response.Headers.Date}");

        System.Diagnostics.Debug.WriteLine(
            $"ESPN response Age: {response.Headers.Age}");

        System.Diagnostics.Debug.WriteLine(
            $"ESPN Cache-Control: {response.Headers.CacheControl}");

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;

        List<Play> plays = new();

        // The summary endpoint exposes drives directly at root.drives.
        // Completed drives are in "previous"; the live drive is in "current".
        if (!root.TryGetProperty("drives", out JsonElement drives))
            return plays;

        var seenPlayIds = new HashSet<string>();

        void AddDrivePlays(JsonElement drive)
        {
            if (drive.ValueKind != JsonValueKind.Object ||
                !drive.TryGetProperty("plays", out JsonElement drivePlays) ||
                drivePlays.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement item in drivePlays.EnumerateArray())
            {
                string playId = GetString(item, "id");

                // Avoid duplicates if ESPN repeats a play in the summary payload.
                if (!string.IsNullOrEmpty(playId) &&
                    !seenPlayIds.Add(playId))
                    continue;

                JsonElement period = item.TryGetProperty("period", out var p)
                    ? p : default;

                JsonElement clock = item.TryGetProperty("clock", out var c)
                    ? c : default;

                JsonElement type = item.TryGetProperty("type", out var t)
                    ? t : default;

                JsonElement start = item.TryGetProperty("start", out var s)
                    ? s : default;

                JsonElement end = item.TryGetProperty("end", out var e)
                    ? e : default;

                int? startYardLine =
                    start.ValueKind == JsonValueKind.Object
                        ? GetNullableInt(start, "yardLine")
                        : null;

                int? endYardLine =
                    end.ValueKind == JsonValueKind.Object
                        ? GetNullableInt(end, "yardLine")
                        : null;

                JsonElement team = start.ValueKind == JsonValueKind.Object &&
                                   start.TryGetProperty("team", out var tm)
                    ? tm : default;

                int quarterNumber = period.ValueKind == JsonValueKind.Object &&
                                   period.TryGetProperty("number", out var q) &&
                                   q.TryGetInt32(out int quarter)
                    ? quarter : 0;

                string teamId = team.ValueKind == JsonValueKind.Object &&
                                team.TryGetProperty("id", out var id)
                    ? id.ToString() : "";

                string teamAbbreviation =
                    teamId == game.AwayTeam.Id
                        ? game.AwayTeam.Abbreviation
                        : teamId == game.HomeTeam.Id
                            ? game.HomeTeam.Abbreviation
                            : "";

                string startFieldTeam = teamAbbreviation;

                string endFieldTeam = "";

                if (end.ValueKind == JsonValueKind.Object &&
                    end.TryGetProperty("team", out var endTeam) &&
                    endTeam.ValueKind == JsonValueKind.Object)
                {
                    string endTeamId = GetString(endTeam, "id");

                    endFieldTeam =
                        endTeamId == game.AwayTeam.Id
                            ? game.AwayTeam.Abbreviation
                            : endTeamId == game.HomeTeam.Id
                                ? game.HomeTeam.Abbreviation
                                : "";
                }

                string downDistance = "";
                string possession = "";

                int down = 0;
                int distance = 0;
                int yardsGained = 0;

                string startFieldPosition = "";
                string endFieldPosition = "";

                if (start.ValueKind == JsonValueKind.Object)
                {
                    if (start.TryGetProperty("down", out var downValue))
                        downValue.TryGetInt32(out down);

                    if (start.TryGetProperty("distance", out var distanceValue))
                        distanceValue.TryGetInt32(out distance);

                    startFieldPosition =
                        GetString(start, "possessionText");
                }

                if (end.ValueKind == JsonValueKind.Object)
                {
                    endFieldPosition =
                        GetString(end, "possessionText");
                }

                if (item.TryGetProperty("statYardage", out var yardageValue))
                {
                    yardageValue.TryGetInt32(out yardsGained);
                }

                if (start.ValueKind == JsonValueKind.Object)
                {
                    downDistance =
                        GetString(start, "shortDownDistanceText");

                    possession =
                        GetString(start, "possessionText");
                }

                plays.Add(new Play
                {
                    Id = playId,

                    Quarter = quarterNumber switch
                    {
                        >= 1 and <= 4 => $"Q{quarterNumber}",
                        > 4 => $"OT{quarterNumber - 4}",
                        _ => "--"
                    },

                    Clock = clock.ValueKind == JsonValueKind.Object
                        ? GetString(clock, "displayValue") is string clockValue &&
                          !string.IsNullOrEmpty(clockValue)
                            ? clockValue
                            : "--:--"
                        : "--:--",

                    Description = GetString(item, "text"),

                    Type = GetString(type, "text"),

                    TeamAbbreviation = teamAbbreviation,

                    DownDistance = downDistance,

                    Possession = possession,

                    YardsGained = yardsGained,

                    Down = down,

                    Distance = distance,

                    StartFieldPosition = startFieldPosition,
                    EndFieldPosition = endFieldPosition,

                    StartYardLine = startYardLine,
                    EndYardLine = endYardLine,

                    StartFieldTeam = startFieldTeam,
                    EndFieldTeam = endFieldTeam,

                    Result = GetPlayResult(item, end),

                    AwayScore = item.TryGetProperty(
                        "awayScore", out var awayScore)
                        ? awayScore.ToString()
                        : "0",

                    HomeScore = item.TryGetProperty(
                        "homeScore", out var homeScore)
                        ? homeScore.ToString()
                        : "0",

                    IsScoringPlay = item.TryGetProperty(
                        "scoringPlay", out var scoring)
                        && scoring.ValueKind == JsonValueKind.True,

                    IsTurnover = item.TryGetProperty(
                        "isTurnover", out var turnover)
                        && turnover.ValueKind == JsonValueKind.True,

                    IsPenalty = item.TryGetProperty(
                        "isPenalty", out var penalty)
                        && penalty.ValueKind == JsonValueKind.True
                });
            }
        }

        // Completed drives
        if (drives.TryGetProperty(
                "previous", out JsonElement previous) &&
            previous.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement drive in previous.EnumerateArray())
                AddDrivePlays(drive);
        }

        // Current live drive
        if (drives.TryGetProperty(
                "current", out JsonElement current) &&
            current.ValueKind == JsonValueKind.Object)
        {
            AddDrivePlays(current);
        }

        // SequenceNumber is supplied as a string by ESPN.
        // Sort numerically so the newest play appears first.
        plays = plays
            .Select((play, index) => new
            {
                Play = play,
                Index = index,
                Sequence = TryGetSequenceNumber(root, play.Id)
            })
            .OrderByDescending(entry => entry.Sequence)
            .ThenByDescending(entry => entry.Index)
            .Select(entry => entry.Play)
            .ToList();

        // Diagnostic: determine whether ESPN's returned play data is changing.
        if (plays.Count > 0)
        {
            var newestPlay = plays[0];

            bool changed =
                !_lastNewestPlayIds.TryGetValue(
                    game.Id, out var previousId) ||
                newestPlay.Id != previousId;

            System.Diagnostics.Debug.WriteLine(
                $"PBP GAME: {game.Id}");

            System.Diagnostics.Debug.WriteLine(
                $"PBP COUNT: {plays.Count}");

            System.Diagnostics.Debug.WriteLine(
                $"PBP NEWEST: {newestPlay.Quarter} " +
                $"{newestPlay.Clock} | {newestPlay.Description}");

            System.Diagnostics.Debug.WriteLine(
                $"PBP ID: {newestPlay.Id}");

            System.Diagnostics.Debug.WriteLine(
                $"PBP CHANGED: {changed}");

            _lastNewestPlayIds[game.Id] = newestPlay.Id;
        }

        return plays;
    }

    private static int? GetNullableInt(JsonElement element, string property)
    {
            if (!element.TryGetProperty(property, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out int number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out number))
            {
                return number;
            }

            return null;
    }

    private static long TryGetSequenceNumber(
        JsonElement root, string playId)
    {
        if (!root.TryGetProperty("drives", out var drives))
            return 0;

        long FindInDrive(JsonElement drive)
        {
            if (!drive.TryGetProperty(
                    "plays", out var drivePlays) ||
                drivePlays.ValueKind != JsonValueKind.Array)
                return 0;

            foreach (var item in drivePlays.EnumerateArray())
            {
                if (GetString(item, "id") != playId)
                    continue;

                string sequence =
                    GetString(item, "sequenceNumber");

                return long.TryParse(
                    sequence, out long value)
                    ? value
                    : 0;
            }

            return 0;
        }

        if (drives.TryGetProperty(
                "previous", out var previous) &&
            previous.ValueKind == JsonValueKind.Array)
        {
            foreach (var drive in previous.EnumerateArray())
            {
                long sequence = FindInDrive(drive);

                if (sequence != 0)
                    return sequence;
            }
        }

        if (drives.TryGetProperty(
                "current", out var current) &&
            current.ValueKind == JsonValueKind.Object)
        {
            return FindInDrive(current);
        }

        return 0;
    }

    private static string GetPlayResult(
    JsonElement item,
    JsonElement end)
    {
        if (item.TryGetProperty("scoringPlay", out var scoring) &&
            scoring.ValueKind == JsonValueKind.True)
        {
            return "Score";
        }

        if (item.TryGetProperty("isTurnover", out var turnover) &&
            turnover.ValueKind == JsonValueKind.True)
        {
            return "Turnover";
        }

        if (item.TryGetProperty("isPenalty", out var penalty) &&
            penalty.ValueKind == JsonValueKind.True)
        {
            return "Penalty";
        }

        if (end.ValueKind == JsonValueKind.Object &&
            end.TryGetProperty("down", out var endDown) &&
            endDown.TryGetInt32(out int nextDown) &&
            nextDown == 1)
        {
            return "First Down";
        }

        return "";
    }

    public async Task<GameSituation?> GetGameSituationAsync(Game game)
    {
        string url =
            $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/events/{game.Id}/competitions/{game.Id}/situation";

        using var response = await _httpClient.GetAsync(url);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(stream);

        JsonElement root = document.RootElement;

        var situation = new GameSituation
        {
            GameId = game.Id,
            Down =
                root.TryGetProperty("down", out var down)
                && down.TryGetInt32(out int downValue)
                    ? downValue
                    : 0,

            Distance =
                root.TryGetProperty("distance", out var distance)
                && distance.TryGetInt32(out int distanceValue)
                    ? distanceValue
                    : 0
        };

        if (root.TryGetProperty("possession", out JsonElement possession))
        {
            System.Diagnostics.Debug.WriteLine(
                $"ESPN POSSESSION VALUE KIND: {possession.ValueKind}");

            System.Diagnostics.Debug.WriteLine(
                $"ESPN POSSESSION JSON: {possession.GetRawText()}");

            string possessionRef = "";

            if (possession.ValueKind == JsonValueKind.Object)
            {
                possessionRef = GetString(possession, "$ref");
            }
            else if (possession.ValueKind == JsonValueKind.String)
            {
                possessionRef = possession.GetString() ?? "";
            }

            System.Diagnostics.Debug.WriteLine(
                $"ESPN POSSESSION REF: {possessionRef}");

            string teamId = possessionRef
                .TrimEnd('/')
                .Split('/')
                .LastOrDefault() ?? "";

            if (teamId == game.HomeTeam.Id)
            {
                situation.PossessionTeam = game.HomeTeam.Abbreviation;
            }
            else if (teamId == game.AwayTeam.Id)
            {
                situation.PossessionTeam = game.AwayTeam.Abbreviation;
            }
            else
            {
                situation.PossessionTeam = "--";
            }

            System.Diagnostics.Debug.WriteLine(
                $"PARSED POSSESSION TEAM: {situation.PossessionTeam}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                "ESPN POSSESSION PROPERTY MISSING");
        }

        if (root.TryGetProperty("yardLine", out var yardLine) && yardLine.TryGetInt32(out int yardLineValue))
        {
            situation.YardLine = yardLineValue;

            situation.FieldPosition =
                $"{situation.PossessionTeam} {yardLineValue}";
        }

        if (root.TryGetProperty("awayTimeouts", out var awayTimeouts)
            && awayTimeouts.TryGetInt32(out int awayTimeoutValue))
        {
            situation.AwayTimeouts = awayTimeoutValue;
        }

        if (root.TryGetProperty("homeTimeouts", out var homeTimeouts)
            && homeTimeouts.TryGetInt32(out int homeTimeoutValue))
        {
            situation.HomeTimeouts = homeTimeoutValue;
        }

        return situation;
    }
}