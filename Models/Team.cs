
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GridironLive.Models;

public class Team : INotifyPropertyChanged
{
    private string _id = "";
    private string _name = "";
    private string _displayName = "";
    private string _abbreviation = "";
    private string _score = "0";
    private string _logoUrl = "";
    private string _record = "";

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

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Abbreviation
    {
        get => _abbreviation;
        set => SetProperty(ref _abbreviation, value);
    }

    public string Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public string LogoUrl
    {
        get => _logoUrl;
        set => SetProperty(ref _logoUrl, value);
    }

    public string Record
    {
        get => _record;
        set => SetProperty(ref _record, value);
    }

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
