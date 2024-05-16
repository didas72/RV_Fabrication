using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RV_Fabrication
{
	internal class FabricationProcessor
	{
		private const string COMMENT_PREFIX = "#";
		private const string DIRECTIVE_PREFIX = ";";
		private const string IMACRO_PREFIX = "$";
		private const string MACRO_PREFIX = "$$";

		private const int genericError = -1;
		private const int includePass = 1, macroSearchPass = 2, macroApplicationPass = 3,
			functionSearchPass = 4, codeReadPass = 5, sectionImplementationPass = 6, mergePass = 7;

		private readonly List<string> includedFiles = [];
		private readonly Dictionary<string, Function> functions = [];
		private readonly List<string> poisonedSymbols = [];
		private Dictionary<string, string> imacros = [];
		private readonly Dictionary<string, Macro> macros = [];



		public FabricationProcessor() { }



		public void ProcessFile(string path)
		{
			string blobPath = Path.ChangeExtension(path, ".blob.s");
			string macroedPath = Path.ChangeExtension(path, ".macroed.s");

			StreamWriter sw = new(blobPath);
			IncludePass(path, sw);
			sw.Close();

			MacroSearchPass(blobPath);
			MacroApplicationPass(blobPath, macroedPath);
			FunctionSearchPass(macroedPath);
			//CodeReadPass(macroedPath);
		}




		private void IncludePass(string path, StreamWriter sw)
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
						Logger.ErrorMsg($"Include directive requires exactly one argument, {args.Length} provided.");
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
		private void MacroSearchPass(string blobPath)
		{
			StreamReader sr = new(blobPath);

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
					case "endmacro":
					case "funcdecl":
					case "poison":
						break; //Ignore

					case "imacro":
						DeclareIMacro(args);
						break;

					case "macro":
						DeclareMacro(args, sr);
						break;

					default:
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}
		}
		private void MacroApplicationPass(string blobPath, string macroedPath)
		{
			StreamReader sr = new(blobPath);
			StreamWriter sw = new(macroedPath);

			SortIMacros();

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine() ?? string.Empty;

				line = ReplaceIMacros(line);
				string cleaned = CleanLine(line);

				if (IsMacroCall(cleaned))
				{
					string macroName = GetMacroCall(cleaned, out string[] args);
					ApplyMacro(macroName, args, sw);
				}
				else
					sw.WriteLine(line);
			}

			sw.Close();
			sr.Close();
		}
		private void FunctionSearchPass(string macroedPath)
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
					case "imacro":
					case "macro":
					case "endmacro":
						break; //Ignore

					case "funcdecl":
						DeclareFunction(args);
						break;

					case "poison":
						PoisonSymbol(args);
						break;

					default:
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}

			sr.Close();
		}
		private void CodeReadPass(string macroedPath)
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
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;
				}
			}

			sr.Close();
		}



		private void DeclareFunction(string[] args)
		{
			if (args.Length < 1 || args.Length > 3)
			{
				Logger.ErrorMsg($"Directive funcdecl requires 1-3 arguments. {args.Length} provided.");
				Environment.Exit(functionSearchPass);
			}
			string name = args[0];
			if (functions.ContainsKey(name))
			{
				Logger.ErrorMsg($"Function '{name}' is already defined.");
				Environment.Exit(functionSearchPass);
			}
			functions.Add(name, new(name));
		}
		private void PoisonSymbol(string[] args)
		{
			if (args.Length < 1)
			{
				Logger.ErrorMsg($"Directive poison requires at least one argument. None provided.");
				Environment.Exit(functionSearchPass);
			}
			string symbol = args[0];
			if (poisonedSymbols.Contains(symbol))
			{
				Logger.WarnMsg($"Symbol '{symbol}' is already poisoned. Ignoring...");
				return;
			}
			poisonedSymbols.Add(symbol);
		}
		private void DeclareIMacro(string[] args)
		{
			if (args.Length != 2)
			{
				Logger.ErrorMsg($"Directive imacro requires exactly two arguments. {args.Length} provided.");
				Environment.Exit(macroSearchPass);
			}
			string name = args[0], content = args[1];
			if (imacros.ContainsKey(name))
			{
				Logger.ErrorMsg($"IMacro '{name}' is already defined.");
				Environment.Exit(macroSearchPass);
			}
			imacros.Add(name, content);
		}
		private void DeclareMacro(string[] args, StreamReader sr)
		{
			if (args.Length < 2)
			{
				Logger.ErrorMsg($"Directive macro requires at least two arguments. {args.Length} provided.");
				Environment.Exit(macroSearchPass);
			}
			string name = args[0]; string[] macro_args = args[1..];
			if (macros.ContainsKey(name))
			{
				Logger.ErrorMsg($"Macro '{name}' is already defined.");
				Environment.Exit(macroSearchPass);
			}
			Macro macro = new(name, macro_args);
			macros.Add(name, macro);

			while (!sr.EndOfStream)
			{
				string? srcLine = sr.ReadLine();
				string line = CleanLine(srcLine);
				if (IsDirective(line) && GetDirective(line, out _) == "endmacro")
					break;

				macro.Lines.Add(srcLine ?? string.Empty);
			}
		}
		private void SortIMacros()
		{
			List<KeyValuePair<string, string>> simacros = imacros.AsEnumerable().ToList();
			simacros.Sort(
				(KeyValuePair<string, string> a, KeyValuePair<string, string> b) =>
				a.Key.Length - b.Key.Length //TODO: Check sign here, longer should be first
			);
			imacros = simacros.ToDictionary();
		}
		private string ReplaceIMacros(string line)
		{
			//TODO: Error on undefined imacros (impl will probs optimize this function)

			foreach (KeyValuePair<string, string> imacro in imacros)
				line = line.Replace($"{IMACRO_PREFIX}{imacro.Key}", imacro.Value);

			return line;
		}
		private void ApplyMacro(string name, string[] args, StreamWriter sw)
		{
			if (!macros.ContainsKey(name))
			{
				Logger.ErrorMsg($"Macro '{name}' is not defined.");
				Environment.Exit(macroApplicationPass);
			}

			Macro macro = macros[name];

			if (args.Length != macro.Args.Length)
			{
				Logger.ErrorMsg($"Macro '{name}' requires {macro.Args.Length} arguments. {args.Length} provided.");
				Environment.Exit(macroApplicationPass);
			}

			foreach (string line in macro.Lines)
			{
				string cleaned = CleanLine(line);
				if (IsMacroCall(cleaned))
				{
					string macroName = GetMacroCall(cleaned, out string[] macro_args);
					ApplyMacro(macroName, macro_args, sw);
					continue;
				}
				string macroed = ReplaceIMacros(line);
				for (int i = 0; i < macro.Args.Length; i++)
					macroed = macroed.Replace(macro.Args[i], args[i]);
				sw.WriteLine(macroed);
			}
		}



		private static string CleanLine(string? srcLine)
		{
			string line = srcLine ?? string.Empty;
			string trimmed = line.Trim();

			int directive_idx = trimmed.IndexOf(DIRECTIVE_PREFIX);
			int comment_idx = trimmed.IndexOf(COMMENT_PREFIX);

			if (directive_idx == -1)
				return trimmed[..comment_idx];

			if (comment_idx != -1)
			{
				Logger.ErrorMsg($"Cannot include comments in lines with directives. Found '{line}'.");
				Environment.Exit(genericError);
			}
			if (directive_idx != 0)
			{
				Logger.ErrorMsg($"Directives must only be preceeded by whitespace. Found '{line}'.");
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
	}
}
