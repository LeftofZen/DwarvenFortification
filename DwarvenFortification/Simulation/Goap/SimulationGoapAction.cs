using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace DwarvenFortification.GOAP
{
	public class SimulationGoapAction : GoapAction
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
			IEnumerable<string> childActionIds,
			IEnumerable<string> parameters = null,
			IReadOnlyDictionary<string, string> parameterBindings = null)
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

			ChildActionIds = [.. (childActionIds ?? []).Where(cid => !string.IsNullOrWhiteSpace(cid))];
			Conditions = [.. requirementExpressions.Select(e => e.ToCondition())];
			Cost = _ => BaseCost;

			Parameters = [.. (parameters ?? []).Where(p => !string.IsNullOrWhiteSpace(p))];
			ParameterBindings = parameterBindings is null
				? []
				: new Dictionary<string, string>(parameterBindings, StringComparer.Ordinal);

			// Parametric templates are not directly runnable: their Name/Conditions/Effects still
			// contain {placeholder} tokens. Validate would reject them. Concrete instances produced
			// by Instantiate(bindings) get fully substituted and then validated.
			if (!IsParametric)
			{
				Validate();
			}
		}

		/// <summary>
		/// Clone constructor used by <see cref="Instantiate"/>: copies the template's data and applies
		/// <paramref name="bindings"/> to every string-shaped surface the planner / runtime reads
		/// (Name, Conditions, SuccessEffect, InterruptedEffect, FailedEffect, RequiredFacts,
		/// SuccessFact, InterruptedFact, FailedFact). The non-string typed metadata (BaseCost,
		/// DurationTicks, TargetKind, DestinationMode, ChildActionIds, expression documents)
		/// is shared with the template.
		/// </summary>
		protected SimulationGoapAction(SimulationGoapAction template, IReadOnlyDictionary<string, string> bindings)
			: base(
				GoapParameterSubstitution.SubstituteString(template.Name, bindings),
				GoapParameterSubstitution.SubstituteEffect(template.SuccessEffect, bindings))
		{
			Id = template.Id;
			BaseCost = template.BaseCost;
			DurationTicks = template.DurationTicks;
			TargetKind = template.TargetKind;
			DestinationMode = template.DestinationMode;

			RequirementExpressions = template.RequirementExpressions;
			RequiredFacts = [.. template.RequiredFacts.Select(f => GoapParameterSubstitution.SubstituteString(f, bindings))];

			SuccessExpression = template.SuccessExpression;
			InterruptedExpression = template.InterruptedExpression;
			FailedExpression = template.FailedExpression;
			SuccessFact = GoapParameterSubstitution.SubstituteString(template.SuccessFact, bindings);
			InterruptedFact = GoapParameterSubstitution.SubstituteString(template.InterruptedFact, bindings);
			FailedFact = GoapParameterSubstitution.SubstituteString(template.FailedFact, bindings);

			InterruptedEffect = template.InterruptedEffect is null
				? null
				: GoapParameterSubstitution.SubstituteEffect(template.InterruptedEffect, bindings);
			FailedEffect = template.FailedEffect is null
				? null
				: GoapParameterSubstitution.SubstituteEffect(template.FailedEffect, bindings);

			ChildActionIds = template.ChildActionIds;
			Conditions = [.. template.Conditions.Select(c => GoapParameterSubstitution.SubstituteCondition(c, bindings))];
			Cost = _ => BaseCost;

			// Concrete instance: no further parameters; record the resolved bindings so runtime code can read them back.
			Parameters = [];
			ParameterBindings = [];
			Bindings = bindings;

			Validate();
		}

		public override GoapAction Instantiate(IReadOnlyDictionary<string, string> bindings)
			=> bindings is null || bindings.Count == 0 ? this : new SimulationGoapAction(this, bindings);

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
