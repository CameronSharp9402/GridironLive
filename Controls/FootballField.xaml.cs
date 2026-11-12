
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace GridironLive.Controls;

public partial class FootballField : UserControl
{
    public FootballField()
    {
        InitializeComponent();

        Loaded += (_, _) => UpdateField();
        SizeChanged += (_, _) => UpdateField();
        PlayingField.SizeChanged += (_, _) => UpdateField();
    }

    public static readonly DependencyProperty PossessionTeamProperty =
        DependencyProperty.Register(
            nameof(PossessionTeam),
            typeof(string),
            typeof(FootballField),
            new PropertyMetadata("", OnFieldDataChanged));

    public static readonly DependencyProperty FieldPositionProperty =
        DependencyProperty.Register(
            nameof(FieldPosition),
            typeof(string),
            typeof(FootballField),
            new PropertyMetadata("", OnFieldDataChanged));

    public static readonly DependencyProperty YardLineProperty =
        DependencyProperty.Register(
            nameof(YardLine),
            typeof(int?),
            typeof(FootballField),
            new PropertyMetadata(null, OnFieldDataChanged));

    public static readonly DependencyProperty AwayTeamProperty =
        DependencyProperty.Register(
            nameof(AwayTeam),
            typeof(string),
            typeof(FootballField),
            new PropertyMetadata("", OnFieldDataChanged));

    public static readonly DependencyProperty HomeTeamProperty =
        DependencyProperty.Register(
            nameof(HomeTeam),
            typeof(string),
            typeof(FootballField),
            new PropertyMetadata("", OnFieldDataChanged));

    public static readonly DependencyProperty DownProperty =
        DependencyProperty.Register(
            nameof(Down),
            typeof(int),
            typeof(FootballField),
            new PropertyMetadata(0, OnFieldDataChanged));

    public static readonly DependencyProperty DistanceProperty =
        DependencyProperty.Register(
            nameof(Distance),
            typeof(int),
            typeof(FootballField),
            new PropertyMetadata(0, OnFieldDataChanged));

    public string PossessionTeam
    {
        get => (string)GetValue(PossessionTeamProperty);
        set => SetValue(PossessionTeamProperty, value);
    }

    public string FieldPosition
    {
        get => (string)GetValue(FieldPositionProperty);
        set => SetValue(FieldPositionProperty, value);
    }

    public int? YardLine
    {
        get => (int?)GetValue(YardLineProperty);
        set => SetValue(YardLineProperty, value);
    }

    public string AwayTeam
    {
        get => (string)GetValue(AwayTeamProperty);
        set => SetValue(AwayTeamProperty, value);
    }

    public string HomeTeam
    {
        get => (string)GetValue(HomeTeamProperty);
        set => SetValue(HomeTeamProperty, value);
    }

    public int Down
    {
        get => (int)GetValue(DownProperty);
        set => SetValue(DownProperty, value);
    }

    public int Distance
    {
        get => (int)GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    private static void OnFieldDataChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is FootballField field)
            field.UpdateField();
    }

    private void UpdateField()
    {
        if (!IsLoaded)
            return;

        string possession = PossessionTeam?.Trim() ?? "";

        SituationText.Text =
            $"{possession} | {DownDisplay()}";

        if (!TryGetOffenseRelativeYardLine(out double yardLine))
        {
            HideFieldMarkers();
            SituationText.Text = "Waiting for field position";
            return;
        }

        yardLine = Math.Clamp(yardLine, 0, 100);

        double fieldWidth = PlayingField.ActualWidth;

        if (fieldWidth <= 0)
        {
            HideFieldMarkers();
            return;
        }

        // Convert the offense-relative yard line into
        // a pixel position within the actual playing field.
        double ballX = yardLine / 100.0 * fieldWidth;

        BallMarker.Visibility = Visibility.Visible;
        LineOfScrimmage.Visibility = Visibility.Visible;
        FirstDownLine.Visibility = Visibility.Visible;

        // Ball marker is centered on the yard line.
        BallMarker.Margin = new Thickness(
            ballX - BallMarker.Width / 2,
            0,
            0,
            0);

        // Blue line: current line of scrimmage.
        LineOfScrimmage.Margin = new Thickness(
            ballX,
            0,
            0,
            0);

        // Yellow line: first-down target.
        // Clamp to the opponent's goal line.
        double firstDownYard = Math.Clamp(
            yardLine + Math.Max(0, Distance),
            0,
            100);

        double firstDownX =
            firstDownYard / 100.0 * fieldWidth;

        FirstDownLine.Margin = new Thickness(
            firstDownX,
            0,
            0,
            0);
    }

    private void HideFieldMarkers()
    {
        BallMarker.Visibility = Visibility.Collapsed;
        LineOfScrimmage.Visibility = Visibility.Collapsed;
        FirstDownLine.Visibility = Visibility.Collapsed;
    }

    private string DownDisplay()
    {
        if (Down < 1 || Down > 4)
            return "--";

        return $"{Down} & {Distance}";
    }

    private bool TryGetOffenseRelativeYardLine(
        out double yardLine)
    {
        yardLine = 0;

        // Prefer numeric ESPN field position.
        if (YardLine.HasValue)
        {
            int numericYardLine = YardLine.Value;

            if (numericYardLine >= 0 &&
                numericYardLine <= 100)
            {
                yardLine = numericYardLine;
                return true;
            }

            return false;
        }

        // Fallback to the existing formatted field position.
        return TryParseFieldPosition(
            FieldPosition,
            PossessionTeam,
            out yardLine);
    }

    private bool TryParseFieldPosition(
        string fieldPosition,
        string possession,
        out double yardLine)
    {
        yardLine = 0;

        if (string.IsNullOrWhiteSpace(fieldPosition))
            return false;

        string[] parts = fieldPosition.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return false;

        string fieldTeam = parts[0];

        if (!double.TryParse(
                parts[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double yards))
        {
            return false;
        }

        if (yards < 0 || yards > 100)
            return false;

        bool ownSide = string.Equals(
            fieldTeam,
            possession,
            StringComparison.OrdinalIgnoreCase);

        yardLine = ownSide
            ? yards
            : 100 - yards;

        return true;
    }
}