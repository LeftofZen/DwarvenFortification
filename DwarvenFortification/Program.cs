using System;

namespace DwarvenFortification
{
	public static class Program
	{
		[STAThread]
		static void Main()
		{
			using var game = new MainGame();
			game.Run();
		}
	}
}
