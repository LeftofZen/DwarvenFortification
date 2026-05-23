using System.Numerics;

namespace DwarvenFortification.GOAP;

/// <summary>
/// A boxed value used inside conditions, effects, and the world state.
/// Equality is structural — two <see cref="GoapValue"/>s compare via <see cref="Value"/>.
/// </summary>
public readonly record struct GoapValue(object? Value)
{
	public override string? ToString() => Value?.ToString();

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	public static implicit operator GoapValue(bool Value) => new(Value);
	public static implicit operator GoapValue(sbyte Value) => new(Value);
	public static implicit operator GoapValue(byte Value) => new(Value);
	public static implicit operator GoapValue(short Value) => new(Value);
	public static implicit operator GoapValue(ushort Value) => new(Value);
	public static implicit operator GoapValue(int Value) => new(Value);
	public static implicit operator GoapValue(uint Value) => new(Value);
	public static implicit operator GoapValue(long Value) => new(Value);
	public static implicit operator GoapValue(ulong Value) => new(Value);
	public static implicit operator GoapValue(Int128 Value) => new(Value);
	public static implicit operator GoapValue(UInt128 Value) => new(Value);
	public static implicit operator GoapValue(BigInteger Value) => new(Value);
	public static implicit operator GoapValue(Half Value) => new(Value);
	public static implicit operator GoapValue(float Value) => new(Value);
	public static implicit operator GoapValue(double Value) => new(Value);
	public static implicit operator GoapValue(decimal Value) => new(Value);
	public static implicit operator GoapValue(string Value) => new(Value);
	public static implicit operator GoapValue(char Value) => new(Value);
	public static implicit operator GoapValue(DateTime Value) => new(Value);
	public static implicit operator GoapValue(DateTimeOffset Value) => new(Value);
	public static implicit operator GoapValue(TimeSpan Value) => new(Value);
	public static implicit operator GoapValue(Guid Value) => new(Value);
#pragma warning restore CS1591
}
