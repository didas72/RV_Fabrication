using System;
using System.IO;
using System.Collections.Generic;

namespace RV_Bozoer
{
	static class Program
	{
		private const string programName = "RV_Bozoer v0.2 by Didas72";

		/*
		* 0 = Err
		* 1 = Err+Warn
		* 2 = Err+Warn+Info
		*/
		private static int logLevel = 1;
		private static string mainPath = string.Empty;

		private static readonly List<string> includedFiles = [];
		private static readonly List<Function> functions = [];



		private static void Main(string[] args)
		{
			Print(programName, ConsoleColor.White);

			mainPath = args[0]; //TODO: Argument parsing

			string blobPath = Path.ChangeExtension(mainPath, ".blob.s");

			StreamWriter sw = new(blobPath);
			IncludePass(mainPath, sw);
			sw.Close();
		}



		private static void IncludePass(string path, StreamWriter sw)
		{
			StreamReader sr = new(path);
			int lineNum = 0;

			while (!sr.EndOfStream)
			{
				string? srcLine = sr.ReadLine();
				string line = CleanLine(srcLine);
				++lineNum;

				if (IsDirective(line))
				{
					string directive = GetDirective(line, out string[] args);
					if (directive != "include")
						goto includePass_skip_include;

					if (args.Length != 1)
					{
						ErrorMsg($"Include directive requires exactly one argument, {args.Length} provided.");
						Environment.Exit(1);
					}

					sw.WriteLine(srcLine);
					string filePath = Path.GetFullPath(args[0]);
					if (!includedFiles.Contains(filePath))
					{
						includedFiles.Add(filePath);
						IncludePass(args[0], sw);
					}
					continue;
				}

			includePass_skip_include:
				sw.WriteLine(srcLine);
			}

			sr.Close();
		}

		private static void SymbolSearchPass(string blobPath)
		{
			StreamReader sr = new(blobPath);

			while (!sr.EndOfStream)
			{
				string clean = CleanLine(sr.ReadLine());
				if (!IsDirective(clean)) continue;
				string directive = GetDirective(clean, out string[] args);

				switch (directive)
				{
					case "sect":
					case "include":
					case "endfunc":
					case "funccall":
					case "endmacro":
						break; //Ignore

					case "funcdecl":
						if (args.Length < 1 || args.Length > 3)
						{
							ErrorMsg($"Directive funcdecl requires 1-3 arguments. {args.Length} provided.");
							Environment.Exit(1);
						}
						functions.Add(new(args[0]));
						break;

					case "poison":
						if (args.Length < 1)
						{
							ErrorMsg($"Directive poison requires at least one argument. None provided.");
							Environment.Exit(1);
						}
						//TODO: Add to list
						break;

					default:
						WarnMsg($"Unknown directive '{directive}'. Skipping...");
						break;
				}
			}

            //TODO: Find poisons
            //TODO: Find imacros
            //TODO: Find macros

            sr.Close();
		}



		private static string CleanLine(string? srcLine)
		{
			string line = srcLine ?? "";
			string trimmed = line.Trim();

			int directive_idx = trimmed.IndexOf("#;");
			int comment_idx = trimmed.IndexOf('#');

			//In case there is a directive, return it (they should be isolated)
			//TODO: Add error for non isolated directives
			if (directive_idx != -1 && comment_idx == directive_idx)
				return trimmed[(directive_idx)..];

			return trimmed[..comment_idx];
		}
		private static bool IsDirective(string cleaned) => cleaned.StartsWith("#;");
		private static string GetDirective(string cleaned, out string[] args)
		{
			string[] parts = cleaned.Split(' ');
			args = (parts.Length >= 2) ? parts[1..] : [];
			return parts[0][2..];
		}
		


		public static void Print(string msg)
		{
			Console.WriteLine(msg);
		}

		public static void Print(string msg, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(msg);
		}

		public static void InfoMsg(string msg)
		{
			if (logLevel < 2) return;
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(msg);
		}

		public static void WarnMsg(string msg)
		{
			if (logLevel < 1) return;
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(msg);
		}

		public static void ErrorMsg(string msg)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(msg);
		}
	}

	internal class Function (string name)
	{
		public string Name { get; } = name;
	}
}
