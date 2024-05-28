using System;
using System.IO;

namespace RV_Fabrication
{
	static class Program
	{
		private const string programName = "RV_Fabrication v1.0 by Didas72";

		private static string inPath = string.Empty, outPath = string.Empty;
		private static FabricationProcessor.InlineMode inlineMode = FabricationProcessor.InlineMode.Auto;
		private static bool logIncludes = false, logSourceMetrics = false, logFoundMacros = false, logAppliedMacros = false, logSymbols = false;



		private static void Main(string[] args)
		{
			Logger.Print(programName, ConsoleColor.White);

			ParseArgs(args);

			FabricationProcessor proc = new(inlineMode);
			proc.ProcessFile(inPath, outPath);

			if (logIncludes) proc.LogIncludedFiles();
			if (logSourceMetrics) proc.LogSourceMetrics();
			if (logFoundMacros) proc.LogFoundMacros();
			if (logAppliedMacros) proc.LogAppliedMacros();
			if (logSymbols) proc.LogFoundSymbols();
		}


		private static bool inlineModeSet = false, logLevelSet = false;
		private static void ParseArgs(string[] args)
		{
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i].StartsWith('-')) //Is switch
				{
					if (args[i].Length < 2)
					{
						Logger.ErrorMsg($"Invalid empty switch argument.");
						Environment.Exit(1);
					}

					for (int j = 1; j < args[i].Length; j++)
						ParseSwitch(args[i][j]);
				}
				else //Is full argument, in this case, should be inPath
				{
					if (inPath != string.Empty && outPath != string.Empty)
					{
						Logger.ErrorMsg($"Found third non-switch argument: {args[i]}");
						Environment.Exit(1);
					}
					else if (inPath == string.Empty) inPath = args[i];
					else outPath = args[i];
				}
			}

			if (inPath == string.Empty)
			{
				Logger.ErrorMsg("No input file specified.");
				Environment.Exit(1);
			}
			if (outPath == string.Empty)
			{
				outPath = Path.Combine(Path.GetDirectoryName(inPath) ?? "",
					Path.GetFileNameWithoutExtension(inPath) + "_out" + Path.GetExtension(inPath));
			}
		}

		private static void ParseSwitch(char ch)
		{
			switch (ch)
			{
				case 'h':
					PrintUsage();
					Environment.Exit(0);
					break;

				case 'a':
					if (inlineModeSet)
					{
						Logger.ErrorMsg("Only one inlining mode switch may be used.");
						Environment.Exit(1);
					}
					inlineMode = FabricationProcessor.InlineMode.Agressive;
					inlineModeSet = true;
					break;

				case 'p':
					if (inlineModeSet)
					{
						Logger.ErrorMsg("Only one inlining mode switch may be used.");
						Environment.Exit(1);
					}
					inlineMode = FabricationProcessor.InlineMode.Prohibit;
					inlineModeSet = true;
					break;

				case 'q':
					if (logLevelSet)
					{
						Logger.ErrorMsg("Only one logging mode switch may be used.");
						Environment.Exit(1);
					}
					Logger.SetLogLevel(Logger.LogLevel.Error);
					logLevelSet = true;
					break;

				case 'w':
					if (logLevelSet)
					{
						Logger.ErrorMsg("Only one logging mode switch may be used.");
						Environment.Exit(1);
					}
					Logger.SetLogLevel(Logger.LogLevel.Warning);
					logLevelSet = true;
					break;

				case 'l':
					if (logLevelSet)
					{
						Logger.ErrorMsg("Only one logging mode switch may be used.");
						Environment.Exit(1);
					}
					Logger.SetLogLevel(Logger.LogLevel.Info);
					logLevelSet = true;
					break;

				case 'i':
					if (logIncludes)
					{
						Logger.ErrorMsg("Only one include logging switch may be used.");
						Environment.Exit(1);
					}
					logIncludes = true;
					break;

				case 's':
					if (logSourceMetrics)
					{
						Logger.ErrorMsg("Only one source metrics logging switch may be used.");
						Environment.Exit(1);
					}
					logSourceMetrics = true;
					break;

				case 'm':
					if (logFoundMacros)
					{
						Logger.ErrorMsg("Only one found macros logging switch may be used.");
						Environment.Exit(1);
					}
					logFoundMacros = true;
					break;

				case 'M':
					if (logAppliedMacros)
					{
						Logger.ErrorMsg("Only one apply macros logging switch may be used.");
						Environment.Exit(1);
					}
					logAppliedMacros = true;
					break;

				case 'S':
					if (logSymbols)
					{
						Logger.ErrorMsg("Only one apply macros logging switch may be used.");
						Environment.Exit(1);
					}
					logSymbols = true;
					break;

				default:
					Logger.ErrorMsg($"Unknown switch {ch}.");
					Environment.Exit(1);
					break;
			}
		}

		private static void PrintUsage()
		{
			Logger.Print("Usage:");
			Logger.Print(Path.GetFileName(Environment.ProcessPath) + " [options] <in_file> [out_file]");
			Logger.Print("\t<src_file> - Path to the file to process");
			Logger.Print("Options:");
			Logger.Print("\ta - Agressive inlining");
			Logger.Print("\tp - Prohibit inlining");
			Logger.Print("\tq - Only print errors");
			Logger.Print("\tw - Only print errors and warnings (default)");
			Logger.Print("\tl - Print all logs");
			Logger.Print("\ti - List included files");
			Logger.Print("\ts - List source metrics");
			Logger.Print("\tm - List found macros");
			Logger.Print("\tM - List applied macros");
			Logger.Print("\tS - List functions found and posioned symbols");
		}
	}
}
