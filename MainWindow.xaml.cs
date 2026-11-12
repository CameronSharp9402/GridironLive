
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GridironLive.ViewModels;

namespace GridironLive;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private bool _isClosing;
    private bool _isRefreshingSchedule;

    private readonly DispatcherTimer _liveUpdateTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };

    private readonly DispatcherTimer _weeklyScheduleTimer = new();

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();

        DataContext = _viewModel;

        Loaded += Window_Loaded;

        _liveUpdateTimer.Tick += LiveUpdateTimer_Tick;
        _weeklyScheduleTimer.Tick += WeeklyScheduleTimer_Tick;

        Closing += (_, _) =>
        {
            _isClosing = true;

            _liveUpdateTimer.Stop();
            _weeklyScheduleTimer.Stop();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Refresh the schedule immediately when the application launches.
        await RefreshScheduleAsync();

        // Schedule the next Wednesday at 4:00 AM local time.
        ScheduleNextWeeklyRefresh();

        // Start the existing live update timer.
        _liveUpdateTimer.Start();
    }

    private async Task RefreshScheduleAsync()
    {
        if (_isClosing || _isRefreshingSchedule)
            return;

        _isRefreshingSchedule = true;

        try
        {
            await _viewModel.RefreshGamesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Schedule refresh error: {ex}");
        }
        finally
        {
            _isRefreshingSchedule = false;
        }
    }

    private void ScheduleNextWeeklyRefresh()
    {
        _weeklyScheduleTimer.Stop();

        DateTime now = DateTime.Now;

        // Wednesday = 3 when Sunday is 0.
        int daysUntilWednesday =
            ((int)DayOfWeek.Wednesday - (int)now.DayOfWeek + 7) % 7;

        DateTime nextRefresh = now.Date
            .AddDays(daysUntilWednesday)
            .AddHours(4);

        // If Wednesday at 4:00 AM has already passed,
        // schedule for the following Wednesday.
        if (nextRefresh <= now)
        {
            nextRefresh = nextRefresh.AddDays(7);
        }

        TimeSpan timeUntilRefresh = nextRefresh - now;

        _weeklyScheduleTimer.Interval = timeUntilRefresh;

        Debug.WriteLine(
            $"NEXT WEEKLY SCHEDULE REFRESH: {nextRefresh}");

        _weeklyScheduleTimer.Start();
    }

    private async void WeeklyScheduleTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _weeklyScheduleTimer.Stop();

        if (_isClosing)
            return;

        try
        {
            Debug.WriteLine(
                $"WEEKLY SCHEDULE REFRESH START: {DateTime.Now}");

            await RefreshScheduleAsync();
        }
        finally
        {
            if (!_isClosing)
            {
                // Schedule the next Wednesday at 4:00 AM.
                ScheduleNextWeeklyRefresh();
            }
        }
    }

    private async void Refresh_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshScheduleAsync();

        await _viewModel.RefreshSelectedGamePlaysAsync();
    }

    private async void LiveUpdateTimer_Tick(
        object? sender,
        EventArgs e)
    {
        _liveUpdateTimer.Stop();

        try
        {
            // Refresh scores, clocks, and game status.
            await _viewModel.RefreshGamesAsync();

            // Retrieve newly available plays and update situation.
            await _viewModel.RefreshSelectedGamePlaysAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Live update error: {ex}");
        }
        finally
        {
            if (!_isClosing)
                _liveUpdateTimer.Start();
        }
    }
}
