using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RV_Bozoer
{
	static class Program
	{
		private const string programName = "RV_Fabrication v0.3 by Didas72";

		/*
		* 0 = Err
		* 1 = Err+Warn
		* 2 = Err+Warn+Info
		*/
		private static int logLevel = 1;
		private static string mainPath = string.Empty;

		private const string COMMENT_PREFIX = "#";
		private const string DIRECTIVE_PREFIX = ";";
		private const string IMACRO_PREFIX = "$";
		private const string MACRO_PREFIX = "$$";

		private static readonly List<string> includedFiles = [];
		private static readonly Dictionary<string, Function> functions = [];
		private static readonly List<string> poisonedSymbols = [];
		private static readonly Dictionary<string, string> imacros = [];
		private static readonly Dictionary<string, Macro> macros = [];



		private static void Main(string[] args)
		{
			Print(programName, ConsoleColor.White);

			mainPath = args[0]; //TODO: Argument parsing

			string blobPath = Path.ChangeExtension(mainPath, ".blob.s");
			string macroedPath = Path.ChangeExtension(mainPath, ".macroed.s");

			StreamWriter sw = new(blobPath);
			IncludePass(mainPath, sw);
			sw.Close();

			MacroSearchPass(blobPath);
			MacroApplicationPass(blobPath, macroedPath);
			FunctionSearchPass(macroedPath);
			//CodeReadPass(macroedPath);
		}



		private const int genericError = -1;
		private const int includePass = 1, macroSearchPass = 2, macroApplicationPass = 3,
			functionSearchPass = 4, codeReadPass = 5, sectionImplementationPass = 6, mergePass = 7;
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
						Environment.Exit(includePass);
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
		private static void MacroSearchPass(string blobPath)
		{
			StreamReader sr = new(blobPath);

			//TODO: Also read macro code

			while (!sr.EndOfStream)
			{
				string clean = CleanLine(sr.ReadLine());
				if (!IsDirective(clean)) continue;
				string directive = GetDirective(clean, out string[] args);

				switch (directive)
				{
					case "include":
					case "sect":
					case "endfunc":
					case "funccall":
					case "namereg":
					case "endmacro":
					case "funcdecl":
					case "poison":
						break; //Ignore

					case "imacro":
						DeclareIMacro(args);
						break;

					case "macro":
						DeclareMacro(args);
						break;

					default:
						WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}
		}
		private static void MacroApplicationPass(string blobPath, string macroedPath)
		{
			StreamReader sr = new(blobPath);
			StreamWriter sw = new(macroedPath);

			List<KeyValuePair<string, string>> simacros = imacros.AsEnumerable().ToList();
			simacros.Sort(
				(KeyValuePair<string, string> a, KeyValuePair<string, string> b) => 
				a.Key.Length - b.Key.Length //TODO: Check sign here, longer should be first
			);

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine() ?? "";

				line = ReplaceIMacros(line, simacros);
				string cleaned = CleanLine(line);

				if (IsMacroCall(cleaned))
				{
					string macroName = GetMacroCall(cleaned, out string[] args);

					if (!macros.ContainsKey(macroName))
					{
						ErrorMsg($"Macro '{macroName}' is not defined.");
						Environment.Exit(macroApplicationPass);
					}
					//TODO: Apply macro
				}
				else
					sw.WriteLine(line);
            }

			sw.Close();
			sr.Close();
		}
		private static void FunctionSearchPass(string macroedPath)
		{
			StreamReader sr = new(macroedPath);

			while (!sr.EndOfStream)
			{
				string clean = CleanLine(sr.ReadLine());
				if (!IsDirective(clean)) continue;
				string directive = GetDirective(clean, out string[] args);

				switch (directive)
				{
					case "include":
					case "sect":
					case "endfunc":
					case "funccall":
					case "namereg":
					case "endmacro":
						break; //Ignore

					case "imacro":
					case "macro":
						//Should have already been removed
						WarnMsg("Found (i)macro in function search pass. Ignoring...");
						break; 

					case "funcdecl":
						DeclareFunction(args);
						break;

					case "poison":
						PoisonSymbol(args);
						break;

					default:
						WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}

			sr.Close();
		}
		private static void CodeReadPass(string macroedPath)
		{
			StreamReader sr = new(macroedPath);

			while (!sr.EndOfStream)
			{
				string clean = CleanLine(sr.ReadLine());
				if (!IsDirective(clean)) continue;
				string directive = GetDirective(clean, out string[] args);

				//TODO: Finish implementing

				switch (directive)
				{
					case "include":
					case "sect":
						break; //Ignore

					default:
						WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}

			sr.Close();
		}



		private static string CleanLine(string? srcLine)
		{
			string line = srcLine ?? "";
			string trimmed = line.Trim();

			int directive_idx = trimmed.IndexOf(DIRECTIVE_PREFIX);
			int comment_idx = trimmed.IndexOf(COMMENT_PREFIX);

			if (directive_idx == -1)
				return trimmed[..comment_idx];

			if (comment_idx != -1)
			{
				ErrorMsg($"Cannot include comments in lines with directives. Found '{line}'.");
				Environment.Exit(genericError);
			}
			if (directive_idx != 0)
			{
				ErrorMsg($"Directives must only be preceeded by whitespace. Found '{line}'.");
				Environment.Exit(genericError);
			}

			return trimmed;
		}
		private static bool IsDirective(string cleaned) => cleaned.StartsWith(DIRECTIVE_PREFIX);
		private static string GetDirective(string cleaned, out string[] args)
		{
			string[] parts = cleaned.Split();
			args = (parts.Length >= 2) ? parts[1..] : [];
			return parts[0][DIRECTIVE_PREFIX.Length..];
		}
		private static bool IsMacroCall(string cleaned) => cleaned.StartsWith(MACRO_PREFIX);
		private static string GetMacroCall(string cleaned, out string[] args)
		{
			string[] parts = cleaned.Split();
			args = (parts.Length >= 2) ? parts[1..] : [];
			return parts[0][MACRO_PREFIX.Length..];
		}
		private static string ReplaceIMacros(string line, List<KeyValuePair<string, string>> simacros)
		{
			//TODO: Error on undefined imacros (impl will probs optimize this function)

			foreach (KeyValuePair<string, string> imacro in simacros)
				line = line.Replace($"${imacro.Key}", imacro.Value);

			return line;
		}



		private static void DeclareFunction(string[] args)
		{
			if (args.Length < 1 || args.Length > 3)
			{
				ErrorMsg($"Directive funcdecl requires 1-3 arguments. {args.Length} provided.");
				Environment.Exit(functionSearchPass);
			}
			string name = args[0];
			if (functions.ContainsKey(name))
			{
				ErrorMsg($"Function '{name}' is already defined.");
				Environment.Exit(functionSearchPass);
			}
			functions.Add(name, new(name));
		}
		private static void PoisonSymbol(string[] args)
		{
			if (args.Length < 1)
			{
				ErrorMsg($"Directive poison requires at least one argument. None provided.");
				Environment.Exit(functionSearchPass);
			}
			string symbol = args[0];
			if (poisonedSymbols.Contains(symbol))
			{
				WarnMsg($"Symbol '{symbol}' is already poisoned. Ignoring...");
				return;
			}
			poisonedSymbols.Add(symbol);
		}
		private static void DeclareIMacro(string[] args)
		{
			if (args.Length != 2)
			{
				ErrorMsg($"Directive imacro requires exactly two arguments. {args.Length} provided.");
				Environment.Exit(macroSearchPass);
			}
			string name = args[0], content = args[1];
			if (imacros.ContainsKey(name))
			{
				ErrorMsg($"IMacro '{name}' is already defined.");
				Environment.Exit(macroSearchPass);
			}
			imacros.Add(name, content);
		}
		private static void DeclareMacro(string[] args)
		{
			if (args.Length < 2)
			{
				ErrorMsg($"Directive macro requires at least two arguments. {args.Length} provided.");
				Environment.Exit(macroSearchPass);
			}
			string name = args[0]; string[] macro_args = args[1..];
			if (macros.ContainsKey(name))
			{
				ErrorMsg($"Macro '{name}' is already defined.");
				Environment.Exit(macroSearchPass);
			}
			macros.Add(name, new(name, macro_args));
		}
		


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
			if (logLevel < 2) return;
			Print(msg, ConsoleColor.White);
		}
		public static void WarnMsg(string msg)
		{
			if (logLevel < 1) return;
			Print(msg, ConsoleColor.Yellow);
		}
		public static void ErrorMsg(string msg)
		{
			Print(msg, ConsoleColor.Red);
		}
	}

	internal class Function(string name)
	{
		public string Name { get; } = name;
	}

	internal class Macro(string name, string[] args)
	{
		public string Name { get; } = name;
		public string[] Args { get; } = args;
	}
}
