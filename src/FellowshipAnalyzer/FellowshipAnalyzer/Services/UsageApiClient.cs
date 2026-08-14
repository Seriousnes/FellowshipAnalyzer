using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace FellowshipAnalyzer.Services;

public sealed class UsageApiClient(HttpClient http, NavigationManager navigation) : IDisposable
{
    private string? _lastPath;
    private bool _started;

    public void StartPageTracking()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        navigation.LocationChanged += OnLocationChanged;
        TrackPage();
    }

    public void TrackHero(string heroId) => Send(CurrentPath(), heroId);

    public void Dispose()
    {
        if (_started)
        {
            navigation.LocationChanged -= OnLocationChanged;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => TrackPage();

    private void TrackPage()
    {
        var path = CurrentPath();
        if (string.Equals(path, _lastPath, StringComparison.Ordinal))
        {
            return;
        }

        _lastPath = path;
        Send(path, hero: null);
    }

    private string CurrentPath()
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);

        var separator = relative.IndexOfAny(['?', '#']);
        if (separator >= 0)
        {
            relative = relative[..separator];
        }

        relative = relative.TrimEnd('/');
        return relative.Length == 0 ? "/" : "/" + relative;
    }

    private void Send(string path, string? hero) => _ = SendAsync(path, hero);

    private async Task SendAsync(string path, string? hero)
    {
        var url = $"api/track?path={Uri.EscapeDataString(path)}";
        if (hero is not null)
        {
            url += $"&hero={Uri.EscapeDataString(hero)}";
        }

        try
        {
            using var response = await http.PostAsync(url, content: null);
        }
        catch (Exception)
        {
        }
    }
}
