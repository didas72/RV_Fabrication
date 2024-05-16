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
		private const string FABRICATOR_PREFIX = COMMENT_PREFIX + "[[FABR]] ";
		private const string MACRO_COMMENT = FABRICATOR_PREFIX + "MACRO_CODE: ";

		private const int genericError = -1;
		private const int includePass = 1, macroSearchPass = 2, macroApplicationPass = 3,
			symbolSearchPass = 4, codeReadPass = 5, sectionImplementationPass = 6, mergePass = 7;

		private readonly List<string> includedFiles = [];
		private readonly Dictionary<string, Function> functions = [];
		private readonly List<string> poisonedSymbols = [];
		private Dictionary<string, IMacro> imacros = [];
		private readonly Dictionary<string, Macro> macros = [];

		private string rootFile = string.Empty;
		private string rootParent = string.Empty;



		private enum Directive
		{
			None = 0,
			Include,
			Sect,
			FuncDecl,
			EndFunc,
			FuncCall,
			SectOrd,
			Poison,
			IMacro,
			Macro,
			EndMacro,
		}



		public FabricationProcessor() { }



		public void ProcessFile(string path)
		{
			rootFile = Path.GetFullPath(path);
			rootParent = Path.GetDirectoryName(path) ?? string.Empty;
			string blobPath = Path.ChangeExtension(rootFile, ".blob.s");
			string macroedPath = Path.ChangeExtension(rootFile, ".macroed.s");

			StreamWriter sw = new(blobPath);
			IncludePass(rootFile, sw);
			sw.Close();
			LogIncludedFiles();

			MacroSearchPass(blobPath);
			LogFoundMacros();

			MacroApplicationPass(blobPath, macroedPath);
			LogAppliedMacros();

			SymbolSearchPass(macroedPath);
			LogFoundSymbols();

			//CodeReadPass(macroedPath);
		}




		private void IncludePass(string path, StreamWriter sw)
		{
			string relPath = Path.GetRelativePath(rootParent, path);
			string parentPath = Path.GetDirectoryName(path) ?? string.Empty;
			if (includedFiles.Contains(relPath))
				return;
			includedFiles.Add(relPath);
			StreamReader sr = new(path);
			int lineNum = 0;

			while (!sr.EndOfStream)
			{
				string? srcLine = sr.ReadLine();
				string line = CleanLine(srcLine);
				++lineNum;

				if (IsDirective(line))
				{
					Directive directive = GetDirective(line, out string[] args);
					if (directive != Directive.Include)
						goto includePass_skip_include;

					if (args.Length != 1)
					{
						Logger.ErrorMsg($"Include directive requires exactly one argument, {args.Length} provided.");
						Environment.Exit(includePass);
					}

					sw.WriteLine(srcLine);
					string includePath = Path.GetFullPath(Path.Combine(parentPath, args[0]));
					IncludePass(includePath, sw);
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
				Directive directive = GetDirective(clean, out string[] args);

				switch (directive)
				{
					case Directive.EndMacro:
						Logger.ErrorMsg($"Found an isolated endmacro directive. Are you missing a macro directive?");
						Environment.Exit(macroSearchPass);
						break;

					case Directive.IMacro:
						DeclareIMacro(args);
						break;

					case Directive.Macro:
						DeclareMacro(args, sr);
						break;

					case Directive.None:
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;

					default:
						break; //Ignore
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

				if (IsDirective(cleaned) && GetDirective(cleaned, out _) == Directive.Macro)
					CommentMacro(line, sr, sw);
				else if (IsMacroCall(cleaned))
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
		private void SymbolSearchPass(string macroedPath)
		{
			StreamReader sr = new(macroedPath);

			while (!sr.EndOfStream)
			{
				string clean = CleanLine(sr.ReadLine());
				if (!IsDirective(clean)) continue;
				Directive directive = GetDirective(clean, out string[] args);

				switch (directive)
				{
					case Directive.FuncDecl:
						DeclareFunction(args);
						break;

					case Directive.Poison:
						PoisonSymbol(args);
						break;

					case Directive.None:
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;

					default:
						break; //Ingore
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
				Directive directive = GetDirective(clean, out string[] args);

				//TODO: Finish implementing

				switch (directive)
				{
					case Directive.None:
						Logger.WarnMsg($"Unknown or unhandled directive '{directive}'. Skipping...");
						break;

					default:
						break; //Ignore
				}
			}

			sr.Close();
		}



		private void DeclareFunction(string[] args)
		{
			if (args.Length < 1 || args.Length > 3)
			{
				Logger.ErrorMsg($"Directive funcdecl requires 1-3 arguments. {args.Length} provided.");
				Environment.Exit(symbolSearchPass);
			}
			string name = args[0];
			if (functions.ContainsKey(name))
			{
				Logger.ErrorMsg($"Function '{name}' is already defined.");
				Environment.Exit(symbolSearchPass);
			}
			functions.Add(name, new(name));
		}
		private void PoisonSymbol(string[] args)
		{
			if (args.Length < 1)
			{
				Logger.ErrorMsg($"Directive poison requires at least one argument. None provided.");
				Environment.Exit(symbolSearchPass);
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
			imacros.Add(name, new (name, content));
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

			bool foundEnd = false;

			while (!sr.EndOfStream)
			{
				string? srcLine = sr.ReadLine();
				string line = CleanLine(srcLine);
				if (IsDirective(line) && GetDirective(line, out _) == Directive.EndMacro)
				{
					foundEnd = true; break;
				}

				macro.Lines.Add(srcLine ?? string.Empty);
			}

			if (!foundEnd)
			{
				Logger.ErrorMsg($"Macro '{name}' is missing and endmacro directive.");
				Environment.Exit(macroSearchPass);
			}
		}
		private void CommentMacro(string macroLine, StreamReader sr, StreamWriter sw)
		{
			sw.WriteLine(MACRO_COMMENT + macroLine);

			while (!sr.EndOfStream)
			{
				string? srcLine = sr.ReadLine();
				string line = CleanLine(srcLine);
				sw.WriteLine(MACRO_COMMENT + srcLine);
				if (IsDirective(line) && GetDirective(line, out _) == Directive.EndMacro)
					break;
			}
		}
		private void SortIMacros()
		{
			List<KeyValuePair<string, IMacro>> simacros = imacros.AsEnumerable().ToList();
			simacros.Sort(
				(KeyValuePair<string, IMacro> a, KeyValuePair<string, IMacro> b) =>
				a.Key.Length - b.Key.Length //TODO: Check sign here, longer should be first
			);
			imacros = simacros.ToDictionary();
		}
		private string ReplaceIMacros(string line)
		{
			//TODO: Error on undefined imacros (impl will probs optimize this function)

			foreach (IMacro imacro in imacros.Values)
			{
				if (line.Contains($"{IMACRO_PREFIX}{imacro.Name}"))
				{
					line = line.Replace($"{IMACRO_PREFIX}{imacro.Name}", imacro.Value);
					imacro.References++;
				}
			}

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

			sw.WriteLine(FABRICATOR_PREFIX + $"MACRO {name}");

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

			sw.WriteLine(FABRICATOR_PREFIX + $"ENDMACRO {name}");

			macro.References++;
		}



		private void LogIncludedFiles()
		{
			Logger.InfoMsg($"Included {includedFiles.Count} " + (includedFiles.Count != 1 ? "files:" : "file:"));
			foreach (string file in includedFiles)
				Logger.InfoMsg($"\t{file}");
		}
		private void LogFoundMacros()
		{
			Logger.InfoMsg($"Found {imacros.Count} " + (imacros.Count != 1 ? "imacros" : "imacro") + $" and {macros.Count} " + (macros.Count != 1 ? "macros" : "macro") + ":");
			foreach (string imacro in imacros.Keys)
				Logger.InfoMsg($"\tIMacro: {imacro}");
			foreach (string macro in macros.Keys)
				Logger.InfoMsg($"\tMacro: {macro}");
		}
		private void LogAppliedMacros()
		{
			int totalIMacroApplies = 0, totalMacroApplies = 0;
			foreach (IMacro imacro in imacros.Values)
				totalIMacroApplies += imacro.References;
			foreach (Macro macro in macros.Values)
				totalMacroApplies += macro.References;

			Logger.InfoMsg($"Applied {totalIMacroApplies} " + (totalIMacroApplies != 1 ? "imacros" : "imacro") + $" and {totalMacroApplies} " + (totalMacroApplies != 1 ? "macros" : "macro") + ":");
			foreach (IMacro imacro in imacros.Values)
				Logger.InfoMsg($"\tIMacro: {imacro.Name}: {imacro.References}");
			foreach (Macro macro in macros.Values)
				Logger.InfoMsg($"\tMacro: {macro.Name}: {macro.References}");
		}
		private void LogFoundSymbols()
		{
			Logger.InfoMsg($"Found {functions.Count} " + (functions.Count != 1 ? "functions" : "function") + $" and poisoned {poisonedSymbols.Count} " + (poisonedSymbols.Count != 1 ? "symbols" : "symbol") + ":");
			foreach (Function func in functions.Values)
				Logger.InfoMsg($"\tFunction {func.Name}");
			foreach (string symbol in poisonedSymbols)
				Logger.InfoMsg($"\tPoisoned symbol {symbol}");
		}



		private static string CleanLine(string? srcLine)
		{
			string line = srcLine ?? string.Empty;
			string trimmed = line.Trim();

			int directive_idx = trimmed.IndexOf(DIRECTIVE_PREFIX);
			int comment_idx = trimmed.IndexOf(COMMENT_PREFIX);

			if (directive_idx == -1)
				return comment_idx != -1 ? trimmed[..comment_idx] : trimmed;

			if (comment_idx != -1 && comment_idx > directive_idx)
			{
				Logger.ErrorMsg($"Cannot include comments in lines with directives. Found '{line}'.");
				Environment.Exit(genericError);
			}
			if (directive_idx != 0 && comment_idx > directive_idx)
			{
				Logger.ErrorMsg($"Directives must only be preceeded by whitespace. Found '{line}'.");
				Environment.Exit(genericError);
			}

			return trimmed;
		}
		private static bool IsDirective(string cleaned) => cleaned.StartsWith(DIRECTIVE_PREFIX);
		private static Directive GetDirective(string cleaned, out string[] args)
		{
			string[] parts = cleaned.Split();
			args = (parts.Length >= 2) ? parts[1..] : [];

			return (parts[0][DIRECTIVE_PREFIX.Length..]) switch
			{
				"include" => Directive.Include,
				"sect" => Directive.Sect,
				"funcdecl" => Directive.FuncDecl,
				"endfunc" => Directive.EndFunc,
				"funccall" => Directive.FuncCall,
				"sectord" => Directive.SectOrd,
				"poison" => Directive.Poison,
				"imacro" => Directive.IMacro,
				"macro" => Directive.Macro,
				"endmacro" => Directive.EndMacro,
				_ => Directive.None,
			};
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
