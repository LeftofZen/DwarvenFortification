using System.Collections.Generic;

namespace DwarvenFortification.Logging
{
	public interface ILogger
	{
		IReadOnlyList<LogLine> Logs { get; }
		void Log(LogLevel level, string message);
	}
}
