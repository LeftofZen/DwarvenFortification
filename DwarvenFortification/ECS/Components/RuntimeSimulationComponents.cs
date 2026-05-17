using Arch.Core;
using DwarvenFortification.Actions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace DwarvenFortification.ECS.Components
{
	public struct AgentTagComponent
	{
	}

	public struct WorldObjectTagComponent
	{
	}

	public struct RuntimeTransformComponent
	{
		public Point Position;
	}

	public struct CellReferenceComponent
	{
		public Point Cell;
	}

	public struct BodyComponent
	{
		public int Width;
		public int Height;
	}

	public struct AgentStatsComponent
	{
		public float Strength;
		public float BaseSpeed;
	}

	public struct RestNeedComponent
	{
		public float Current;
		public float Max;
		public float DecayPerTick;
		public float RecoveryPerTick;
	}

	public struct BodyNutritionComponent
	{
		public float CarbohydratesCurrent;
		public float CarbohydratesMax;
		public float ProteinCurrent;
		public float ProteinMax;
		public float FatCurrent;
		public float FatMax;
		public float SugarCurrent;
		public float SugarMax;
		public float HydrationCurrentLiters;
		public float HydrationMaxLiters;
		public float SugarUsePerTick;
		public float HydrationUsePerTick;
		public float SugarFromCarbohydratesPerTick;
		public float SugarFromFatPerTick;
		public float ProteinCatabolismPerTick;
	}

	public struct LifeBodyComponent
	{
		public string[] BodyParts;
		public string[] Organs;
		public string[] Systems;
	}

	public struct FactionComponent
	{
		public string FactionId;
	}

	public struct PerceptionComponent
	{
		public bool EnemyVisible;
		public Point LastKnownEnemyCell;
		public int ScanTicksRemaining;
	}

	public struct StealthComponent
	{
		public int HiddenTicksRemaining;
	}

	public struct PatrolStateComponent
	{
		public int PatrolledTicksRemaining;
	}

	public struct MemoryComponent
	{
		public string ProviderId;
		public HashSet<string> KnownFacts;
		public Dictionary<string, Point> KnownItemLocations;
	}

	public struct InventoryComponent
	{
		public List<Entity> Items;
		public int Capacity;
	}

	public struct ActionQueueComponent
	{
		public Queue<IAgentAction> Actions;
	}

	public struct AgentArchetypeReferenceComponent
	{
		public string ArchetypeId;
	}

	public struct WorldObjectReferenceComponent
	{
		public string DefinitionId;
	}

	public struct ConstructionSiteComponent
	{
		public string TargetDefinitionId;
		public MaterialCostComponent[] BuildCosts;
	}

	public struct ProductionOrderComponent
	{
		public string ActiveRecipeId;
		public int BatchesRequested;
		public int BatchesCompleted;
	}
}
