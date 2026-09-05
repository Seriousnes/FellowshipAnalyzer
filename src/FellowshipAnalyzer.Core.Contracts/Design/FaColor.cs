using System.Globalization;

namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// An sRGB colour with straight alpha, implicitly convertible from a hex string so a palette
/// definition reads as a list of literals.
/// </summary>
/// <param name="R">Red channel, 0-255.</param>
/// <param name="G">Green channel, 0-255.</param>
/// <param name="B">Blue channel, 0-255.</param>
/// <param name="A">Alpha, 0-1, where 1 is fully opaque.</param>
public readonly record struct FaColor(byte R, byte G, byte B, double A)
{
    /// <summary>Creates a fully opaque colour.</summary>
    public FaColor(byte r, byte g, byte b) : this(r, g, b, 1d)
    {
    }

    /// <summary>True when the colour has no transparency and formats as a hex literal.</summary>
    public bool IsOpaque => A >= 1d;

    /// <summary>
    /// Parses <c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c>. The leading hash is optional.
    /// </summary>
    public static FaColor Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var digits = hex.AsSpan().Trim();
        if (digits.Length > 0 && digits[0] == '#')
        {
            digits = digits[1..];
        }

        return digits.Length switch
        {
            3 => new FaColor(Nibble(digits[0]), Nibble(digits[1]), Nibble(digits[2])),
            6 => new FaColor(Byte(digits[..2]), Byte(digits[2..4]), Byte(digits[4..6])),
            8 => new FaColor(Byte(digits[..2]), Byte(digits[2..4]), Byte(digits[4..6]), Byte(digits[6..8]) / 255d),
            _ => throw new FormatException($"'{hex}' is not a #rgb, #rrggbb or #rrggbbaa colour."),
        };

        static byte Nibble(char c)
        {
            var v = Digit(c);
            return (byte)(v * 16 + v);
        }

        static byte Byte(ReadOnlySpan<char> pair) => (byte)(Digit(pair[0]) * 16 + Digit(pair[1]));

        static int Digit(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => throw new FormatException($"'{c}' is not a hex digit."),
        };
    }

    /// <summary>Implicit conversion so a palette property can be assigned a hex literal.</summary>
    public static implicit operator FaColor(string hex) => Parse(hex);

    /// <summary>Returns the same hue at a new alpha, 0-1.</summary>
    public FaColor WithAlpha(double alpha) => this with { A = Math.Clamp(alpha, 0d, 1d) };

    /// <summary>
    /// Sets HSL lightness and rounds each RGB channel, the C# equivalent of
    /// <c>step($c, $lightness)</c> in the SCSS themes. Alpha is preserved.
    /// </summary>
    /// <param name="lightnessPercent">Target lightness, 0-100.</param>
    public FaColor Step(double lightnessPercent)
    {
        var r = R / 255d;
        var g = G / 255d;
        var b = B / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2d;

        double hue = 0d;
        double saturation = 0d;

        if (max > min)
        {
            var delta = max - min;
            saturation = lightness > 0.5d ? delta / (2d - max - min) : delta / (max + min);

            if (max == r)
            {
                hue = (g - b) / delta + (g < b ? 6d : 0d);
            }
            else if (max == g)
            {
                hue = (b - r) / delta + 2d;
            }
            else
            {
                hue = (r - g) / delta + 4d;
            }

            hue /= 6d;
        }

        var target = Math.Clamp(lightnessPercent / 100d, 0d, 1d);

        if (saturation == 0d)
        {
            return new FaColor(Channel(target), Channel(target), Channel(target), A);
        }

        var q = target < 0.5d ? target * (1d + saturation) : target + saturation - target * saturation;
        var p = 2d * target - q;

        return new FaColor(
            Channel(HueToChannel(p, q, hue + 1d / 3d)),
            Channel(HueToChannel(p, q, hue)),
            Channel(HueToChannel(p, q, hue - 1d / 3d)),
            A);

        static double HueToChannel(double p, double q, double t)
        {
            if (t < 0d)
            {
                t += 1d;
            }

            if (t > 1d)
            {
                t -= 1d;
            }

            if (t < 1d / 6d)
            {
                return p + (q - p) * 6d * t;
            }

            if (t < 1d / 2d)
            {
                return q;
            }

            if (t < 2d / 3d)
            {
                return p + (q - p) * (2d / 3d - t) * 6d;
            }

            return p;
        }

        static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255d, MidpointRounding.AwayFromZero), 0d, 255d);
    }

    /// <summary>
    /// The CSS text the emitter writes: <c>#rrggbb</c> when opaque, otherwise <c>rgba(r, g, b, a)</c>.
    /// </summary>
    public string ToCss() => IsOpaque
        ? string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}")
        : string.Create(CultureInfo.InvariantCulture, $"rgba({R}, {G}, {B}, {A.ToString("0.####", CultureInfo.InvariantCulture)})");

    /// <inheritdoc />
    public override string ToString() => ToCss();
}
