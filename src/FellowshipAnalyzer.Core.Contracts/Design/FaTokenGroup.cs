namespace FellowshipAnalyzer.Core.Contracts.Design;

/// <summary>
/// A named run of tokens, in the order the design system documents them. A tool that lists tokens
/// walks these groups rather than repeating the membership, so its structure cannot drift from the
/// theme's.
/// </summary>
/// <param name="Name">The group's display name, for example <c>Surfaces</c>.</param>
/// <param name="Source">
/// The design record that declares the group's values, for example <c>FaPalette</c>. An exporter
/// uses it to write each value back under the record it came from.
/// </param>
/// <param name="Tokens">The group's tokens, in declaration order.</param>
public sealed record FaTokenGroup(string Name, string Source, IReadOnlyList<FaToken> Tokens);
