namespace DwarvenFortification.GOAP;

/// <summary>
/// Optional inclusive bounds for a single state value. Used to clamp the result of effects so
/// numeric (or other comparable) state can't drift outside its natural range.
/// </summary>
/// <remarks>
/// Bounds are the engine-level mechanism that makes operator goals safe to plan: when a state is
/// clamped, the planner's state-dedup eventually sees a repeated canonical value and stops
/// expanding. Without bounds, an action like <c>energy IncreaseBy 1</c> against a goal of
/// <c>energy &gt; 10_000</c> would generate a unique state per step forever.
/// </remarks>
public readonly record struct GoapStateBounds(GoapValue? Min = null, GoapValue? Max = null)
{
	/// <summary>Clamp <paramref name="Value"/> to <see cref="Min"/>/<see cref="Max"/> when set.</summary>
	public GoapValue Clamp(GoapValue Value)
	{
		if (Min is { } min && GoapComparison.LessThan.IsMet(Value, min))
		{
			return min;
		}
		if (Max is { } max && GoapComparison.GreaterThan.IsMet(Value, max))
		{
			return max;
		}
		return Value;
	}
}
