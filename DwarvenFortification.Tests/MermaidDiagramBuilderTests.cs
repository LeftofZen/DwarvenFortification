using System.Collections.Concurrent;
using DwarvenFortification.GOAP;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class MermaidDiagramBuilderTests
{
	[Test]
	public void BuildTreemapDiagram_EmitsComplexPlanAndOmitsUnrelatedBranches()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var diagram = GoapPlanDiagram.BuildTreemapDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("treemap-beta"));
			Assert.That(diagram, Does.Contain("\"Goal: Secure Food (cost 40)\""));
			Assert.That(diagram, Does.Contain("\"Action: quarry-salt (cost 1)\": 1"));
			Assert.That(diagram, Does.Contain("\"Action: gather-herbs (cost 2)\": 2"));
			Assert.That(diagram, Does.Contain("\"Action: grind-seasoning (cost 3)\": 3"));
			Assert.That(diagram, Does.Contain("\"Action: collect-fiber (cost 7)\": 7"));
			Assert.That(diagram, Does.Contain("\"Action: weave-basket (cost 8)\": 8"));
			Assert.That(diagram, Does.Contain("\"Action: forage-berries (cost 9)\": 9"));
			Assert.That(diagram, Does.Contain("\"Action: cook-feast (cost 10)\": 10"));
			Assert.That(diagram, Does.Not.Contain("mine-iron"));
			Assert.That(diagram, Does.Not.Contain("assemble-pickaxe"));
			Assert.That(diagram, Does.Not.Contain("weave-cloak"));
		});
	}

	[Test]
	public void BuildGanttDiagram_EmitsComplexPlanInExecutionOrder()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var diagram = GoapPlanDiagram.BuildGanttDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("dateFormat X"));
			Assert.That(diagram, Does.Contain("axisFormat %s"));
			Assert.That(diagram, Does.Contain("section Actions"));
			Assert.That(diagram, Does.Contain($"1. {plan.Actions[0].Name} :task1, 0, {plan.Actions[0].Cost(plan.Agent):0.##}s"));
			for (var index = 1; index < plan.Actions.Count; ++index)
			{
				Assert.That(diagram, Does.Contain($"{index + 1}. {plan.Actions[index].Name} :task{index + 1}, after task{index}, {plan.Actions[index].Cost(plan.Agent):0.##}s"));
			}
			Assert.That(diagram, Does.Not.Contain("forge-pickaxe-head"));
			Assert.That(diagram, Does.Not.Contain("fell-tree"));
			Assert.That(diagram, Does.Not.Contain("spin-yarn"));
		});
	}

	[Test]
	public void BuildStateDiagram_EmitsComplexPlanStateTransitions()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var diagram = GoapPlanDiagram.BuildStateDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagram, Does.Contain("direction LR"));
			Assert.That(diagram, Does.Contain("[*] --> s0"));
			Assert.That(diagram, Does.Contain("state \"Initial State\" as s0"));
			for (var index = 0; index < plan.Actions.Count; ++index)
			{
				var action = plan.Actions[index];
				Assert.That(diagram, Does.Contain($"s{index} --> s{index + 1} : {action.Name} (cost {action.Cost(plan.Agent):0.##})"));
			}

			Assert.That(diagram, Does.Contain("seasoning.salt: false -> true"));
			Assert.That(diagram, Does.Contain("seasoning.herbs: false -> true"));
			Assert.That(diagram, Does.Contain("seasoning.ready: false -> true"));
			Assert.That(diagram, Does.Contain("basket.ready: false -> true"));
			Assert.That(diagram, Does.Contain("berries.found: false -> true"));
			Assert.That(diagram, Does.Contain("state \"Goal- Secure Food\" as s7"));
			Assert.That(diagram, Does.Contain("s7 --> [*]"));
		});
	}

	[Test]
	public void BuildFullStateDiagram_ExploresComplexReachableStateGraph()
	{
		var agent = CreateSecureFoodAgent(includeUnrelatedWorldBranches: false);
		var diagram = GoapPlanDiagram.BuildFullStateDiagram(agent);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagram, Does.Contain("direction LR"));
			Assert.That(diagram, Does.Contain("[*] --> s0"));
			Assert.That(diagram, Does.Contain("state \"Initial State\" as s0"));
			Assert.That(diagram, Does.Contain(": quarry-salt"));
			Assert.That(diagram, Does.Contain(": gather-herbs"));
			Assert.That(diagram, Does.Contain(": grind-seasoning"));
			Assert.That(diagram, Does.Contain(": collect-fiber"));
			Assert.That(diagram, Does.Contain(": weave-basket"));
			Assert.That(diagram, Does.Contain(": forage-berries"));
			Assert.That(diagram, Does.Contain(": cook-feast"));
			Assert.That(diagram, Does.Contain("seasoning.salt: false -> true"));
			Assert.That(diagram, Does.Contain("seasoning.herbs: false -> true"));
			Assert.That(diagram, Does.Contain("basket.ready: false -> true"));
			Assert.That(diagram, Does.Contain("food.available: false -> true"));
		});
	}

	[Test]
	public void BuildFullStateDiagram_IncludesUnrelatedReachableBranchesWhenAskedForWholeAgentGraph()
	{
		var agent = CreateSecureFoodAgent(includeUnrelatedWorldBranches: true);
		var diagram = GoapPlanDiagram.BuildFullStateDiagram(agent);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.Contain(": fell-tree"));
			Assert.That(diagram, Does.Contain(": mine-iron"));
			Assert.That(diagram, Does.Contain("tree.logs: false -> true"));
			Assert.That(diagram, Does.Contain("ore.iron: false -> true"));
		});
	}

	[Test]
	public void BuildBothDiagrams_ReturnsAllDiagramKindsForComplexPlan()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var diagrams = GoapPlanDiagram.BuildDiagrams(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagrams.Treemap, Does.StartWith("treemap-beta"));
			Assert.That(diagrams.Gantt, Does.StartWith("gantt"));
			Assert.That(diagrams.StateDiagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagrams.Treemap, Does.Contain("cook-feast"));
			Assert.That(diagrams.Gantt, Does.Contain("cook-feast :task7, after task6, 10s"));
			Assert.That(diagrams.StateDiagram, Does.Contain("state \"Goal- Secure Food\" as s7"));
		});
	}

	static GoapPlan CreatePlannedSecureFoodPlan()
		=> CreateSecureFoodAgent(includeUnrelatedWorldBranches: true).FindPlan(new GoapPlanSettings
		{
			MaxIterations = 1_000,
			MaxActions = 7,
		})!;

	static GoapAgent CreateSecureFoodAgent(bool includeUnrelatedWorldBranches)
	{
		var stateIds = new List<string>();

		stateIds.AddRange([
			"seasoning.salt",
			"seasoning.herbs",
			"seasoning.ready",
			"fiber.collected",
			"basket.ready",
			"berries.found",
			"food.available",
		]);

		if (includeUnrelatedWorldBranches)
		{
			stateIds.AddRange([
				"tree.logs",
				"firewood.ready",
				"tool.handle",
				"ore.iron",
				"ingot.iron",
				"pickaxe.head",
				"pickaxe.ready",
				"mine.deep-access",
				"wool.raw",
				"yarn.spun",
				"clothing.warm",
			]);
		}

		return new GoapAgent("complex-diagram-test")
		{
			States = new ConcurrentDictionary<object, object?>(stateIds.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(id => (object)id, _ => (object?)false)),
			Goals =
			[
				new GoapGoal("Secure Food", [Condition("food.available")])
				{
					Id = "secure-food",
				},
			],
			Actions = includeUnrelatedWorldBranches
				? [.. CreateSecureFoodActions(), .. CreateUnrelatedWorldActions()]
				: [.. CreateSecureFoodActions()],
		};
	}

	static IEnumerable<GoapAction> CreateSecureFoodActions()
	{
		yield return Action("quarry-salt", [], ["seasoning.salt"], 1);
		yield return Action("gather-herbs", [], ["seasoning.herbs"], 2);
		yield return Action("grind-seasoning", ["seasoning.salt", "seasoning.herbs"], ["seasoning.ready"], 3);
		yield return Action("collect-fiber", [], ["fiber.collected"], 7);
		yield return Action("weave-basket", ["fiber.collected"], ["basket.ready"], 8);
		yield return Action("forage-berries", ["basket.ready"], ["berries.found"], 9);
		yield return Action("cook-feast", ["berries.found", "seasoning.ready"], ["food.available"], 10);
	}

	static IEnumerable<GoapAction> CreateUnrelatedWorldActions()
	{
		yield return Action("fell-tree", [], ["tree.logs"], 4);
		yield return Action("split-logs", ["tree.logs"], ["firewood.ready"], 2);
		yield return Action("shape-tool-handle", ["tree.logs"], ["tool.handle"], 3);
		yield return Action("mine-iron", [], ["ore.iron"], 5);
		yield return Action("smelt-iron-ingot", ["ore.iron", "firewood.ready"], ["ingot.iron"], 6);
		yield return Action("forge-pickaxe-head", ["ingot.iron"], ["pickaxe.head"], 4);
		yield return Action("assemble-pickaxe", ["pickaxe.head", "tool.handle"], ["pickaxe.ready"], 3);
		yield return Action("dig-deep-mine", ["pickaxe.ready"], ["mine.deep-access"], 8);
		yield return Action("shear-wool", [], ["wool.raw"], 3);
		yield return Action("spin-yarn", ["wool.raw"], ["yarn.spun"], 4);
		yield return Action("weave-cloak", ["yarn.spun"], ["clothing.warm"], 6);
	}

	static GoapAction Action(string name, string[] requirements, string[] effects, double cost)
		=> new(name, [.. effects.Select(effect => new GoapEffect(effect, GoapOperation.SetTo, true))])
		{
			Requirements = [.. requirements.Select(Condition)],
			Cost = _ => cost,
		};

	static GoapCondition Condition(string state)
		=> new(state, GoapComparison.EqualTo, true);
}
