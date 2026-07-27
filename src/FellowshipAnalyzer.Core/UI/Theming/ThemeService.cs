using System.Text.Json;
using System.Text.Json.Serialization;
using FellowshipAnalyzer.Core.Contracts.Design;
using Microsoft.JSInterop;

namespace FellowshipAnalyzer.Core.UI.Theming;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ThemeJsonContext : JsonSerializerContext;

/// <summary>
/// Selects the active <see cref="FaTheme"/> and overrides individual design tokens at runtime.
/// <para>
/// A theme is selected by writing the root element's <c>data-theme</c> attribute, which is the one
/// switch the generated stylesheet listens to: one attribute write moves every token to its value
/// under the new theme, so the browser is never handed a fresh block of CSS to parse.
/// </para>
/// <para>
/// An override is a single declaration in the root element's style attribute. A style-attribute
/// declaration outranks every author-origin rule whatever its selector, so one property beats the
/// generated <c>:root</c> block while every other token keeps coming from the stylesheet. Because
/// <c>var()</c> substitutes at computed-value time, after the cascade has resolved, a
/// <c>color-mix()</c> that reads an overridden token re-resolves live. Clearing an override removes
/// the declaration, so the token falls back to the block for whichever theme is selected rather
/// than to a value this service would have to remember.
/// </para>
/// Choices are persisted to <c>localStorage</c>, and are resilient to JS interop being unavailable
/// during pre-rendering.
/// </summary>
public sealed class ThemeService(IJSRuntime js) : IAsyncDisposable
{
    /// <summary>The <c>localStorage</c> key holding the selected theme's <see cref="FaTheme.Selector"/>.</summary>
    public const string ThemeStorageKey = "fa-theme";

    /// <summary>The <c>localStorage</c> key holding the token overrides, as token name to CSS text.</summary>
    public const string OverrideStorageKey = "fa-token-overrides";

    private const string ModulePath = "./_content/FellowshipAnalyzer.Core/js/theme.js";

    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);

    private IJSObjectReference? _module;
    private Task? _restore;

    /// <summary>Raised after the theme or any override changes.</summary>
    public event Action? Changed;

    /// <summary>The selected theme. Every token not overridden resolves to this theme's value.</summary>
    public FaTheme Theme { get; private set; } = FaTheme.Original;

    /// <summary>The overridden tokens, keyed by token name without the <c>--fa-</c> prefix.</summary>
    public IReadOnlyDictionary<string, string> Overrides => _overrides;

    /// <summary>The CSS text in force for a token: its override when it has one, else the theme's value.</summary>
    public string ValueOf(FaToken token) =>
        _overrides.TryGetValue(token.Name, out var value) ? value : token.Value;

    /// <summary>True when the token carries an override rather than the theme's value.</summary>
    public bool IsOverridden(FaToken token) => _overrides.ContainsKey(token.Name);

    /// <summary>
    /// Reads the persisted theme and overrides and applies them, once per service instance.
    /// Call from <c>OnAfterRenderAsync(firstRender: true)</c>, where JS interop is available.
    /// </summary>
    public Task RestoreAsync() => _restore ??= RestoreCoreAsync();

    /// <summary>Selects a theme, dropping any override the new theme's own value already matches.</summary>
    public async Task SetThemeAsync(FaTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (theme.Name == Theme.Name)
        {
            return;
        }

        Theme = theme;
        await InvokeAsync("setTheme", theme.Selector);

        foreach (var token in theme.Tokens)
        {
            if (_overrides.TryGetValue(token.Name, out var value) && value == token.Value)
            {
                _overrides.Remove(token.Name);
                await InvokeAsync("clearToken", token.CustomProperty);
            }
        }

        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Overrides one token with CSS text.</summary>
    /// <param name="name">The token name without its <c>--fa-</c> prefix, for example <c>bg-card</c>.</param>
    /// <param name="value">The CSS text to apply, for example <c>#3a4452</c>.</param>
    public async Task SetTokenAsync(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _overrides[name] = value;
        await InvokeAsync("setToken", FaToken.Prefix + name, value);
        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Clears one override, returning the token to the selected theme's value.</summary>
    public async Task ClearTokenAsync(string name)
    {
        if (!_overrides.Remove(name))
        {
            return;
        }

        await InvokeAsync("clearToken", FaToken.Prefix + name);
        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Clears every override, returning all tokens to the selected theme's values.</summary>
    public async Task ClearOverridesAsync()
    {
        if (_overrides.Count == 0)
        {
            return;
        }

        var names = _overrides.Keys.ToArray();
        _overrides.Clear();

        foreach (var name in names)
        {
            await InvokeAsync("clearToken", FaToken.Prefix + name);
        }

        await PersistAsync();
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private async Task RestoreCoreAsync()
    {
        var selector = await GetItemAsync(ThemeStorageKey);
        foreach (var theme in FaTheme.All)
        {
            if (string.Equals(theme.Selector, selector, StringComparison.OrdinalIgnoreCase))
            {
                Theme = theme;
                break;
            }
        }

        var known = Theme.Tokens.Select(token => token.Name).ToHashSet(StringComparer.Ordinal);

        var json = await GetItemAsync(OverrideStorageKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var stored = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.DictionaryStringString);
                if (stored is not null)
                {
                    foreach (var pair in stored)
                    {
                        if (known.Contains(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                        {
                            _overrides[pair.Key] = pair.Value;
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        await InvokeAsync("setTheme", Theme.Selector);

        foreach (var pair in _overrides)
        {
            await InvokeAsync("setToken", FaToken.Prefix + pair.Key, pair.Value);
        }

        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        await SetItemAsync(ThemeStorageKey, Theme.Selector);
        await SetItemAsync(OverrideStorageKey, JsonSerializer.Serialize(_overrides, ThemeJsonContext.Default.DictionaryStringString));
    }

    private async Task<string?> GetItemAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    private async Task SetItemAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private async Task InvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await _module.InvokeVoidAsync(identifier, args);
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
