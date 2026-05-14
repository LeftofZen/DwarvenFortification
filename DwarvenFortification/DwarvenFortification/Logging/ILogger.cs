using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DwarvenFortification.Logging
{
	public interface ILogger
	{
		IReadOnlyList<LogLine> Logs { get; }
		void Log(LogLevel level, string message);
	}
}
