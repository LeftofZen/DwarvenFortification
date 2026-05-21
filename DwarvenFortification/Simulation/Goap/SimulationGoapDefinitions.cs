#pragma warning disable CS8632

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DwarvenFortification.ECS;
using DwarvenFortification.UI;

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
			IEnumerable<string> requiredFacts,
			IEnumerable<string> effectFacts,
			IEnumerable<string> skills)
			: base(string.IsNullOrWhiteSpace(name) ? id : name, BuildEffects(effectFacts))
		{
			Id = id;
			BaseCost = Math.Max(1, baseCost);
			DurationTicks = Math.Max(1, durationTicks);
			TargetKind = targetKind ?? string.Empty;
			DestinationMode = destinationMode ?? string.Empty;
			RequiredFacts = [.. (requiredFacts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact))];
			EffectFacts = [.. (effectFacts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact))];
			Skills = [.. (skills ?? []).Where(skill => !string.IsNullOrWhiteSpace(skill))];
			Conditions = BuildRequirements(RequiredFacts);
			Cost = _ => BaseCost;
		}

		public string Id { get; }
		public int BaseCost { get; }
		public int DurationTicks { get; }
		public string TargetKind { get; }
		public string DestinationMode { get; }
		public string[] RequiredFacts { get; }
		public string[] EffectFacts { get; }
		public string[] Skills { get; }

		static List<GoapCondition> BuildRequirements(IEnumerable<string> facts)
			=> [.. (facts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)).Select(GoapFactState.ToCondition)];

		static List<GoapEffect> BuildEffects(IEnumerable<string> facts)
			=> [.. (facts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)).Select(fact => new GoapEffect(GoapFactState.Normalize(fact), GoapOperation.SetTo, true))];
	}

	public sealed class SimulationGoapGoal : GoapGoal
	{
		[SetsRequiredMembers]
		public SimulationGoapGoal(string id, string name, int priority, IEnumerable<string> desiredFacts, IEnumerable<string> requiredFacts)
			: base(string.IsNullOrWhiteSpace(name) ? id : name, BuildObjectives(desiredFacts))
		{
			Id = id;
			PriorityValue = priority;
			DesiredFacts = [.. (desiredFacts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact))];
			RequiredFacts = [.. (requiredFacts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact))];
			Priority = _ => PriorityValue;
			IsValidOverride = agent => RequiredFacts.All(fact => GoapFactState.IsSatisfied(agent.States, fact));
		}

		public int PriorityValue { get; }
		public string[] DesiredFacts { get; }
		public string[] RequiredFacts { get; }

		static List<GoapCondition> BuildObjectives(IEnumerable<string> facts)
			=> [.. (facts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)).Select(GoapFactState.ToCondition)];
	}

	public static class GoapFactState
	{
		public static string Normalize(string fact)
			=> fact != null && fact.StartsWith('!') ? fact[1..] : fact ?? string.Empty;

		public static GoapCondition ToCondition(string fact)
		{
			var isNegated = fact.StartsWith('!');
			return new GoapCondition(Normalize(fact), isNegated ? GoapComparison.NotEqualTo : GoapComparison.EqualTo, true);
		}

		public static bool IsSatisfied(IDictionary<object, object?> states, string fact)
		{
			var state = Normalize(fact);
			var value = states.GetValueOrDefault(state);
			var isTrue = value is bool boolValue && boolValue;
			return fact.StartsWith('!') ? !isTrue : isTrue;
		}

		public static IEnumerable<string> EnumerateStateKeys(IEnumerable<string> facts)
			=> (facts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)).Select(Normalize);
	}

	public static class SimulationGoapActionExtensions
	{
		public static SimulationGoapAction AsSimulationAction(this GoapAction action)
			=> action as SimulationGoapAction ?? throw new InvalidOperationException($"GOAP action '{action?.Name}' is not a DwarvenFortification simulation action.");

		public static string GetId(this GoapAction action)
			=> action.AsSimulationAction().Id;

		public static string GetTargetKind(this GoapAction action)
			=> action.AsSimulationAction().TargetKind;

		public static string GetDestinationMode(this GoapAction action)
			=> action.AsSimulationAction().DestinationMode;

		public static int GetDurationTicks(this GoapAction action)
			=> action.AsSimulationAction().DurationTicks;

		public static string[] GetSkills(this GoapAction action)
			=> action.AsSimulationAction().Skills;

		public static string[] GetRequiredFacts(this GoapAction action)
			=> action.AsSimulationAction().RequiredFacts;

		public static string[] GetEffectFacts(this GoapAction action)
			=> action.AsSimulationAction().EffectFacts;
	}

	public static class SimulationGoapAgentFactory
	{
		public static GoapAgent CreateAgent(SimulationDefinitionRegistry definitions, string name, IEnumerable<string> currentFacts)
			=> new(name)
			{
				States = BuildStateDictionary(definitions, currentFacts),
				Goals = [.. definitions.GetGoalDefinitions().Cast<GoapGoal>()],
				Actions = [.. definitions.GetActionDefinitions().Cast<GoapAction>()],
			};

		static ConcurrentDictionary<object, object?> BuildStateDictionary(SimulationDefinitionRegistry definitions, IEnumerable<string> currentFacts)
		{
			var states = new ConcurrentDictionary<object, object?>();
			foreach (var key in EnumerateKnownStateKeys(definitions))
			{
				states[key] = false;
			}

			foreach (var fact in currentFacts.Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				states[GoapFactState.Normalize(fact)] = true;
			}

			return states;
		}

		static IEnumerable<string> EnumerateKnownStateKeys(SimulationDefinitionRegistry definitions)
		{
			foreach (var action in definitions.GetActionDefinitions())
			{
				foreach (var key in GoapFactState.EnumerateStateKeys(action.RequiredFacts.Concat(action.EffectFacts)))
				{
					yield return key;
				}
			}

			foreach (var goal in definitions.GetGoalDefinitions())
			{
				foreach (var key in GoapFactState.EnumerateStateKeys(goal.RequiredFacts.Concat(goal.DesiredFacts)))
				{
					yield return key;
				}
			}
		}
	}

	public interface IGoapPlanExecutor
	{
		bool Enqueue(Arch.Core.Entity agent, GoapPlan plan, AgentActionMetadata metadata = null);
		bool Enqueue(Arch.Core.Entity agent, GoapAction step, GoapAgent goapAgent, AgentActionMetadata metadata = null);
	}
}