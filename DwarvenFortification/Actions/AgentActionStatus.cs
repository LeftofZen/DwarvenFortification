using DwarvenFortification.GOAP;

namespace DwarvenFortification.Actions
{
	public enum AgentActionStatus
	{
		Pending,
		Running,
		Succeeded,
		Failed,
		Cancelled,
	}

	public static class AgentActionStatusExtensions
	{
		/// <summary>
		/// Maps a terminal <see cref="AgentActionStatus"/> to the corresponding <see cref="GoapActionResult"/>.
		/// Cancelled is treated as Interrupted (the task could continue toward success later).
		/// Non-terminal statuses default to Failed.
		/// </summary>
		public static GoapActionResult ToGoapResult(this AgentActionStatus status)
			=> status switch
			{
				AgentActionStatus.Succeeded => GoapActionResult.Success,
				AgentActionStatus.Cancelled => GoapActionResult.Interrupted,
				_ => GoapActionResult.Failed,
			};
	}
}