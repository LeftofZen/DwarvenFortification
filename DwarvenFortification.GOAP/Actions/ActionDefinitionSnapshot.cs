namespace DwarvenFortification.GOAP.Actions
{
	public readonly record struct ActionDefinitionSnapshot(
		string Id,
		string Name,
		string TargetKind,
		string DestinationMode,
		int BaseCost,
		int DurationTicks,
		string[] RequiredTargetTags,
		string[] RequiredFacts,
		string[] BlockedByFacts,
		bool RequiresReservation,
		string[] AddFacts,
		string[] RemoveFacts);
}
