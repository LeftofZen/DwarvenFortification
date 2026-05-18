namespace DwarvenFortification.GOAP
{
	public readonly record struct GoapAction(
		string Id,
		string Name,
		string TargetKind,
		string DestinationMode,
		int BaseCost,
		int DurationTicks,
		string[] Requirements,
		string[] Effects,
		string[] Skills);
}
