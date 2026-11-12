
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GridironLive.Models;

public class Game : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _shortName = "";
    private DateTimeOffset _startTime;

    private Team _awayTeam = new();
    private Team _homeTeam = new();

    private string _statusState = "";
    private string _statusDetail = "";

    private string _displayClock = "--:--";
    private int _period;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ShortName
    {
        get => _shortName;
        set => SetProperty(ref _shortName, value);
    }

    public DateTimeOffset StartTime
    {
        get => _startTime;
        set => SetProperty(ref _startTime, value);
    }

    public Team AwayTeam
    {
        get => _awayTeam;
        set
        {
            if (SetProperty(ref _awayTeam, value))
            {
                OnPropertyChanged(nameof(DisplayScore));
            }
        }
    }

    public Team HomeTeam
    {
        get => _homeTeam;
        set
        {
            if (SetProperty(ref _homeTeam, value))
            {
                OnPropertyChanged(nameof(DisplayScore));
            }
        }
    }

    public string StatusState
    {
        get => _statusState;
        set
        {
            if (SetProperty(ref _statusState, value))
            {
                OnPropertyChanged(nameof(IsLive));
                OnPropertyChanged(nameof(DisplayStatus));
            }
        }
    }

    public string StatusDetail
    {
        get => _statusDetail;
        set => SetProperty(ref _statusDetail, value);
    }

    public string DisplayClock
    {
        get => _displayClock;
        set => SetProperty(ref _displayClock, value);
    }

    public int Period
    {
        get => _period;
        set => SetProperty(ref _period, value);
    }

    public bool IsLive => StatusState == "in";

    public string DisplayScore =>
        $"{AwayTeam.Score} : {HomeTeam.Score}";

    public string DisplayStatus =>
        StatusState switch
        {
            "in" => "LIVE",
            "post" => "FINAL",
            "pre" => "UPCOMING",
            _ => "UNKNOWN"
        };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;

        field = value;

        OnPropertyChanged(propertyName);

        return true;
    }
}
