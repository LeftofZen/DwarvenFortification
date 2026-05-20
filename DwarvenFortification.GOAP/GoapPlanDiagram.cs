using System.Text;

namespace DwarvenFortification.GOAP;

public readonly record struct GoapPlanDiagrams(
	string Treemap,
	string Gantt,
	string StateDiagram);

public static class GoapPlanDiagram
{
	const int MaxSearchDepth = 8;
	const int MaxStateNodes = 64;

	public static GoapPlanDiagrams BuildDiagrams(GoapPlan plan)
		=> new(BuildTreemapDiagram(plan), BuildGanttDiagram(plan), BuildStateDiagram(plan));

	public static string BuildTreemapDiagram(GoapPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var builder = new StringBuilder();
		builder.AppendLine("treemap-beta");
		builder.Append('"').Append(EscapeTreemapLabel($"Goal: {plan.Goal.Name} (cost {CalculatePlanCost(plan):0.##})")).Append('"').AppendLine();

		if (plan.Actions.Count == 0)
		{
			builder.AppendLine("    \"Already satisfied\": 1");
			return builder.ToString().TrimEnd();
		}

		foreach (var action in plan.Actions)
		{
			var cost = Math.Max(1, action.Cost(plan.Agent));
			builder.Append("    \"")
				.Append(EscapeTreemapLabel($"Action: {action.Name} (cost {cost:0.##})"))
				.Append("\": ")
				.Append(cost.ToString("0.##"))
				.AppendLine();
		}

		return builder.ToString().TrimEnd();
	}

	public static string BuildGanttDiagram(GoapPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var builder = new StringBuilder();
		builder.AppendLine("gantt");
		builder.Append("    title ").AppendLine(SanitizeGanttText($"Plan: {plan.Goal.Name}"));
		builder.AppendLine("    dateFormat X");
		builder.AppendLine("    axisFormat %s");
		builder.AppendLine("    section Actions");

		if (plan.Actions.Count == 0)
		{
			builder.AppendLine("        No actions : milestone, empty, 0, 1s");
			return builder.ToString().TrimEnd();
		}

		string previousTaskId = null;
		for (var index = 0; index < plan.Actions.Count; ++index)
		{
			var action = plan.Actions[index];
			var taskId = $"task{index + 1}";
			var label = SanitizeGanttText($"{index + 1}. {action.Name}");
			var cost = Math.Max(1, action.Cost(plan.Agent));

			builder.Append("        ").Append(label).Append(" :").Append(taskId).Append(", ");
			builder.Append(previousTaskId == null ? "0" : $"after {previousTaskId}").Append(", ");
			builder.Append(cost.ToString("0.##")).AppendLine("s");
			previousTaskId = taskId;
		}

		return builder.ToString().TrimEnd();
	}

	public static string BuildFullStateDiagram(GoapAgent agent)
	{
		ArgumentNullException.ThrowIfNull(agent);
		return BuildFullStateDiagram(agent.States, agent.Actions);
	}

	public static string BuildFullStateDiagram(
		IReadOnlyDictionary<object, object?> initialStates,
		IReadOnlyList<GoapAction> actions)
	{
		ArgumentNullException.ThrowIfNull(initialStates);
		ArgumentNullException.ThrowIfNull(actions);

		var keyToId = new Dictionary<string, string>(StringComparer.Ordinal);
		var idToStates = new Dictionary<string, Dictionary<object, object?>>(StringComparer.Ordinal);

		string GetOrAddState(Dictionary<object, object?> states)
		{
			var key = StateKey(states);
			if (!keyToId.TryGetValue(key, out var id))
			{
				id = $"s{keyToId.Count}";
				keyToId[key] = id;
				idToStates[id] = states;
			}

			return id;
		}

		var initial = new Dictionary<object, object?>(initialStates);
		var transitions = new List<(string From, string Label, string To)>();
		var visited = new HashSet<string>(StringComparer.Ordinal);
		var queue = new Queue<(Dictionary<object, object?> States, int Depth)>();

		queue.Enqueue((initial, 0));
		GetOrAddState(initial);

		while (queue.Count > 0)
		{
			var (current, depth) = queue.Dequeue();
			var currentKey = StateKey(current);

			if (!visited.Add(currentKey) || depth >= MaxSearchDepth || keyToId.Count >= MaxStateNodes)
			{
				continue;
			}

			var currentId = keyToId[currentKey];
			foreach (var action in actions)
			{
				if (!action.Requirements.All(requirement => requirement.IsMet(current)))
				{
					continue;
				}

				var next = action.PredictStates(current);
				var nextKey = StateKey(next);
				if (string.Equals(nextKey, currentKey, StringComparison.Ordinal))
				{
					continue;
				}

				var nextId = GetOrAddState(next);
				transitions.Add((currentId, SanitizeStateTransitionText(action.Name), nextId));

				if (!visited.Contains(nextKey))
				{
					queue.Enqueue((next, depth + 1));
				}
			}
		}

		var builder = new StringBuilder();
		builder.AppendLine("stateDiagram-v2");
		builder.AppendLine("    direction LR");
		var initialId = keyToId[StateKey(initial)];
		builder.AppendLine($"    [*] --> {initialId}");

		foreach (var (id, states) in idToStates)
		{
			var label = string.Equals(id, initialId, StringComparison.Ordinal)
				? "Initial State"
				: BuildDeltaLabel(initial, states);
			builder.AppendLine($"    state \"{SanitizeStateLabelText(label)}\" as {id}");
		}

		foreach (var (from, label, to) in transitions)
		{
			builder.AppendLine($"    {from} --> {to} : {label}");
		}

		return builder.ToString().TrimEnd();
	}

