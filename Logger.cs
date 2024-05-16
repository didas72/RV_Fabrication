using System;

namespace RV_Fabrication
{
	internal class Logger
	{
		public enum LogLevel
		{
			Error = 0,
			Warning = 1,
			Info = 2,
		}
		private static LogLevel logLevel = LogLevel.Info;



		public static void SetLogLevel(LogLevel level) => logLevel = level; 



		public static void Print(string msg)
		{
			Console.WriteLine(msg);
		}
		public static void Print(string msg, ConsoleColor color = ConsoleColor.White)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(msg);
		}
		public static void InfoMsg(string msg)
		{
			if (logLevel < LogLevel.Info) return;
			Print(msg, ConsoleColor.White);
		}
		public static void WarnMsg(string msg)
		{
			if (logLevel < LogLevel.Warning) return;
			Print(msg, ConsoleColor.Yellow);
		}
		public static void ErrorMsg(string msg)
		{
			if (logLevel < LogLevel.Error) return;
			Print(msg, ConsoleColor.Red);
		}
	}
}
