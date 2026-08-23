using FellowshipAnalyzer.Core.Game;

namespace FellowshipAnalyzer.SpellData;

public static class Schools
{
    private const string MagicPrefix = "Magic";
    private const string PhysicalPrefix = "Physical";

    public static MagicSchool Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? default
            : Enum.Parse<MagicSchool>(value.Replace('/', ','), ignoreCase: true);

    public static MagicSchool FromExport(List<string> schools)
    {
        var result = default(MagicSchool);
        foreach (var school in schools)
        {
            if (school.StartsWith(MagicPrefix, StringComparison.Ordinal))
                result |= MagicSchool.Magic;
            else if (school.StartsWith(PhysicalPrefix, StringComparison.Ordinal))
                result |= MagicSchool.Physical;
            else
                throw new InvalidOperationException(
                    $"The export writes damage school '{school}', which is neither Magic nor Physical.");
        }
        return result;
    }
}
