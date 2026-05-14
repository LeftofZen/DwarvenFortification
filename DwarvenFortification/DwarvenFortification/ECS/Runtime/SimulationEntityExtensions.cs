using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace DwarvenFortification
{
	public static class SimulationEntityExtensions
	{
		public static bool IsAgent(this Entity entity)
			=> entity.Has<AgentTagComponent>();

		public static bool IsWorldObject(this Entity entity)
			=> entity.Has<WorldObjectTagComponent>();

		public static string GetName(this Entity entity)
		{
			ref var identity = ref entity.Get<DefinitionIdentityComponent>();
			return string.IsNullOrWhiteSpace(identity.Name) ? identity.Id : identity.Name;
		}

		public static Point GetPosition(this Entity entity)
			=> entity.Get<RuntimeTransformComponent>().Position;

		public static void SetPosition(this Entity entity, Point position)
		{
			ref var transform = ref entity.Get<RuntimeTransformComponent>();
			transform.Position = position;
		}

		public static float GetStrength(this Entity entity)
			=> entity.Get<AgentStatsComponent>().Strength;

		public static float GetSpeed(this Entity entity)
		{
			ref var stats = ref entity.Get<AgentStatsComponent>();
			ref var inventory = ref entity.Get<InventoryComponent>();
			var capacity = inventory.Capacity <= 0 ? 1 : inventory.Capacity;
			return stats.BaseSpeed * (1 - (0.25f * ((float)inventory.Items.Count / capacity)));
		}

		public static int GetWidth(this Entity entity)
			=> entity.Get<BodyComponent>().Width;

		public static int GetHeight(this Entity entity)
			=> entity.Get<BodyComponent>().Height;

		public static int GetTop(this Entity entity)
			=> entity.GetPosition().Y - (entity.GetHeight() / 2);

		public static int GetLeft(this Entity entity)
			=> entity.GetPosition().X - (entity.GetWidth() / 2);

		public static int GetInventoryCapacity(this Entity entity)
			=> entity.Get<InventoryComponent>().Capacity;

		public static List<Entity> GetInventory(this Entity entity)
			=> entity.Get<InventoryComponent>().Items;

		public static void AddInventoryItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			inventory.Items.Add(item);
		}

		public static bool RemoveInventoryItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			return inventory.Items.Remove(item);
		}

		public static bool HasQueuedTasks(this Entity entity)
			=> entity.Get<TaskQueueComponent>().Tasks.Count > 0;

		public static void EnqueueTask(this Entity entity, IAgentTask task, int count = 1)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			for (var i = 0; i < count; ++i)
			{
				queue.Tasks.Enqueue(task);
			}
		}

		public static bool TryPeekTask(this Entity entity, out IAgentTask task)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			if (queue.Tasks.Count > 0)
			{
				task = queue.Tasks.Peek();
				return true;
			}

			task = null;
			return false;
		}

		public static void ClearQueuedTasks(this Entity entity)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			queue.Tasks.Clear();
		}

		public static bool HasInvalidQueuedTask(this Entity entity, ISimulationWorld world)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			return queue.Tasks.Any(task => !task.IsStillValid(world));
		}

		public static void DequeueTask(this Entity entity)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			if (queue.Tasks.Count > 0)
			{
				queue.Tasks.Dequeue();
			}
		}

		public static Point GetCurrentPathGoal(this Entity entity)
		{
			ref var queue = ref entity.Get<TaskQueueComponent>();
			return queue.Tasks.OfType<MoveAlongPathTask>().LastOrDefault()?.Destination ?? entity.GetPosition();
		}

		public static GridCell GetCurrentCell(this Entity entity, ISimulationWorld world)
			=> world.CellAtXY(entity.GetPosition());

		public static Rectangle GetCellBounds(this Entity entity, ISimulationWorld world)
		{
			var position = entity.GetPosition();
			return world.CellBoundsAt(position.X, position.Y);
		}

		public static string GetItemDefinitionId(this Entity entity)
			=> entity.Get<ItemInstanceComponent>().DefinitionId;

		public static Point GetCellReference(this Entity entity)
			=> entity.Get<CellReferenceComponent>().Cell;

		public static void AddStoredItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			inventory.Items.Add(item);
		}

		public static bool RemoveStoredItem(this Entity entity, Entity item)
		{
			ref var inventory = ref entity.Get<InventoryComponent>();
			return inventory.Items.Remove(item);
		}

		public static bool IsStorageObject(this Entity entity)
			=> entity.Has<WorldObjectDefinitionComponent>() && entity.Has<TagCollectionComponent>() && entity.Get<TagCollectionComponent>().Contains("storage");

		public static bool CanStore(this Entity entity, Entity item)
		{
			if (!entity.IsStorageObject() || !item.Has<TagCollectionComponent>())
			{
				return false;
			}

			var worldObject = entity.Get<WorldObjectDefinitionComponent>();
			if (worldObject.AcceptedItemTags.Length == 0)
			{
				return true;
			}

			var itemTags = item.Get<TagCollectionComponent>();
			return worldObject.AcceptedItemTags.Any(itemTags.Contains);
		}

		public static bool HasInventorySpace(this Entity entity)
		{
			if (!entity.Has<InventoryComponent>())
			{
				return false;
			}

			var inventory = entity.Get<InventoryComponent>();
			return inventory.Capacity <= 0 || inventory.Items.Count < inventory.Capacity;
		}

		public static bool HasItemDefinition(this Entity entity, string itemDefinitionId)
			=> entity.GetInventory().Any(item => string.Equals(item.GetItemDefinitionId(), itemDefinitionId, System.StringComparison.OrdinalIgnoreCase));

		public static bool HasBodyPart(this Entity entity, string bodyPart)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().BodyParts.Any(part => string.Equals(part, bodyPart, System.StringComparison.OrdinalIgnoreCase));

		public static bool HasOrgan(this Entity entity, string organ)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().Organs.Any(part => string.Equals(part, organ, System.StringComparison.OrdinalIgnoreCase));

		public static bool HasSystem(this Entity entity, string system)
			=> entity.Has<LifeBodyComponent>() && entity.Get<LifeBodyComponent>().Systems.Any(part => string.Equals(part, system, System.StringComparison.OrdinalIgnoreCase));

		public static string GetFactionId(this Entity entity)
			=> entity.Has<FactionComponent>() ? entity.Get<FactionComponent>().FactionId : "neutral";

		public static bool HasMemoryCapability(this Entity entity)
			=> entity.Has<MemoryComponent>() && !string.IsNullOrWhiteSpace(entity.Get<MemoryComponent>().ProviderId);

		public static string GetMemoryProviderId(this Entity entity)
			=> entity.Has<MemoryComponent>() ? entity.Get<MemoryComponent>().ProviderId : string.Empty;

		public static void LearnFact(this Entity entity, string fact)
		{
			if (!entity.HasMemoryCapability() || string.IsNullOrWhiteSpace(fact))
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownFacts.Add(fact);
		}

		public static bool KnowsFact(this Entity entity, string fact)
			=> entity.HasMemoryCapability() && entity.Get<MemoryComponent>().KnownFacts.Contains(fact);

		public static IEnumerable<string> GetKnownFacts(this Entity entity)
			=> entity.HasMemoryCapability() ? entity.Get<MemoryComponent>().KnownFacts : Enumerable.Empty<string>();

		public static IEnumerable<KeyValuePair<string, Point>> GetKnownItemLocations(this Entity entity)
			=> entity.HasMemoryCapability() ? entity.Get<MemoryComponent>().KnownItemLocations : Enumerable.Empty<KeyValuePair<string, Point>>();

		public static void RememberItemLocation(this Entity entity, string itemId, Point cell)
		{
			if (!entity.HasMemoryCapability() || string.IsNullOrWhiteSpace(itemId))
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownItemLocations[itemId] = cell;
			memory.KnownFacts.Add(GoapFacts.KnowsItemLocation(itemId));
		}

		public static bool TryRecallItemLocation(this Entity entity, string itemId, out Point cell)
		{
			cell = default;
			if (!entity.HasMemoryCapability())
			{
				return false;
			}

			return entity.Get<MemoryComponent>().KnownItemLocations.TryGetValue(itemId, out cell);
		}

		public static void ForgetItemLocation(this Entity entity, string itemId)
		{
			if (!entity.HasMemoryCapability())
			{
				return;
			}

			ref var memory = ref entity.Get<MemoryComponent>();
			memory.KnownItemLocations.Remove(itemId);
			memory.KnownFacts.Remove(GoapFacts.KnowsItemLocation(itemId));
		}

		public static void ShareKnownItemLocation(this Entity entity, Entity source, string itemId)
		{
			if (source.TryRecallItemLocation(itemId, out var cell))
			{
				entity.RememberItemLocation(itemId, cell);
			}
		}

		public static void ShareKnownFact(this Entity entity, Entity source, string fact)
		{
			if (source.KnowsFact(fact))
			{
				entity.LearnFact(fact);
			}
		}

		public static bool IsRestLow(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return false;
			}

			var rest = entity.Get<RestNeedComponent>();
			return rest.Current <= rest.Max * 0.35f;
		}

		public static bool IsHungry(this Entity entity)
		{
			if (!entity.Has<HungerNeedComponent>())
			{
				return false;
			}

			var hunger = entity.Get<HungerNeedComponent>();
			return hunger.Current <= hunger.Max * 0.35f;
		}

		public static bool IsThirsty(this Entity entity)
		{
			if (!entity.Has<ThirstNeedComponent>())
			{
				return false;
			}

			var thirst = entity.Get<ThirstNeedComponent>();
			return thirst.Current <= thirst.Max * 0.35f;
		}

		public static void DecayRest(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return;
			}

			ref var rest = ref entity.Get<RestNeedComponent>();
			rest.Current = System.Math.Clamp(rest.Current - rest.DecayPerTick, 0f, rest.Max);
		}

		public static void RestoreRest(this Entity entity)
		{
			if (!entity.Has<RestNeedComponent>())
			{
				return;
			}

			ref var rest = ref entity.Get<RestNeedComponent>();
			rest.Current = System.Math.Clamp(rest.Current + rest.RecoveryPerTick, 0f, rest.Max);
		}

		public static void DecayHunger(this Entity entity)
		{
			if (!entity.Has<HungerNeedComponent>())
			{
				return;
			}

			ref var hunger = ref entity.Get<HungerNeedComponent>();
			hunger.Current = System.Math.Clamp(hunger.Current - hunger.DecayPerTick, 0f, hunger.Max);
		}

		public static void RestoreHunger(this Entity entity, float amount)
		{
			if (!entity.Has<HungerNeedComponent>())
			{
				return;
			}

			ref var hunger = ref entity.Get<HungerNeedComponent>();
			hunger.Current = System.Math.Clamp(hunger.Current + amount, 0f, hunger.Max);
		}

		public static void DecayThirst(this Entity entity)
		{
			if (!entity.Has<ThirstNeedComponent>())
			{
				return;
			}

			ref var thirst = ref entity.Get<ThirstNeedComponent>();
			thirst.Current = System.Math.Clamp(thirst.Current - thirst.DecayPerTick, 0f, thirst.Max);
		}

		public static void RestoreThirst(this Entity entity, float amount)
		{
			if (!entity.Has<ThirstNeedComponent>())
			{
				return;
			}

			ref var thirst = ref entity.Get<ThirstNeedComponent>();
			thirst.Current = System.Math.Clamp(thirst.Current + amount, 0f, thirst.Max);
		}

		public static void SetEnemyVisible(this Entity entity, bool visible, Point enemyCell, int durationTicks)
		{
			if (!entity.Has<PerceptionComponent>())
			{
				return;
			}

			ref var perception = ref entity.Get<PerceptionComponent>();
			perception.EnemyVisible = visible;
			perception.LastKnownEnemyCell = enemyCell;
			perception.ScanTicksRemaining = durationTicks;
		}

		public static void TickPerception(this Entity entity)
		{
			if (!entity.Has<PerceptionComponent>())
			{
				return;
			}

			ref var perception = ref entity.Get<PerceptionComponent>();
			perception.ScanTicksRemaining = System.Math.Max(0, perception.ScanTicksRemaining - 1);
			if (perception.ScanTicksRemaining == 0)
			{
				perception.EnemyVisible = false;
				perception.LastKnownEnemyCell = new Point(-1, -1);
			}
		}

		public static void SetHidden(this Entity entity, int durationTicks)
		{
			if (!entity.Has<StealthComponent>())
			{
				return;
			}

			ref var stealth = ref entity.Get<StealthComponent>();
			stealth.HiddenTicksRemaining = System.Math.Max(stealth.HiddenTicksRemaining, durationTicks);
		}

		public static void TickStealth(this Entity entity)
		{
			if (!entity.Has<StealthComponent>())
			{
				return;
			}

			ref var stealth = ref entity.Get<StealthComponent>();
			stealth.HiddenTicksRemaining = System.Math.Max(0, stealth.HiddenTicksRemaining - 1);
		}

		public static bool IsHidden(this Entity entity)
			=> entity.Has<StealthComponent>() && entity.Get<StealthComponent>().HiddenTicksRemaining > 0;

		public static void SetPatrolled(this Entity entity, int durationTicks)
		{
			if (!entity.Has<PatrolStateComponent>())
			{
				return;
			}

			ref var patrol = ref entity.Get<PatrolStateComponent>();
			patrol.PatrolledTicksRemaining = System.Math.Max(patrol.PatrolledTicksRemaining, durationTicks);
		}

		public static void TickPatrolState(this Entity entity)
		{
			if (!entity.Has<PatrolStateComponent>())
			{
				return;
			}

			ref var patrol = ref entity.Get<PatrolStateComponent>();
			patrol.PatrolledTicksRemaining = System.Math.Max(0, patrol.PatrolledTicksRemaining - 1);
		}

		public static bool IsRecentlyPatrolling(this Entity entity)
			=> entity.Has<PatrolStateComponent>() && entity.Get<PatrolStateComponent>().PatrolledTicksRemaining > 0;
	}
}
