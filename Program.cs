using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RV_Fabrication
{
	static class Program
	{
		private const string programName = "RV_Fabrication v0.3 by Didas72";

		private static string mainPath = string.Empty;



		private static void Main(string[] args)
		{
			Logger.Print(programName, ConsoleColor.White);

			mainPath = args[0]; //TODO: Argument parsing

			FabricationProcessor proc = new();
			proc.ProcessFile(mainPath);
			//TODO: Print statistics
		}
	}
}
