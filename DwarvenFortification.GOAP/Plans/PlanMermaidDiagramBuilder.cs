namespace DwarvenFortification.GOAP.Plans
{
	public readonly record struct PlanMermaidDiagrams(
		string Treemap,
		string Gantt);

	public static class PlanMermaidDiagramBuilder
	{
		public static PlanMermaidDiagrams BuildDiagrams(Plan plan)
			=> new(BuildTreemapDiagram(plan), BuildGanttDiagram(plan));

		public static string BuildTreemapDiagram(Plan plan)
		{
			ArgumentNullException.ThrowIfNull(plan);

			var builder = new System.Text.StringBuilder();
			builder.AppendLine("treemap-beta");
			AppendTreemapNode(builder, plan.Root, 0);
			return builder.ToString().TrimEnd();
		}

		public static string BuildGanttDiagram(Plan plan)
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

		static void AppendTreemapNode(System.Text.StringBuilder builder, PlanNode node, int depth)
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

		static string FormatTreemapLabel(PlanNode node)
			=> $"{node.Kind}: {node.Label} (cost {node.Cost})";

		static string EscapeTreemapLabel(string value)
			=> value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

		static string SanitizeGanttText(string value)
			=> string.Join(" ", value
				.Replace(':', '-')
				.Replace(',', ' ')
				.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
	}
}