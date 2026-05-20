using System;
using System.Collections.Generic;

namespace DwarvenFortification.Logging
{
	public record LogLine(LogLevel Level, string Message, DateTime Time)
	{
		public override string ToString()
			=> $"[{Time.ToShortTimeString()}] [{Level}] \"{Message}\"";
	}

	public class GameLogger : ILogger
	{
		public IReadOnlyList<LogLine> Logs => logs;

		public void Log(LogLevel level, string message) => logs.Add(new LogLine(level, message, DateTime.Now));

		readonly List<LogLine> logs = [];
	}
}
