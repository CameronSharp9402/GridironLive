
namespace GridironLive.Models;

public class Play
{
    public string Id { get; set; } = "";

    public string Quarter { get; set; } = "";
    public string Clock { get; set; } = "";

    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string TeamAbbreviation { get; set; } = "";

    public string DownDistance { get; set; } = "";
    public string Possession { get; set; } = "";
    public string TeamLogoUrl { get; set; } = "";

    // Play details
    public int YardsGained { get; set; }
    public int Down { get; set; }
    public int Distance { get; set; }

    // Field position
    public string StartFieldPosition { get; set; } = "";
    public string EndFieldPosition { get; set; } = "";
    public int? StartYardLine { get; set; }
    public int? EndYardLine { get; set; }

    public string StartFieldTeam { get; set; } = "";
    public string EndFieldTeam { get; set; } = "";

    // Play result
    public string Result { get; set; } = "";

    public string AwayScore { get; set; } = "0";
    public string HomeScore { get; set; } = "0";

    public bool IsScoringPlay { get; set; }
    public bool IsTurnover { get; set; }
    public bool IsPenalty { get; set; }

    // Display properties for important plays
    public bool IsImportantPlay =>
        IsScoringPlay ||
        IsTurnover ||
        IsPenalty ||
        Result == "First Down";

    public string HighlightLabel =>
        IsScoringPlay ? "SCORING PLAY" :
        IsTurnover ? "TURNOVER" :
        IsPenalty ? "PENALTY" :
        Result == "First Down" ? "FIRST DOWN" :
        "";
}
