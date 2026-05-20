using Arch.Core;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;
using Microsoft.Xna.Framework;
using System.Text.Json;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class GoapPlannerTests
{
	[Test]
	public void Plan_SelectsLowestCostPlanForGoal()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				Effects = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", requirements: [], effects: ["berries.found"], baseCost: 1),
			CreateActionDefinition("harvest-berries", requirements: ["berries.found"], effects: ["food.available"], baseCost: 1),
			CreateActionDefinition("buy-rations", requirements: [], effects: ["food.available"], baseCost: 5),
		};

		var (planner, agent) = CreatePlanner(goals, actions, []);

		var plan = planner.CreatePlan(agent);

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Goal.Id, Is.EqualTo("secure-food"));
			Assert.That(plan.Cost, Is.EqualTo(2));
			Assert.That(plan.Steps.Select(step => step.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
		});
	}

	[Test]
	public void Inspect_ReportsMissingFactsForIneligibleGoal()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "rest",
				Name = "Rest",
				Priority = 5,
				Effects = ["restored"],
				Requirements = ["bed.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("sleep", requirements: [], effects: ["restored"]),
		};

		var (planner, agent) = CreatePlanner(goals, actions, []);

		var snapshot = planner.Inspect(agent);

		Assert.That(snapshot.Goals, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(snapshot.Goals[0].IsEligible, Is.False);
			Assert.That(snapshot.Goals[0].MissingRequiredStates, Is.EqualTo(new[] { "bed.available" }));
			Assert.That(snapshot.Goals[0].CandidatePlan, Is.Null);
		});
	}

	[Test]
	public void Plan_RespectsNegatedRequiredFacts()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "hide",
				Name = "Hide",
				Priority = 20,
				Effects = ["self.hidden"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("hide", requirements: ["!enemy.visible"], effects: ["self.hidden"]),
		};

		var (planner, safeAgent) = CreatePlanner(goals, actions, []);
		var safePlan = planner.CreatePlan(safeAgent);

		var (blockedPlanner, threatenedAgent) = CreatePlanner(goals, actions, ["enemy.visible"]);
		var blockedPlan = blockedPlanner.CreatePlan(threatenedAgent);

		Assert.Multiple(() =>
		{
			Assert.That(safePlan, Is.Not.Null);
			Assert.That(blockedPlan, Is.Null);
		});
	}

	[Test]
	public void Plan_BuildsRequirementTreeAndFlattensLeafActions()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				Effects = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", requirements: [], effects: ["berries.found"], durationTicks: 4),
			CreateActionDefinition("harvest-berries", requirements: ["berries.found"], effects: ["food.available"], durationTicks: 6),
		};

		var (planner, agent) = CreatePlanner(
			goals,
			actions,
			[]);

		var plan = planner.CreatePlan(agent);

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Root.Kind, Is.EqualTo(GoapPlanNodeKind.Goal));
			Assert.That(plan.Root.Children, Has.Count.EqualTo(1));
			Assert.That(plan.Root.Children[0].Kind, Is.EqualTo(GoapPlanNodeKind.Requirement));
			Assert.That(plan.Root.Children[0].Children[0].Kind, Is.EqualTo(GoapPlanNodeKind.Requirement));
			Assert.That(plan!.Root.Children[0].Children[0].Children[0].Action?.Id, Is.EqualTo("forage"));
			Assert.That(plan.Root.Children[0].Children[1].Action?.Id, Is.EqualTo("harvest-berries"));
			Assert.That(plan.Steps.Select(step => step.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
		});
	}

	[Test]
	public void Plan_FindsCorrectSequenceWithRequirementFiltering()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				Effects = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", requirements: [], effects: ["berries.found"]),
			CreateActionDefinition("harvest-berries", requirements: ["berries.found"], effects: ["food.available"]),
		};

		var (planner, agent) = CreatePlanner(goals, actions, []);

		var plan = planner.CreatePlan(agent);

		Assert.That(plan, Is.Not.Null);
		Assert.That(plan!.Steps.Select(step => step.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
	}

	[Test]
	public void Plan_BuildsExpectedMermaidTreemapDiagram()
	{
		var plan = CreatePlannedSecureFoodMermaidPlan();
		var diagram = GoapPlanDiagram.BuildTreemapDiagram(plan);
		var usedActionIds = plan.Actions.Select(step => step.Id).ToArray();

		Assert.Multiple(() =>
		{
			Assert.That(usedActionIds, Is.EqualTo(ExpectedSecureFoodPlanActionIds));
			Assert.That(usedActionIds, Has.None.Matches<string>(id => UnrelatedSecureFoodWorldActionIds.Contains(id, StringComparer.OrdinalIgnoreCase)));
			Assert.That(diagram, Does.StartWith("treemap-beta"));
			Assert.That(diagram, Does.Contain("\"Goal: Secure Food (cost 40)\""));
			Assert.That(diagram, Does.Contain("    \"Requirement: Require state 'food.available' (cost 40)\""));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require state 'seasoning.ready' (cost 6)\""));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require state 'seasoning.salt' (cost 1)\""));
			Assert.That(diagram, Does.Contain("                \"Action: quarry-salt (cost 1)\": 1"));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require state 'seasoning.herbs' (cost 2)\""));
			Assert.That(diagram, Does.Contain("                \"Action: gather-herbs (cost 2)\": 2"));
			Assert.That(diagram, Does.Contain("            \"Action: grind-seasoning (cost 3)\": 3"));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require state 'berries.found' (cost 24)\""));
			Assert.That(diagram, Does.Contain("            \"Requirement: Require state 'basket.ready' (cost 15)\""));
			Assert.That(diagram, Does.Contain("                \"Requirement: Require state 'fiber.collected' (cost 7)\""));
			Assert.That(diagram, Does.Contain("                    \"Action: collect-fiber (cost 7)\": 7"));
			Assert.That(diagram, Does.Contain("                \"Action: weave-basket (cost 8)\": 8"));
			Assert.That(diagram, Does.Contain("            \"Action: forage-berries (cost 9)\": 9"));
			Assert.That(diagram, Does.Contain("        \"Action: cook-feast (cost 10)\": 10"));
			Assert.That(diagram, Does.Not.Contain("mine-iron"));
			Assert.That(diagram, Does.Not.Contain("assemble-pickaxe"));
			Assert.That(diagram, Does.Not.Contain("weave-cloak"));
		});
	}

	[Test]
	public void Plan_BuildsExpectedMermaidGanttDiagram()
	{
		var plan = CreatePlannedSecureFoodMermaidPlan();
		var diagram = GoapPlanDiagram.BuildGanttDiagram(plan);
		var usedActionIds = plan.Actions.Select(step => step.Id).ToArray();

		Assert.Multiple(() =>
		{
			Assert.That(usedActionIds, Is.EqualTo(ExpectedSecureFoodPlanActionIds));
			Assert.That(usedActionIds, Has.None.Matches<string>(id => UnrelatedSecureFoodWorldActionIds.Contains(id, StringComparer.OrdinalIgnoreCase)));
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("    title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("    dateFormat X"));
			Assert.That(diagram, Does.Contain("    axisFormat %s"));
			Assert.That(diagram, Does.Contain("    section Actions"));
			Assert.That(diagram, Does.Contain("        1. quarry-salt :task1, 0, 1s"));
			Assert.That(diagram, Does.Contain("        2. gather-herbs :task2, after task1, 2s"));
			Assert.That(diagram, Does.Contain("        3. grind-seasoning :task3, after task2, 3s"));
			Assert.That(diagram, Does.Contain("        4. collect-fiber :task4, after task3, 7s"));
			Assert.That(diagram, Does.Contain("        5. weave-basket :task5, after task4, 8s"));
			Assert.That(diagram, Does.Contain("        6. forage-berries :task6, after task5, 9s"));
			Assert.That(diagram, Does.Contain("        7. cook-feast :task7, after task6, 10s"));
			Assert.That(diagram, Does.Not.Contain("forge-pickaxe-head"));
			Assert.That(diagram, Does.Not.Contain("fell-tree"));
			Assert.That(diagram, Does.Not.Contain("spin-yarn"));
		});
	}

	static readonly string[] ExpectedSecureFoodPlanActionIds =
	[
		"quarry-salt",
		"gather-herbs",
		"grind-seasoning",
		"collect-fiber",
		"weave-basket",
		"forage-berries",
		"cook-feast",
	];

	static readonly string[] UnrelatedSecureFoodWorldActionIds =
	[
		"fell-tree",
		"split-logs",
		"shape-tool-handle",
		"mine-iron",
		"smelt-iron-ingot",
		"forge-pickaxe-head",
		"assemble-pickaxe",
		"dig-deep-mine",
		"shear-wool",
		"spin-yarn",
		"weave-cloak",
	];

	static GoapPlan CreatePlannedSecureFoodMermaidPlan()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				Effects = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("quarry-salt", requirements: [], effects: ["seasoning.salt"], durationTicks: 1, baseCost: 0),
			CreateActionDefinition("gather-herbs", requirements: [], effects: ["seasoning.herbs"], durationTicks: 2, baseCost: 0),
			CreateActionDefinition("grind-seasoning", requirements: ["seasoning.salt", "seasoning.herbs"], effects: ["seasoning.ready"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("collect-fiber", requirements: [], effects: ["fiber.collected"], durationTicks: 7, baseCost: 0),
			CreateActionDefinition("weave-basket", requirements: ["fiber.collected"], effects: ["basket.ready"], durationTicks: 8, baseCost: 0),
			CreateActionDefinition("forage-berries", requirements: ["basket.ready"], effects: ["berries.found"], durationTicks: 9, baseCost: 0),
			CreateActionDefinition("cook-feast", requirements: ["berries.found", "seasoning.ready"], effects: ["food.available"], durationTicks: 10, baseCost: 0),
			CreateActionDefinition("fell-tree", requirements: [], effects: ["tree.logs"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("split-logs", requirements: ["tree.logs"], effects: ["firewood.ready"], durationTicks: 2, baseCost: 0),
			CreateActionDefinition("shape-tool-handle", requirements: ["tree.logs"], effects: ["tool.handle"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("mine-iron", requirements: [], effects: ["ore.iron"], durationTicks: 5, baseCost: 0),
			CreateActionDefinition("smelt-iron-ingot", requirements: ["ore.iron", "firewood.ready"], effects: ["ingot.iron"], durationTicks: 6, baseCost: 0),
			CreateActionDefinition("forge-pickaxe-head", requirements: ["ingot.iron"], effects: ["pickaxe.head"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("assemble-pickaxe", requirements: ["pickaxe.head", "tool.handle"], effects: ["pickaxe.ready"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("dig-deep-mine", requirements: ["pickaxe.ready"], effects: ["mine.deep-access"], durationTicks: 8, baseCost: 0),
			CreateActionDefinition("shear-wool", requirements: [], effects: ["wool.raw"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("spin-yarn", requirements: ["wool.raw"], effects: ["yarn.spun"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("weave-cloak", requirements: ["yarn.spun"], effects: ["clothing.warm"], durationTicks: 6, baseCost: 0),
		};

		var (planner, agent) = CreatePlanner(
			goals,
			actions,
			[]);

		return planner.CreatePlan(agent)!;
	}

	static (GoapPlanner Planner, IGoapAgent Agent) CreatePlanner(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentState)
	{
		var definitions = TestSimulationDefinitions.CreateRegistry(actions, goals);
		var agent = new TestGoapAgent(currentState);
		return (new GoapPlanner(definitions), agent);
	}

	static ActionDefinition CreateActionDefinition(string id, string[] requirements, string[] effects, int durationTicks = 0, int baseCost = 1)
		=> new()
		{
			Id = id,
			Name = id,
			TargetKind = "self",
			DestinationMode = "current",
			BaseCost = baseCost,
			DurationTicks = durationTicks,
			Requirements = requirements,
			Effects = effects,
		};

	sealed class TestGoapAgent(IEnumerable<string> state) : IGoapAgent
	{
		readonly HashSet<string> initialState = new(state, StringComparer.OrdinalIgnoreCase);
		public HashSet<string> GetCurrentState() => new(initialState, StringComparer.OrdinalIgnoreCase);
	}

	static class TestSimulationDefinitions
	{
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = false,
		};

		public static SimulationDefinitionRegistry CreateRegistry(ActionDefinition[] actions, GoalDefinition[] goals)
		{
			var contentRoot = Path.Combine(Path.GetTempPath(), $"df-goap-tests-{Guid.NewGuid():N}");
			Directory.CreateDirectory(contentRoot);

			try
			{
				WriteDocument(Path.Combine(contentRoot, "items.json"), new ItemDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "actions.json"), new ActionDefinitionDocument { Actions = actions });
				WriteDocument(Path.Combine(contentRoot, "objects.json"), new WorldObjectDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "resources.json"), new ResourceNodeDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "agents.json"), new AgentDefinitionDocument());
				WriteDocument(Path.Combine(contentRoot, "goals.json"), new GoalDefinitionDocument { Goals = goals });

				return SimulationDefinitionRegistry.LoadFromContentDirectory(contentRoot);
			}
			finally
			{
				Directory.Delete(contentRoot, recursive: true);
			}
		}

		static void WriteDocument<T>(string path, T document)
		{
			var json = JsonSerializer.Serialize(document, JsonOptions);
			File.WriteAllText(path, json);
		}
	}
}
