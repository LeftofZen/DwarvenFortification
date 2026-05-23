using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class SimulationGoapGoal : GoapGoal
	{
		[SetsRequiredMembers]
		public SimulationGoapGoal(string id, string name, int priority, IEnumerable<GoapExpressionDocument> desiredEffects, IEnumerable<GoapExpressionDocument> requirements)
			: base(string.IsNullOrWhiteSpace(name) ? id : name, BuildObjectives(desiredEffects))
		{
			Id = id;
			PriorityValue = priority;
			DesiredExpressions = [.. (desiredEffects ?? []).Where(e => e is not null)];
			RequirementExpressions = [.. (requirements ?? []).Where(e => e is not null)];
			DesiredFacts = [.. DesiredExpressions.Where(e => e.IsBooleanFact).Select(e => e.Fact)];
			RequiredFacts = [.. RequirementExpressions.Where(e => e.IsBooleanFact).Select(e => e.Fact)];
			Priority = _ => PriorityValue;
			Validate();
		}

		public int PriorityValue { get; }

		/// <summary>All goal-objective expressions (boolean facts + structured numeric conditions).</summary>
		public GoapExpressionDocument[] DesiredExpressions { get; }

		/// <summary>All goal-precondition expressions.</summary>
		public GoapExpressionDocument[] RequirementExpressions { get; }

		/// <summary>Subset of <see cref="DesiredExpressions"/> that are plain boolean fact strings.</summary>
		public string[] DesiredFacts { get; }

		/// <summary>Subset of <see cref="RequirementExpressions"/> that are plain boolean fact strings.</summary>
		public string[] RequiredFacts { get; }

		/// <summary>
		/// Builds the goal's condition list directly from the structured expressions, so numeric
		/// operators (<c>&lt;=</c>, <c>&gt;</c>, …) compose with the planner's <see cref="GoapStateBounds"/>.
		/// </summary>
		static List<GoapCondition> BuildObjectives(IEnumerable<GoapExpressionDocument> expressions)
			=> [.. (expressions ?? [])
				.Where(e => e is not null)
				.Select(e => e.ToCondition())];
	}
}
