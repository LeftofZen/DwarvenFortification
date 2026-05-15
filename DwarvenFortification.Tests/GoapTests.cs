using Arch.Core;
using DwarvenFortification.ECS;
using DwarvenFortification.ECS.Authoring;
using DwarvenFortification.GOAP;
using DwarvenFortification.GOAP.Actions;
using DwarvenFortification.GOAP.Plans;
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
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", ["berries.found"], baseCost: 1),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"], baseCost: 1),
			CreateActionDefinition("buy-rations", ["food.available"], baseCost: 5),
		};

		var (planner, agent, _) = CreatePlanner(goals, actions, [], _ => ["forage", "harvest-berries", "buy-rations"]);

		var plan = planner.BuildCandidatePlans(agent).SingleOrDefault();

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Goal.Id, Is.EqualTo("secure-food"));
			Assert.That(plan.Cost, Is.EqualTo(2));
			Assert.That(plan.Steps.Select(step => step.Definition.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
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
				DesiredFacts = ["restored"],
				RequiredFacts = ["bed.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("sleep", ["restored"]),
		};

		var (planner, agent, _) = CreatePlanner(goals, actions, [], _ => []);

		var snapshot = planner.Inspect(agent);

		Assert.That(snapshot.CandidatePlans, Is.Empty);
		Assert.That(snapshot.Goals, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(snapshot.Goals[0].IsEligible, Is.False);
			Assert.That(snapshot.Goals[0].MissingRequiredFacts, Is.EqualTo(new[] { "bed.available" }));
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
				DesiredFacts = ["self.hidden"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("hide", ["self.hidden"], requiredFacts: ["!enemy.visible"]),
		};

		var (planner, safeAgent, _) = CreatePlanner(goals, actions, [], _ => ["hide"]);
		var safePlan = planner.BuildCandidatePlans(safeAgent).SingleOrDefault();

		var (blockedPlanner, threatenedAgent, _) = CreatePlanner(goals, actions, ["enemy.visible"], _ => ["hide"]);
		var blockedPlan = blockedPlanner.BuildCandidatePlans(threatenedAgent).SingleOrDefault();

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
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", ["berries.found"], durationTicks: 4),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"], durationTicks: 6),
		};

		var (planner, agent, _) = CreatePlanner(
			goals,
			actions,
			[],
			_ => ["forage", "harvest-berries"]);

		var plan = planner.BuildCandidatePlans(agent).SingleOrDefault();

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Root.Kind, Is.EqualTo(PlanNodeKind.Goal));
			Assert.That(plan.Root.Children, Has.Count.EqualTo(1));
			Assert.That(plan.Root.Children[0].Kind, Is.EqualTo(PlanNodeKind.Requirement));
			Assert.That(plan.Root.Children[0].Children[0].Kind, Is.EqualTo(PlanNodeKind.Requirement));
			Assert.That(plan.Root.Children[0].Children[0].Children[0].Candidate?.Definition.Id, Is.EqualTo("forage"));
			Assert.That(plan.Root.Children[0].Children[1].Candidate?.Definition.Id, Is.EqualTo("harvest-berries"));
			Assert.That(plan.Steps.Select(step => step.Definition.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
		});
	}

	[Test]
	public void Plan_QueriesCandidatesAgainstSimulatedPlanningState()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", ["berries.found"]),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"]),
		};

		var (planner, agent, queryService) = CreatePlanner(
			goals,
			actions,
			[],
			currentFacts => currentFacts.Contains("berries.found")
				? ["harvest-berries"]
				: ["forage"]);

		var plan = planner.BuildCandidatePlans(agent).SingleOrDefault();

		Assert.That(plan, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(plan!.Steps.Select(step => step.Definition.Id), Is.EqualTo(new[] { "forage", "harvest-berries" }));
			Assert.That(queryService.ObservedStates, Has.Some.Matches<string[]>(state => state.Contains("berries.found", StringComparer.OrdinalIgnoreCase)));
		});
	}

	[Test]
	public void Plan_BuildsExpectedMermaidTreemapDiagram()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", ["berries.found"], durationTicks: 4),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"], durationTicks: 6),
		};

		var (planner, agent, _) = CreatePlanner(
			goals,
			actions,
			[],
			_ => ["forage", "harvest-berries"]);

		var plan = planner.BuildCandidatePlans(agent).Single();
		var diagram = PlanMermaidDiagramBuilder.BuildTreemapDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("treemap-beta"));
			Assert.That(diagram, Does.Contain("\"Goal: Secure Food (cost 12)\""));
			Assert.That(diagram, Does.Contain("    \"Requirement: Require fact 'food.available' (cost 12)\""));
			Assert.That(diagram, Does.Contain("        \"Requirement: Require fact 'berries.found' (cost 5)\""));
			Assert.That(diagram, Does.Contain("            \"Action: forage (cost 5)\": 5"));
			Assert.That(diagram, Does.Contain("        \"Action: harvest-berries (cost 7)\": 7"));
		});
	}

	[Test]
	public void Plan_BuildsExpectedMermaidGanttDiagram()
	{
		var goals = new[]
		{
			new GoalDefinition
			{
				Id = "secure-food",
				Name = "Secure Food",
				Priority = 10,
				DesiredFacts = ["food.available"],
			},
		};
		var actions = new[]
		{
			CreateActionDefinition("forage", ["berries.found"], durationTicks: 4),
			CreateActionDefinition("harvest-berries", ["food.available"], requiredFacts: ["berries.found"], durationTicks: 6),
		};

		var (planner, agent, _) = CreatePlanner(
			goals,
			actions,
			[],
			_ => ["forage", "harvest-berries"]);

		var plan = planner.BuildCandidatePlans(agent).Single();
		var diagram = PlanMermaidDiagramBuilder.BuildGanttDiagram(plan);

		Assert.Multiple(() =>
		{
			Assert.That(diagram, Does.StartWith("gantt"));
			Assert.That(diagram, Does.Contain("    title Plan- Secure Food"));
			Assert.That(diagram, Does.Contain("    dateFormat X"));
			Assert.That(diagram, Does.Contain("    axisFormat %s"));
			Assert.That(diagram, Does.Contain("    section Actions"));
			Assert.That(diagram, Does.Contain("        1. forage :task1, 0, 4s"));
			Assert.That(diagram, Does.Contain("        2. harvest-berries :task2, after task1, 6s"));
		});
	}

	static (Planner Planner, Entity Agent, StubGoapWorldQueryService QueryService) CreatePlanner(
		GoalDefinition[] goals,
		ActionDefinition[] actions,
		IEnumerable<string> currentFacts,
		Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
	{
		var definitions = TestSimulationDefinitions.CreateRegistry(actions, goals);
		var world = World.Create();
		var agent = world.Create();
		var queryService = new StubGoapWorldQueryService(currentFacts, availableActionIdsFactory);
		return (new Planner(definitions, queryService), agent, queryService);
	}

	static ActionDefinition CreateActionDefinition(string id, string[] addFacts, string[]? requiredFacts = null, int durationTicks = 0, int baseCost = 1)
		=> new()
		{
			Id = id,
			Name = id,
			TargetKind = "self",
			DestinationMode = "current",
			BaseCost = baseCost,
			DurationTicks = durationTicks,
			Requires = new ActionRequirementDefinition
			{
				RequiredFacts = requiredFacts ?? [],
			},
			Effects = new ActionEffectDefinition
			{
				AddFacts = addFacts,
			},
		};

	sealed class StubGoapWorldQueryService : IWorldQueryService
	{
		readonly HashSet<string> currentFacts;
		readonly Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory;

		public StubGoapWorldQueryService(IEnumerable<string> currentFacts, Func<HashSet<string>, IReadOnlyList<string>> availableActionIdsFactory)
		{
			this.currentFacts = new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase);
			this.availableActionIdsFactory = availableActionIdsFactory;
		}

		public List<string[]> ObservedStates { get; } = new();

		public HashSet<string> BuildCurrentFacts(Entity agent)
			=> new(currentFacts, StringComparer.OrdinalIgnoreCase);

		public CandidateQuerySnapshot InspectCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
		{
			var snapshotFacts = currentFacts.OrderBy(fact => fact, StringComparer.OrdinalIgnoreCase).ToArray();
			ObservedStates.Add(snapshotFacts);
			return new(BuildCandidates(agent, actions, currentFacts).ToArray(), Array.Empty<ActionDiagnostic>());
		}

		public IEnumerable<ActionCandidate> BuildCandidates(Entity agent, IReadOnlyList<ActionDefinitionSnapshot> actions, HashSet<string> currentFacts)
		{
			var availableActionIds = availableActionIdsFactory(new HashSet<string>(currentFacts, StringComparer.OrdinalIgnoreCase));
			foreach (var actionId in availableActionIds)
			{
				var action = actions.First(definition => string.Equals(definition.Id, actionId, StringComparison.OrdinalIgnoreCase));
				yield return new ActionCandidate(
					action,
					Point.Zero,
					Point.Zero,
					null,
					action.BaseCost + action.DurationTicks,
					action.RequiredFacts,
					action.AddFacts,
					action.RemoveFacts);
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
