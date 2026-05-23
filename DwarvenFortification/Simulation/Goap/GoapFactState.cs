using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	/// <summary>
	/// Helpers for the simulation's boolean-fact convention: a leading '!' means "must be false".
	/// </summary>
	public static class GoapFactState
	{
		public static string Normalize(string fact)
			=> fact != null && fact.StartsWith('!') ? fact[1..] : fact ?? string.Empty;

		public static GoapCondition ToCondition(string fact)
			=> new(Normalize(fact), fact.StartsWith('!') ? GoapComparison.NotEqualTo : GoapComparison.EqualTo, true);

		public static bool IsSatisfied(GoapWorldState states, string fact)
		{
			var isTrue = states.GetValueOrDefault(Normalize(fact)) is { Value: bool b } && b;
			return fact.StartsWith('!') ? !isTrue : isTrue;
		}

		public static IEnumerable<string> EnumerateStateKeys(IEnumerable<string> facts)
			=> (facts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)).Select(Normalize);
	}
}
