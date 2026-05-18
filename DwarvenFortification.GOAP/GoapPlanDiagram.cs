namespace DwarvenFortification.GOAP
{
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

			var builder = new System.Text.StringBuilder();
			builder.AppendLine("treemap-beta");
			AppendTreemapNode(builder, plan.Root, 0);
			return builder.ToString().TrimEnd();
		}

		public static string BuildGanttDiagram(GoapPlan plan)
		{
			ArgumentNullException.ThrowIfNull(plan);

			var builder = new System.Text.StringBuilder();
			builder.AppendLine("gantt");
			builder.Append("    title ").AppendLine(SanitizeGanttText($"Plan: {plan.Goal.Name}"));
			builder.AppendLine("    dateFormat X");
			builder.AppendLine("    axisFormat %s");
			builder.AppendLine("    section Actions");

			if (plan.Steps.Count == 0)
			{
				builder.AppendLine("        No actions : milestone, empty, 0, 1s");
				return builder.ToString().TrimEnd();
			}

			string previousTaskId = null;
			for (var index = 0; index < plan.Steps.Count; ++index)
			{
				var step = plan.Steps[index];
				var taskId = $"task{index + 1}";
				var durationSeconds = Math.Max(step.Definition.DurationTicks, 1);
				var label = SanitizeGanttText($"{index + 1}. {step.Definition.Name}");

				builder.Append("        ").Append(label).Append(" :").Append(taskId).Append(", ");
				if (previousTaskId == null)
				{
					builder.Append("0, ");
				}
				else
				{
					builder.Append("after ").Append(previousTaskId).Append(", ");
				}

				builder.Append(durationSeconds).AppendLine("s");
				previousTaskId = taskId;
			}

			return builder.ToString().TrimEnd();
		}

		static void AppendTreemapNode(System.Text.StringBuilder builder, GoapPlanNode node, int depth)
		{
			var indent = new string(' ', depth * 4);
			var label = EscapeTreemapLabel(FormatTreemapLabel(node));

			if (node.Children.Count == 0)
			{
				builder.Append(indent).Append('"').Append(label).Append('"').Append(": ").Append(Math.Max(node.Cost, 1)).AppendLine();
				return;
			}

			builder.Append(indent).Append('"').Append(label).Append('"').AppendLine();
			foreach (var child in node.Children)
			{
				AppendTreemapNode(builder, child, depth + 1);
			}
		}

		static string FormatTreemapLabel(GoapPlanNode node)
			=> $"{node.Kind}: {node.Label} (cost {node.Cost})";

		static string EscapeTreemapLabel(string value)
			=> value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

		public static string BuildFullStateDiagram(GoapSnapshot snapshot)
		{
			ArgumentNullException.ThrowIfNull(snapshot);
			return BuildFullStateDiagram(snapshot.CurrentState, snapshot.ActionManifestations);
		}

		public static string BuildFullStateDiagram(
			IReadOnlyList<string> initialState,
			IReadOnlyList<GoapActionCandidate> candidates)
		{
			ArgumentNullException.ThrowIfNull(initialState);
			ArgumentNullException.ThrowIfNull(candidates);

			// Deduplicate by action ID — multiple world-target manifestations of the same action
			// have the same logical RequiredStates/AddStates/RemoveStates structure.
			var uniqueCandidates = candidates
				.GroupBy(c => c.Definition.Id, StringComparer.OrdinalIgnoreCase)
				.Select(g => g.First())
				.ToList();

			var initial = new HashSet<string>(initialState, StringComparer.OrdinalIgnoreCase);

			// State registry: canonical key → (id, fact-set)
			var keyToId = new Dictionary<string, string>(StringComparer.Ordinal);
			var idToFacts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

			string GetOrAddState(HashSet<string> state)
			{
				var key = StateKey(state);
				if (!keyToId.TryGetValue(key, out var id))
				{
					id = $"s{keyToId.Count}";
					keyToId[key] = id;
					idToFacts[id] = state;
				}
				return id;
			}

			var transitions = new List<(string From, string Label, string To)>();
			var visited = new HashSet<string>(StringComparer.Ordinal);
			var queue = new Queue<(HashSet<string> State, int Depth)>();

			queue.Enqueue((initial, 0));
			GetOrAddState(initial);

			while (queue.Count > 0)
			{
				var (current, depth) = queue.Dequeue();
				var currentKey = StateKey(current);

				if (!visited.Add(currentKey))
				{
					continue;
				}

				if (depth >= MaxSearchDepth || keyToId.Count >= MaxStateNodes)
				{
					continue;
				}

				var currentId = keyToId[currentKey];

				foreach (var candidate in uniqueCandidates)
				{
					if (!candidate.Requirements.All(current.Contains))
					{
						continue;
					}

					var next = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
					foreach (var s in candidate.Effects) next.Add(s);

					var nextKey = StateKey(next);
					if (string.Equals(nextKey, currentKey, StringComparison.Ordinal))
					{
						continue;
					}

					var nextId = GetOrAddState(next);
					transitions.Add((currentId, SanitizeStateTransitionText(candidate.Definition.Name), nextId));

					if (!visited.Contains(nextKey))
					{
						queue.Enqueue((next, depth + 1));
					}
				}
			}

			var sb = new System.Text.StringBuilder();
			sb.AppendLine("stateDiagram-v2");
			sb.AppendLine("    direction LR");

			var initialId = keyToId[StateKey(initial)];
			sb.AppendLine($"    [*] --> {initialId}");

			foreach (var (id, facts) in idToFacts)
			{
				var added = facts.Except(initial, StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToArray();
				var removed = initial.Except(facts, StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToArray();

				var label = string.Equals(id, initialId, StringComparison.Ordinal)
					? "Initial State"
					: BuildDeltaLabel(added, removed);

				sb.AppendLine($"    state \"{SanitizeStateLabelText(label)}\" as {id}");
			}

			foreach (var (from, label, to) in transitions)
			{
				sb.AppendLine($"    {from} --> {to} : {label}");
			}

			return sb.ToString().TrimEnd();
		}

		static string StateKey(HashSet<string> state)
			=> string.Join("\0", state.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

		static string BuildDeltaLabel(string[] added, string[] removed)
		{
			var parts = added.Select(s => $"+{s}").Concat(removed.Select(s => $"-{s}")).ToArray();
			return parts.Length > 0 ? string.Join(", ", parts) : "unchanged";
		}

		public static string BuildStateDiagram(GoapPlan plan)
		{
			ArgumentNullException.ThrowIfNull(plan);

			var builder = new System.Text.StringBuilder();
			builder.AppendLine("stateDiagram-v2");
			builder.AppendLine("    direction LR");

			var steps = plan.Steps;
			if (steps.Count == 0)
			{
				builder.AppendLine("    [*] --> goalSatisfied");
				builder.AppendLine($"    state \"Goal already satisfied\" as goalSatisfied");
				builder.AppendLine("    goalSatisfied --> [*]");
				return builder.ToString().TrimEnd();
			}

			builder.AppendLine("    [*] --> s0");
			builder.AppendLine("    state \"Initial State\" as s0");

			for (var i = 0; i < steps.Count; i++)
			{
				var step = steps[i];
				var fromId = $"s{i}";
				var toId = $"s{i + 1}";

				var transitionLabel = SanitizeStateTransitionText($"{step.Definition.Name} (cost {step.Cost})");
				builder.AppendLine($"    {fromId} --> {toId} : {transitionLabel}");

				string nodeLabel;
				if (i == steps.Count - 1)
				{
					nodeLabel = $"Goal- {plan.Goal.Name}";
				}
				else
				{
					var deltaLines = step.Effects
						.Where(s => !string.IsNullOrWhiteSpace(s))
						.Select(s => $"+{s}")
						.ToArray();
					nodeLabel = deltaLines.Length > 0 ? string.Join(", ", deltaLines) : $"State {i + 1}";
				}

				builder.AppendLine($"    state \"{SanitizeStateLabelText(nodeLabel)}\" as {toId}");
			}

			builder.AppendLine($"    s{steps.Count} --> [*]");
			return builder.ToString().TrimEnd();
		}

		// State transition labels: Mermaid uses ':' as a separator so it must be replaced.
		static string SanitizeStateTransitionText(string value)
			=> string.Join(" ", value
				.Replace(':', '-')
				.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));

		// State node labels appear inside double-quotes; double-quotes themselves must be avoided.
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
}
