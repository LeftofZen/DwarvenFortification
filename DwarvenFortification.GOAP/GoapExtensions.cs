namespace DwarvenFortification.GOAP;

public static class GoapExtensions
{
	/// <summary>
	/// Evaluates <paramref name="value"/> against <paramref name="operand"/> using the given comparison.
	/// Equality uses <see cref="object.Equals(object?, object?)"/>; ordered comparisons fall back to
	/// <see cref="IComparable"/> on the unwrapped left-hand value.
	/// </summary>
	public static bool IsMet(this GoapComparison Comparison, GoapValue value, GoapValue operand)
	{
		var left = value.Value;
		var right = operand.Value;
		return Comparison switch
		{
			GoapComparison.EqualTo => Equals(left, right),
			GoapComparison.NotEqualTo => !Equals(left, right),
			GoapComparison.LessThan => Compare(left, right) < 0,
			GoapComparison.GreaterThan => Compare(left, right) > 0,
			GoapComparison.LessThanOrEqualTo => Compare(left, right) <= 0,
			GoapComparison.GreaterThanOrEqualTo => Compare(left, right) >= 0,
			_ => false,
		};
	}

	static int Compare(object? left, object? right)
	{
		if (left is IComparable leftComparable && right != null)
		{
			return leftComparable.CompareTo(right);
		}

		return Equals(left, right) ? 0 : -1;
	}

	public static GoapValue Operate(this GoapOperation Operation, GoapValue ValueA, GoapValue ValueB)
	{
		// SetTo is the only op whose result is independent of ValueA.
		if (Operation == GoapOperation.SetTo)
		{
			return ValueB;
		}

		// Every other op is arithmetic on the underlying primitives. Operate on the wrapped
		// values directly so dynamic dispatch picks the primitive operators (int+int, ...).
		dynamic? a = ValueA.Value;
		dynamic? b = ValueB.Value;

		object? result = Operation switch
		{
			GoapOperation.IncreaseBy => a + b,
			GoapOperation.DecreaseBy => a - b,
			GoapOperation.MultiplyBy => a * b,
			GoapOperation.DivideBy => a / b,
			GoapOperation.ModuloBy => a % b,
			GoapOperation.ExponentiateBy => Math.Pow(Convert.ToDouble(a), Convert.ToDouble(b)),
			_ => throw new NotImplementedException()
		};

		return new GoapValue(result);
	}

	public static TValue? GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> Dictionary, TKey Key, TValue? Default = default)
		=> Dictionary.TryGetValue(Key, out var Value) ? Value : Default;
}