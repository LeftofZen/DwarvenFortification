using Arch.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace DwarvenFortification
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

	public struct HungerNeedComponent
	{
		public float Current;
		public float Max;
		public float DecayPerTick;
		public float RecoveryPerTick;
	}

	public struct ThirstNeedComponent
	{
		public float Current;
		public float Max;
		public float DecayPerTick;
		public float RecoveryPerTick;
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

	public struct TaskQueueComponent
	{
		public Queue<IAgentTask> Tasks;
	}

	public struct AgentArchetypeReferenceComponent
	{
		public string ArchetypeId;
	}

	public struct WorldObjectReferenceComponent
	{
		public string DefinitionId;
	}
}
