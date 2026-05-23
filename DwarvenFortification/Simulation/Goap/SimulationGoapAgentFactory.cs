using System;
using System.Collections.Generic;
using System.Linq;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;

namespace DwarvenFortification.GOAP
{
	public static class SimulationGoapAgentFactory
	{
		public static GoapAgent CreateAgent(
			SimulationDefinitionRegistry definitions,
			string name,
			IEnumerable<string> currentFacts,
			IReadOnlyDictionary<string, GoapValue> numericState = null)
		{
			var agent = new GoapAgent(name)
			{
				States = BuildStateDictionary(definitions, currentFacts, numericState),
				Goals = [.. definitions.GetGoalDefinitions().Cast<GoapGoal>()],
				Actions = [.. definitions.GetActionDefinitions().Cast<GoapAction>()],
			};

			RegisterNumericBounds(agent, definitions);
			return agent;
		}

		static GoapWorldState BuildStateDictionary(
			SimulationDefinitionRegistry definitions,
			IEnumerable<string> currentFacts,
			IReadOnlyDictionary<string, GoapValue> numericState)
		{
			var states = new Dictionary<string, GoapValue>(StringComparer.OrdinalIgnoreCase);
			foreach (var key in EnumerateKnownStateKeys(definitions))
			{
				states[key] = false;
			}

			foreach (var fact in (currentFacts ?? []).Where(fact => !string.IsNullOrWhiteSpace(fact)))
			{
				states[GoapFactState.Normalize(fact)] = true;
			}

			if (numericState is not null)
			{
				foreach (var (key, value) in numericState)
				{
					if (!string.IsNullOrWhiteSpace(key))
					{
						states[key] = value;
					}
				}
			}

			return states;
		}

		static void RegisterNumericBounds(GoapAgent agent, SimulationDefinitionRegistry definitions)
		{
			foreach (var fact in definitions.GetFactDefinitions())
			{
				if (!string.Equals(fact.Kind, "numeric", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!fact.Min.HasValue && !fact.Max.HasValue)
				{
					continue;
				}

				GoapValue? min = fact.Min.HasValue ? AsGoapValue(fact.Min.Value) : null;
				GoapValue? max = fact.Max.HasValue ? AsGoapValue(fact.Max.Value) : null;
				agent.StateBounds[fact.Id] = new GoapStateBounds(min, max);
			}
		}

		static GoapValue AsGoapValue(double v)
		{
			// Prefer int when the bound is an integer; planner arithmetic preserves that type.
			if (v >= int.MinValue && v <= int.MaxValue && Math.Floor(v) == v)
			{
				return (int)v;
			}

			return v;
		}

		static IEnumerable<string> EnumerateKnownStateKeys(SimulationDefinitionRegistry definitions)
		{
			foreach (var action in definitions.GetActionDefinitions())
			{
				var actionFacts = action.RequiredFacts
					.Append(action.SuccessFact)
					.Append(action.InterruptedFact)
					.Append(action.FailedFact)
					.Where(fact => !string.IsNullOrWhiteSpace(fact));
				foreach (var key in GoapFactState.EnumerateStateKeys(actionFacts))
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
}
