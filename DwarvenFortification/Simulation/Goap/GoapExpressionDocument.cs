using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DwarvenFortification.GOAP
{
	/// <summary>
	/// A condition / effect entry as it appears in actions.json or goals.json.
	/// Accepts two surface forms:
	///   1. Plain string: <c>"hunger.low"</c> or <c>"!enemy.visible"</c> (boolean fact, optionally negated).
	///   2. Structured object: <c>{ "state": "vital.hunger", "op": "&lt;=", "value": 35 }</c>
	///      where <c>op</c> is one of the GOAP comparison/arithmetic operators.
	/// </summary>
	[JsonConverter(typeof(GoapExpressionDocumentConverter))]
	public sealed class GoapExpressionDocument
	{
		/// <summary>Boolean fact form (may be prefixed with '!' for negation). Null when structured.</summary>
		public string Fact { get; init; }

		/// <summary>State key for the structured form.</summary>
		public string State { get; init; }

		/// <summary>Operator for the structured form (e.g. "==", "!=", "&lt;=", ">", "+=", "-=", "=").</summary>
		public string Op { get; init; }

		/// <summary>Operand for the structured form (bool/number/string).</summary>
		public JsonElement? Value { get; init; }

		public bool IsBooleanFact => !string.IsNullOrWhiteSpace(Fact);

		public bool IsNumericExpression => !string.IsNullOrWhiteSpace(State);

		public static GoapExpressionDocument FromFact(string fact)
			=> new() { Fact = fact };

		public GoapCondition ToCondition()
		{
			if (IsBooleanFact)
			{
				return GoapFactState.ToCondition(Fact);
			}

			if (!IsNumericExpression)
			{
				throw new InvalidOperationException("GoapExpressionDocument has neither a boolean fact nor a structured state.");
			}

			var comparison = ParseComparison(Op);
			return new GoapCondition(State, comparison, ToGoapValue(Value));
		}

		public GoapEffect ToEffect()
		{
			if (IsBooleanFact)
			{
				var key = GoapFactState.Normalize(Fact);
				return new GoapEffect(key, GoapOperation.SetTo, !Fact.StartsWith('!'));
			}

			if (!IsNumericExpression)
			{
				throw new InvalidOperationException("GoapExpressionDocument has neither a boolean fact nor a structured state.");
			}

			var op = ParseOperation(Op);
			return new GoapEffect(State, op, ToGoapValue(Value));
		}

		/// <summary>The fact key (without '!') when in boolean form, else empty string.</summary>
		public string BooleanKey => IsBooleanFact ? GoapFactState.Normalize(Fact) : string.Empty;

		/// <summary>The fact string (with '!' preserved) when in boolean form, else empty.</summary>
		public string BooleanFact => IsBooleanFact ? Fact : string.Empty;

		static GoapComparison ParseComparison(string op) => op switch
		{
			"=" or "==" => GoapComparison.EqualTo,
			"!=" or "<>" => GoapComparison.NotEqualTo,
			"<" => GoapComparison.LessThan,
			">" => GoapComparison.GreaterThan,
			"<=" => GoapComparison.LessThanOrEqualTo,
			">=" => GoapComparison.GreaterThanOrEqualTo,
			_ => throw new InvalidOperationException($"Unknown GOAP comparison operator '{op}'."),
		};

		static GoapOperation ParseOperation(string op) => op switch
		{
			"=" or ":=" => GoapOperation.SetTo,
			"+=" => GoapOperation.IncreaseBy,
			"-=" => GoapOperation.DecreaseBy,
			"*=" => GoapOperation.MultiplyBy,
			"/=" => GoapOperation.DivideBy,
			"%=" => GoapOperation.ModuloBy,
			"^=" => GoapOperation.ExponentiateBy,
			_ => throw new InvalidOperationException($"Unknown GOAP arithmetic operator '{op}'."),
		};

		static GoapValue ToGoapValue(JsonElement? value)
		{
			if (value is not { } el)
			{
				throw new InvalidOperationException("Structured GoapExpressionDocument is missing 'value'.");
			}

			return el.ValueKind switch
			{
				JsonValueKind.True => (GoapValue)true,
				JsonValueKind.False => (GoapValue)false,
				JsonValueKind.Number => el.TryGetInt64(out var i) && i >= int.MinValue && i <= int.MaxValue
					? (GoapValue)(int)i
					: (GoapValue)el.GetDouble(),
				JsonValueKind.String => (GoapValue)el.GetString(),
				_ => throw new InvalidOperationException($"Unsupported GoapExpressionDocument value kind '{el.ValueKind}'."),
			};
		}
	}

	sealed class GoapExpressionDocumentConverter : JsonConverter<GoapExpressionDocument>
	{
		public override GoapExpressionDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return GoapExpressionDocument.FromFact(reader.GetString());
				case JsonTokenType.StartObject:
				{
					using var doc = JsonDocument.ParseValue(ref reader);
					var el = doc.RootElement;
					string fact = null, state = null, op = null;
					JsonElement? value = null;
					foreach (var prop in el.EnumerateObject())
					{
						switch (prop.Name.ToLowerInvariant())
						{
							case "fact": fact = prop.Value.GetString(); break;
							case "state": state = prop.Value.GetString(); break;
							case "op": op = prop.Value.GetString(); break;
							case "value": value = prop.Value.Clone(); break;
						}
					}

					return new GoapExpressionDocument { Fact = fact, State = state, Op = op, Value = value };
				}
				default:
					throw new JsonException($"Unexpected token '{reader.TokenType}' for GoapExpressionDocument.");
			}
		}

		public override void Write(Utf8JsonWriter writer, GoapExpressionDocument value, JsonSerializerOptions options)
		{
			if (value is null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.IsBooleanFact)
			{
				writer.WriteStringValue(value.Fact);
				return;
			}

			writer.WriteStartObject();
			writer.WriteString("state", value.State ?? string.Empty);
			writer.WriteString("op", value.Op ?? string.Empty);
			if (value.Value.HasValue)
			{
				writer.WritePropertyName("value");
				value.Value.Value.WriteTo(writer);
			}

			writer.WriteEndObject();
		}
	}
}
