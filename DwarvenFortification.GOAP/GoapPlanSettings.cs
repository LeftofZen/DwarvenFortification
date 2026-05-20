namespace DwarvenFortification.GOAP
{
	/// <summary>
	/// Settings to fine-tune the finding of a <see cref="GoapPlan"/>.
	/// </summary>
	public struct GoapPlanSettings()
	{
		/// <summary>
		/// How many times to try to find a plan that reaches the goal before giving up.<br/>
		/// If too low, plans that take more actions will be missed.<br/>
		/// If too high, time will be wasted when there is no possible plan.<br/>
		/// Default: 1000
		/// </summary>
		public int MaxIterations { get; set; } = 1000;
		/// <summary>
		/// How much to consider plans that won't help the agent in the short term.<br/>
		/// If too low, plans that could be beneficial in the long term will be missed (e.g. buying a sword to deal more damage to the player).<br/>
		/// If too high, time will be wasted considering plans that are unlikely to reach the goal.<br/>
		/// Default: 10
		/// </summary>
		public int MaxDistanceEstimate { get; set; } = 10;
		/// <summary>
		/// The maximum number of actions in a valid plan.<br/>
		/// Default: ∞
		/// </summary>
		public int MaxActions { get; set; } = int.MaxValue;
		/// <summary>
		/// The maximum cost of a valid plan.<br/>
		/// Default: ∞
		/// </summary>
		public double MaxCost { get; set; } = double.MaxValue;
	}
}
