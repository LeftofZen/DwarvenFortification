namespace DwarvenFortification.GOAP
{
	public readonly record struct ResourceNodeDefinitionSnapshot(
		string Id,
		string Name,
		string DisplayColorHex,
		string[] Tags,
		string[] SupportedActionIds,
		string[] RequiredToolItemIds,
		string YieldItemId,
		int YieldCount,
		bool BlocksMovement);
}
