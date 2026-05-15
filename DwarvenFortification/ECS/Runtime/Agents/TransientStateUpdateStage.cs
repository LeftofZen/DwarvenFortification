using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;
using Microsoft.Xna.Framework;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class TransientStateUpdateStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (agent.Has<PerceptionComponent>())
			{
				ref var perception = ref agent.Get<PerceptionComponent>();
				perception.ScanTicksRemaining = System.Math.Max(0, perception.ScanTicksRemaining - 1);
				if (perception.ScanTicksRemaining == 0)
				{
					perception.EnemyVisible = false;
					perception.LastKnownEnemyCell = new Point(-1, -1);
				}
			}

			if (agent.Has<StealthComponent>())
			{
				ref var stealth = ref agent.Get<StealthComponent>();
				stealth.HiddenTicksRemaining = System.Math.Max(0, stealth.HiddenTicksRemaining - 1);
			}

			if (agent.Has<PatrolStateComponent>())
			{
				ref var patrol = ref agent.Get<PatrolStateComponent>();
				patrol.PatrolledTicksRemaining = System.Math.Max(0, patrol.PatrolledTicksRemaining - 1);
			}
		}
	}
}