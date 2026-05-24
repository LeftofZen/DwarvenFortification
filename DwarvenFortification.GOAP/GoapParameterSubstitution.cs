namespace DwarvenFortification.GOAP;

/// <summary>
/// Expansion + substitution helpers that turn parametric action templates into concrete planner-ready
/// instances. Parameters appear as <c>{paramName}</c> placeholders in action names, condition / effect
/// state ids, and string-typed operands; substitution replaces each placeholder with the bound value.
/// </summary>
public static class GoapParameterSubstitution
{
	/// <summary>
	/// Expand every parametric template in <paramref name="actions"/> into one concrete instance per
	/// legal binding tuple, leaving non-parametric actions untouched. Bindings are sourced from
	/// <paramref name="resolver"/> applied to each parameter's binding pattern; templates with no
	/// matching bindings are omitted (they have no instantiations to plan with).
	/// </summary>
	public static IReadOnlyList<GoapAction> ExpandParametricActions(
		IEnumerable<GoapAction> actions,
		Func<string, IEnumerable<string>> resolver,
		IReadOnlyCollection<string> relevantStateIds = null)
	{
		var actionList = (actions ?? []).Where(a => a is not null).ToList();

		// "Relevant" state ids = the ones the planner could plausibly chain to (any condition StateId
		// on any non-parametric action, plus any caller-supplied goal-state ids). A substituted
		// parametric instance whose SuccessEffect.StateId is NOT relevant is a dead-end branch that
		// only inflates A*'s open list; we drop those instances entirely to keep MaxExpansions usable.
		// This filter is only applied when the caller opts in by supplying `relevantStateIds`;
		// otherwise every successful expansion is kept (matches the historical behavior expected by
		// unit tests that exercise the substitution logic in isolation).
		var applyRelevanceFilter = relevantStateIds is not null;
		var relevant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (applyRelevanceFilter)
		{
			foreach (var a in actionList.Where(a => !a.IsParametric))
			{
				foreach (var c in a.Conditions ?? [])
				{
					if (!string.IsNullOrEmpty(c.StateId))
					{
						relevant.Add(c.StateId);
					}
				}
			}

			foreach (var id in relevantStateIds)
			{
				if (!string.IsNullOrEmpty(id))
				{
					relevant.Add(id);
				}
			}
		}

		var result = new List<GoapAction>();
		foreach (var action in actionList)
		{
			if (!action.IsParametric)
			{
				result.Add(action);
				continue;
			}

			// No resolver => no way to enumerate legal values; skip this template.
			if (resolver is null)
			{
				continue;
			}

			var candidates = new List<List<KeyValuePair<string, string>>>(action.Parameters.Count);
			var fullyResolved = true;
			foreach (var parameterName in action.Parameters)
			{
				if (!action.ParameterBindings.TryGetValue(parameterName, out var pattern) || string.IsNullOrWhiteSpace(pattern))
				{
					fullyResolved = false;
					break;
				}

				var pairs = (resolver(pattern) ?? [])
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.Select(value => new KeyValuePair<string, string>(parameterName, value))
					.ToList();
				if (pairs.Count == 0)
				{
					fullyResolved = false;
					break;
				}

				candidates.Add(pairs);
			}

			if (!fullyResolved)
			{
				continue;
			}

			foreach (var bindings in CartesianProduct(candidates))
			{
				var instance = action.Instantiate(bindings);
				// Drop substitutions whose effect nobody consumes \u2014 they bloat the search frontier
				// without ever contributing to a plan.
				var effectStateId = instance.SuccessEffect?.StateId;
				if (applyRelevanceFilter && !string.IsNullOrEmpty(effectStateId) && !relevant.Contains(effectStateId))
				{
					continue;
				}

				result.Add(instance);
			}
		}

		return result;
	}

	/// <summary>Replace every <c>{paramName}</c> placeholder in <paramref name="template"/> with its bound value.</summary>
	public static string SubstituteString(string template, IReadOnlyDictionary<string, string> bindings)
	{
		if (string.IsNullOrEmpty(template) || bindings is null || bindings.Count == 0)
		{
			return template;
		}

		var result = template;
		foreach (var pair in bindings)
		{
			result = result.Replace("{" + pair.Key + "}", pair.Value, StringComparison.Ordinal);
		}

		return result;
	}

	public static GoapCondition SubstituteCondition(GoapCondition condition, IReadOnlyDictionary<string, string> bindings)
		=> new(
			SubstituteString(condition.StateId, bindings),
			condition.Comparison,
			SubstituteValue(condition.Operand, bindings));

	public static GoapEffect SubstituteEffect(GoapEffect effect, IReadOnlyDictionary<string, string> bindings)
		=> new(
			SubstituteString(effect.StateId, bindings),
			effect.Operation,
			SubstituteValue(effect.Operand, bindings));

	static GoapValue SubstituteValue(GoapValue value, IReadOnlyDictionary<string, string> bindings)
		=> value.Value is string s ? new GoapValue(SubstituteString(s, bindings)) : value;

	static IEnumerable<IReadOnlyDictionary<string, string>> CartesianProduct(List<List<KeyValuePair<string, string>>> lists)
	{
		IEnumerable<Dictionary<string, string>> current = new[] { new Dictionary<string, string>(StringComparer.Ordinal) };
		foreach (var list in lists)
		{
			var snapshot = list;
			current = current.SelectMany(prev => snapshot.Select(pair =>
			{
				var next = new Dictionary<string, string>(prev, StringComparer.Ordinal) { [pair.Key] = pair.Value };
				return next;
			}));
		}

		foreach (var dict in current)
		{
			yield return dict;
		}
	}
}