	public static string BuildStateDiagram(GoapPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		var builder = new StringBuilder();
		builder.AppendLine("stateDiagram-v2");
		builder.AppendLine("    direction LR");

		if (plan.Actions.Count == 0)
		{
			builder.AppendLine("    [*] --> goalSatisfied");
			builder.AppendLine("    state \"Goal already satisfied\" as goalSatisfied");
			builder.AppendLine("    goalSatisfied --> [*]");
			return builder.ToString().TrimEnd();
		}

		var states = new Dictionary<object, object?>(plan.Agent.States);
		builder.AppendLine("    [*] --> s0");
		builder.AppendLine("    state \"Initial State\" as s0");

		for (var index = 0; index < plan.Actions.Count; ++index)
		{
			var action = plan.Actions[index];
			var previous = new Dictionary<object, object?>(states);
			action.UpdateStates(states);

			var fromId = $"s{index}";
			var toId = $"s{index + 1}";
			var cost = Math.Max(1, action.Cost(plan.Agent));
			builder.AppendLine($"    {fromId} --> {toId} : {SanitizeStateTransitionText($"{action.Name} (cost {cost:0.##})")}");

			var nodeLabel = index == plan.Actions.Count - 1
				? $"Goal- {plan.Goal.Name}"
				: BuildDeltaLabel(previous, states);
			builder.AppendLine($"    state \"{SanitizeStateLabelText(nodeLabel)}\" as {toId}");
		}

		builder.AppendLine($"    s{plan.Actions.Count} --> [*]");
		return builder.ToString().TrimEnd();
	}

	static double CalculatePlanCost(GoapPlan plan)
		=> plan.Actions.Sum(action => action.Cost(plan.Agent));

	static string StateKey(IReadOnlyDictionary<object, object?> states)
		=> string.Join("\0", states
			.OrderBy(pair => FormatStateKey(pair.Key), StringComparer.Ordinal)
			.Select(pair => $"{FormatStateKey(pair.Key)}={FormatValue(pair.Value)}"));

	static string BuildDeltaLabel(IReadOnlyDictionary<object, object?> previous, IReadOnlyDictionary<object, object?> current)
	{
		var keys = previous.Keys.Concat(current.Keys)
			.Select(FormatStateKey)
			.Distinct(StringComparer.Ordinal)
			.OrderBy(key => key, StringComparer.Ordinal)
			.ToArray();

		var previousByKey = previous.ToDictionary(pair => FormatStateKey(pair.Key), pair => pair.Value, StringComparer.Ordinal);
		var currentByKey = current.ToDictionary(pair => FormatStateKey(pair.Key), pair => pair.Value, StringComparer.Ordinal);
		var parts = new List<string>();

		foreach (var key in keys)
		{
			previousByKey.TryGetValue(key, out var previousValue);
			currentByKey.TryGetValue(key, out var currentValue);
			if (Equals(previousValue, currentValue))
			{
				continue;
			}

			parts.Add($"{key}: {FormatValue(previousValue)} -> {FormatValue(currentValue)}");
		}

		return parts.Count > 0 ? string.Join(", ", parts) : "unchanged";
	}

	static string FormatStateKey(object key)
		=> key?.ToString() ?? "null";

	static string FormatValue(object? value)
		=> value switch
		{
			null => "null",
			bool boolean => boolean ? "true" : "false",
			_ => value.ToString() ?? string.Empty,
		};

	static string EscapeTreemapLabel(string value)
		=> value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

	static string SanitizeStateTransitionText(string value)
		=> string.Join(" ", value
			.Replace(':', '-')
			.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));

	static string SanitizeStateLabelText(string value)
		=> value.Replace('"', '\'')
			.Replace('\r', ' ')
			.Replace('\n', ' ');

	static string SanitizeGanttText(string value)
		=> string.Join(" ", value
			.Replace(':', '-')
			.Replace(',', ' ')
			.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
}
