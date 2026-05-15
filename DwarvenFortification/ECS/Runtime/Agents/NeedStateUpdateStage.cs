using Arch.Core;
using Arch.Core.Extensions;
using DwarvenFortification.ECS.Components;

namespace DwarvenFortification.ECS.Runtime.Agents
{
	public sealed class NeedStateUpdateStage : IAgentUpdateStage
	{
		public void Update(AgentRuntimeContext context, Entity agent)
		{
			if (agent.Has<RestNeedComponent>())
			{
				ref var rest = ref agent.Get<RestNeedComponent>();
				rest.Current = System.Math.Clamp(rest.Current - rest.DecayPerTick, 0f, rest.Max);
			}

			if (agent.Has<HungerNeedComponent>())
			{
				ref var hunger = ref agent.Get<HungerNeedComponent>();
				hunger.Current = System.Math.Clamp(hunger.Current - hunger.DecayPerTick, 0f, hunger.Max);
			}

			if (agent.Has<ThirstNeedComponent>())
			{
				ref var thirst = ref agent.Get<ThirstNeedComponent>();
				thirst.Current = System.Math.Clamp(thirst.Current - thirst.DecayPerTick, 0f, thirst.Max);
			}
		}
	}
}