using Arch.Core;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;
using Microsoft.Xna.Framework;
using System.Text.Json;

namespace DwarvenFortification.Tests;

[TestFixture]
public sealed class MermaidDiagramBuilderTests
{
	[Test]
	public void BuildTreemapDiagram()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var usedActionIds = plan.Steps.Select(step => step.Definition.Id).ToArray();

		var diagram = GoapPlanDiagram.BuildTreemapDiagram(plan);

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
	public void BuildGanttDiagram()
	{
		var plan = CreatePlannedSecureFoodPlan();
		var usedActionIds = plan.Steps.Select(step => step.Definition.Id).ToArray();

		var diagram = GoapPlanDiagram.BuildGanttDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(usedActionIds, Is.EqualTo(ExpectedSecureFoodPlanActionIds));
			Assert.That(usedActionIds, Has.None.Matches<string>(id => UnrelatedSecureFoodWorldActionIds.Contains(id, StringComparer.OrdinalIgnoreCase)));
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("dateFormat X"));
			Assert.That(diagram, Does.Contain("section Actions"));
			Assert.That(diagram, Does.Contain("1. quarry-salt :task1, 0, 1s"));
			Assert.That(diagram, Does.Contain("2. gather-herbs :task2, after task1, 2s"));
			Assert.That(diagram, Does.Contain("3. grind-seasoning :task3, after task2, 3s"));
			Assert.That(diagram, Does.Contain("4. collect-fiber :task4, after task3, 7s"));
			Assert.That(diagram, Does.Contain("5. weave-basket :task5, after task4, 8s"));
			Assert.That(diagram, Does.Contain("6. forage-berries :task6, after task5, 9s"));
			Assert.That(diagram, Does.Contain("7. cook-feast :task7, after task6, 10s"));
			Assert.That(diagram, Does.Not.Contain("forge-pickaxe-head"));
			Assert.That(diagram, Does.Not.Contain("fell-tree"));
			Assert.That(diagram, Does.Not.Contain("spin-yarn"));
		});
	}

	[Test]
	public void BuildFullStateDiagram()
	{
		var (planner, agent, queryService) = CreatePlannerWithQueryService(
			[
				new GoalDefinition { Id = "secure-food", Name = "Secure Food", Priority = 10, Effects = ["food.available"] },
			],
			[
				CreateActionDefinition("quarry-salt", ["seasoning.salt"], durationTicks: 1),
				CreateActionDefinition("gather-herbs", ["seasoning.herbs"], durationTicks: 2),
				CreateActionDefinition("grind-seasoning", ["seasoning.ready"], requirements: ["seasoning.salt", "seasoning.herbs"], durationTicks: 3),
				CreateActionDefinition("collect-fiber", ["fiber.collected"], durationTicks: 7),
				CreateActionDefinition("weave-basket", ["basket.ready"], requirements: ["fiber.collected"], durationTicks: 8),
				CreateActionDefinition("forage-berries", ["berries.found"], requirements: ["basket.ready"], durationTicks: 9),
				CreateActionDefinition("cook-feast", ["food.available"], requirements: ["berries.found", "seasoning.ready"], durationTicks: 10),
			],
			[],
			_ => ["quarry-salt", "gather-herbs", "grind-seasoning", "collect-fiber", "weave-basket", "forage-berries", "cook-feast"]);

		var snapshot = planner.Inspect(agent);
		var diagram = GoapPlanDiagram.BuildFullStateDiagram(snapshot);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagram, Does.Contain("direction LR"));
			Assert.That(diagram, Does.Contain("[*] --> s0"));
			Assert.That(diagram, Does.Contain("state \"Initial State\" as s0"));
			// All 7 actions should appear as transitions
			Assert.That(diagram, Does.Contain(": quarry-salt"));
			Assert.That(diagram, Does.Contain(": gather-herbs"));
			Assert.That(diagram, Does.Contain(": grind-seasoning"));
			Assert.That(diagram, Does.Contain(": collect-fiber"));
			Assert.That(diagram, Does.Contain(": weave-basket"));
			Assert.That(diagram, Does.Contain(": forage-berries"));
			Assert.That(diagram, Does.Contain(": cook-feast"));
			// grind-seasoning requires both salt and herbs to be present first
			// so the state it originates from should show both as added
			Assert.That(diagram, Does.Contain("+seasoning.herbs, +seasoning.salt"));
			// The final state reached by cook-feast adds food.available
			Assert.That(diagram, Does.Contain("+food.available"));
		});
	}

	[Test]
	public void BuildStateDiagram()
	{		var plan = CreatePlannedSecureFoodPlan();

		var diagram = GoapPlanDiagram.BuildStateDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagram, Does.Contain("direction LR"));
			Assert.That(diagram, Does.Contain("[*] --> s0"));
			Assert.That(diagram, Does.Contain("state \"Initial State\" as s0"));
			Assert.That(diagram, Does.Contain("s0 --> s1 : quarry-salt (cost 1)"));
			Assert.That(diagram, Does.Contain("state \"+seasoning.salt\" as s1"));
			Assert.That(diagram, Does.Contain("s1 --> s2 : gather-herbs (cost 2)"));
			Assert.That(diagram, Does.Contain("state \"+seasoning.herbs\" as s2"));
			Assert.That(diagram, Does.Contain("s5 --> s6 : forage-berries (cost 9)"));
			Assert.That(diagram, Does.Contain("s6 --> s7 : cook-feast (cost 10)"));
			Assert.That(diagram, Does.Contain("state \"Goal- Secure Food\" as s7"));
			Assert.That(diagram, Does.Contain("s7 --> [*]"));
		});
	}

	[Test]
	public void BuildBothDiagrams()
	{
		var plan = CreatePlannedSecureFoodPlan();

		var diagrams = GoapPlanDiagram.BuildDiagrams(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagrams.Treemap, Does.StartWith("treemap-beta"));
			Assert.That(diagrams.Gantt, Does.StartWith("gantt"));
			Assert.That(diagrams.StateDiagram, Does.StartWith("stateDiagram-v2"));
			Assert.That(diagrams.Gantt, Does.Contain("7. cook-feast :task7, after task6, 10s"));
			Assert.That(diagrams.Treemap, Does.Not.Contain("mine-iron"));
			Assert.That(diagrams.Gantt, Does.Not.Contain("weave-cloak"));
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

	static GoapPlan CreatePlannedSecureFoodPlan()
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
			CreateActionDefinition("quarry-salt", ["seasoning.salt"], durationTicks: 1, baseCost: 0),
			CreateActionDefinition("gather-herbs", ["seasoning.herbs"], durationTicks: 2, baseCost: 0),
			CreateActionDefinition("grind-seasoning", ["seasoning.ready"], requirements: ["seasoning.salt", "seasoning.herbs"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("collect-fiber", ["fiber.collected"], durationTicks: 7, baseCost: 0),
			CreateActionDefinition("weave-basket", ["basket.ready"], requirements: ["fiber.collected"], durationTicks: 8, baseCost: 0),
			CreateActionDefinition("forage-berries", ["berries.found"], requirements: ["basket.ready"], durationTicks: 9, baseCost: 0),
			CreateActionDefinition("cook-feast", ["food.available"], requirements: ["berries.found", "seasoning.ready"], durationTicks: 10, baseCost: 0),
			CreateActionDefinition("fell-tree", ["tree.logs"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("split-logs", ["firewood.ready"], requirements: ["tree.logs"], durationTicks: 2, baseCost: 0),
			CreateActionDefinition("shape-tool-handle", ["tool.handle"], requirements: ["tree.logs"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("mine-iron", ["ore.iron"], durationTicks: 5, baseCost: 0),
			CreateActionDefinition("smelt-iron-ingot", ["ingot.iron"], requirements: ["ore.iron", "firewood.ready"], durationTicks: 6, baseCost: 0),
			CreateActionDefinition("forge-pickaxe-head", ["pickaxe.head"], requirements: ["ingot.iron"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("assemble-pickaxe", ["pickaxe.ready"], requirements: ["pickaxe.head", "tool.handle"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("dig-deep-mine", ["mine.deep-access"], requirements: ["pickaxe.ready"], durationTicks: 8, baseCost: 0),
			CreateActionDefinition("shear-wool", ["wool.raw"], durationTicks: 3, baseCost: 0),
			CreateActionDefinition("spin-yarn", ["yarn.spun"], requirements: ["wool.raw"], durationTicks: 4, baseCost: 0),
			CreateActionDefinition("weave-cloak", ["clothing.warm"], requirements: ["yarn.spun"], durationTicks: 6, baseCost: 0),
		};

		var (planner, agent) = CreatePlanner(
			goals,
			actions,
			[],
			_ =>
			[
				.. ExpectedSecureFoodPlanActionIds,
				.. UnrelatedSecureFoodWorldActionIds,
			]);
		return planner.BuildCandidatePlans(agent).Single();
	}

	static (GoapPlanner Planner, Entity Agent) CreatePlanner(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentState,
		Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
	{
		var (planner, agent, _) = CreatePlannerWithQueryService(goals, actions, currentState, availableActionIdsFactory);
		return (planner, agent);
	}

	static (GoapPlanner Planner, Entity Agent, StubGoapWorldQueryService QueryService) CreatePlannerWithQueryService(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentState,
		Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
	{
		var definitions = TestSimulationDefinitions.CreateRegistry(actions, goals);
		var world = World.Create();
		var agent = world.Create();
		var queryService = new StubGoapWorldQueryService(currentState, availableActionIdsFactory);
		return (new GoapPlanner(definitions, queryService), agent, queryService);
	}

	static ActionDefinition CreateActionDefinition(string id, string[] effects, string[]? requirements = null, int durationTicks = 0, int baseCost = 1)
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

	sealed class StubGoapWorldQueryService : IGoapWorldQueryService
	{
		readonly HashSet<string> currentState;
		readonly Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory;

		public StubGoapWorldQueryService(IEnumerable<string> currentState, Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
		{
			this.currentState = new HashSet<string>(currentState, StringComparer.OrdinalIgnoreCase);
			this.availableActionIdsFactory = availableActionIdsFactory;
		}

		public HashSet<string> BuildCurrentState(Entity agent)
			=> new(currentState, StringComparer.OrdinalIgnoreCase);

		public GoapCandidateQuery InspectCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState)
			=> new([.. BuildCandidates(agent, actions, currentState)], Array.Empty<GoapActionDiagnostic>());

		public IEnumerable<GoapActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<GoapAction> actions, HashSet<string> currentState)
		{
			var availableActionIds = availableActionIdsFactory(new HashSet<string>(currentState, StringComparer.OrdinalIgnoreCase));
			foreach (var actionId in availableActionIds)
			{
				var action = actions.First(definition => string.Equals(definition.Id, actionId, StringComparison.OrdinalIgnoreCase));
				yield return new GoapActionCandidate(
					action,
					Point.Zero,
					Point.Zero,
					null,
					action.BaseCost + action.DurationTicks,
					action.Requirements,
					action.Effects);
			}
		}
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