using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public sealed class SimulationGoapAction : GoapAction
	{
		[SetsRequiredMembers]
		public SimulationGoapAction(
			string id,
			string name,
			int baseCost,
			int durationTicks,
			string targetKind,
			string destinationMode,
			IEnumerable<GoapExpressionDocument> requirements,
			GoapExpressionDocument successEffect,
			GoapExpressionDocument interruptedEffect,
			GoapExpressionDocument failedEffect,
			IEnumerable<string> skills,
			IEnumerable<string> childActionIds)
			: base(string.IsNullOrWhiteSpace(name) ? id : name, BuildEffect(successEffect, nameof(successEffect)))
		{
			Id = id;
			BaseCost = Math.Max(1, baseCost);
			DurationTicks = Math.Max(1, durationTicks);
			TargetKind = targetKind ?? string.Empty;
			DestinationMode = destinationMode ?? string.Empty;

			var requirementExpressions = (requirements ?? []).Where(e => e is not null).ToArray();
			RequirementExpressions = requirementExpressions;
			RequiredFacts = [.. requirementExpressions.Where(e => e.IsBooleanFact).Select(e => e.Fact)];

			SuccessExpression = successEffect;
			InterruptedExpression = interruptedEffect;
			FailedExpression = failedEffect;
			SuccessFact = successEffect is { IsBooleanFact: true } ? successEffect.Fact : null;
			InterruptedFact = interruptedEffect is { IsBooleanFact: true } ? interruptedEffect.Fact : null;
			FailedFact = failedEffect is { IsBooleanFact: true } ? failedEffect.Fact : null;

			InterruptedEffect = interruptedEffect?.ToEffect();
			FailedEffect = failedEffect?.ToEffect();

			Skills = [.. (skills ?? []).Where(skill => !string.IsNullOrWhiteSpace(skill))];
			ChildActionIds = [.. (childActionIds ?? []).Where(cid => !string.IsNullOrWhiteSpace(cid))];
			Conditions = [.. requirementExpressions.Select(e => e.ToCondition())];
			Cost = _ => BaseCost;
			Validate();
		}

		public string Id { get; }
		public int BaseCost { get; }
		public int DurationTicks { get; }
		public string TargetKind { get; }
		public string DestinationMode { get; }

		/// <summary>All requirement expressions (boolean facts + structured numeric conditions).</summary>
		public GoapExpressionDocument[] RequirementExpressions { get; }

		/// <summary>Subset of <see cref="RequirementExpressions"/> that are plain boolean fact strings (with '!' preserved).</summary>
		public string[] RequiredFacts { get; }

		public GoapExpressionDocument SuccessExpression { get; }
		public GoapExpressionDocument InterruptedExpression { get; }
		public GoapExpressionDocument FailedExpression { get; }

		/// <summary>Boolean form of the success effect when applicable; null when the effect is numeric.</summary>
		public string SuccessFact { get; }
		public string InterruptedFact { get; }
		public string FailedFact { get; }

		public string[] Skills { get; }
		public string[] ChildActionIds { get; }

		/// <summary>Convenience: the planner-visible effect fact (empty for numeric effects).</summary>
		public string EffectFact => SuccessFact ?? string.Empty;

		static GoapEffect BuildEffect(GoapExpressionDocument expression, string paramName)
		{
			if (expression is null)
			{
				throw new ArgumentException("Success effect must not be null.", paramName);
			}

			return expression.ToEffect();
		}
	}
}
