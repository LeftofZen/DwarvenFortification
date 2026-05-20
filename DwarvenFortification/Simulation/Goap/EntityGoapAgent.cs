using Arch.Core;
using DwarvenFortification.GOAP;
using System.Collections.Generic;

namespace DwarvenFortification.Simulation.Goap
{
	/// <summary>
	/// Adapts an Arch ECS <see cref="Entity"/> to the GOAP-layer <see cref="IGoapAgent"/> interface,
	/// keeping the GOAP library free of any ECS dependency.
	/// </summary>
	public readonly struct EntityGoapAgent(Entity entity, HashSet<string> state) : IGoapAgent
	{
		public Entity Entity { get; } = entity;
		public HashSet<string> GetCurrentState() => state;
	}
}
