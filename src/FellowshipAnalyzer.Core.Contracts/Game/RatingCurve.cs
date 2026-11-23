namespace FellowshipAnalyzer.Core.Game;

/// <summary>
/// Rating-to-percentage curve of one secondary stat, as the game-data export states it.
/// </summary>
/// <param name="BaseStatMultiplier">Percentage points each rating point gives before diminishing returns.</param>
/// <param name="Brackets">Brackets in ascending order. Each has the converted percentage it ends at and the penalty applied within it.</param>
/// <param name="FinalPenaltyPercentage">Penalty applied above the last bracket.</param>
public sealed record RatingCurve(double BaseStatMultiplier, (double MaxBracketValue, double PenaltyPercentage)[] Brackets, double FinalPenaltyPercentage);
