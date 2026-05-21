namespace DwarvenFortification.GOAP;

public record GoapSensor(string StateId, Func<GoapValue> GetValue);
