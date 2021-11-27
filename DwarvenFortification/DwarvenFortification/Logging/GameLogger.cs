using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DwarvenFortification.Logging
{
	public record LogLine(LogLevel Level, string Message, DateTime Time)
	{
		public override string ToString()
			=> $"[{Time.ToShortTimeString()}] [{Level}] \"{Message}\"";
	}

	public class GameLogger : ILogger
	{
		public void Log(LogLevel level, string message)
		{
			Logs.Add(new LogLine(level, message, DateTime.Now));
		}

		public List<LogLine> Logs { get; } = new();
	}
}
