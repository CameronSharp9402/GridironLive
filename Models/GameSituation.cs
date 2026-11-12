namespace GridironLive.Models;

public class GameSituation
{
    public string GameId { get; set; } = "";

    public int Down { get; set; }
    public int Distance { get; set; }

    public string PossessionTeam { get; set; } = "--";
    public string FieldPosition { get; set; } = "--";

    // Numeric field position from ESPN.
    public int? YardLine { get; set; }

    public bool HasValidFieldPosition =>
        YardLine.HasValue &&
        YardLine.Value >= 0 &&
        YardLine.Value <= 100 &&
        !string.IsNullOrWhiteSpace(PossessionTeam) &&
        PossessionTeam != "--";

    public int? AwayTimeouts { get; set; }
    public int? HomeTimeouts { get; set; }

    public string DownDistance =>
        Down > 0
            ? $"{Down}{GetDownSuffix(Down)} & {Distance}"
            : "--";

    public string AwayTimeoutsDisplay =>
        AwayTimeouts?.ToString() ?? "--";

    public string HomeTimeoutsDisplay =>
        HomeTimeouts?.ToString() ?? "--";

    private static string GetDownSuffix(int down)
    {
        return down switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }
}