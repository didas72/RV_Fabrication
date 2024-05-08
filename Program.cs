using System;
using System.IO;
using System.Collections.Generic;

namespace RV_Bozoer
{
	static class Program
	{
		private const string programName = "RV_Bozoer v0.2 by Didas72";
		private static readonly Stack<FileStackEntry> fileStack = new();
		private static readonly List<string> includedFiles = [];
		private static readonly Dictionary<string, FunctionDecl> functions = [];
		private static readonly Dictionary<string, int> references = [];
		private static StreamWriter? data_sw, text_sw;

		/*
		* 0 = Err
		* 1 = Err+Warn
		* 2 = Err+Warn+Info
		*/
		private static int logLevel = 1;
		/*
		* 0 = Header only
		* 1 = Preserve directives*
		* 2 = Directives+Linker output
		*/
		private static int commentLevel = 2;
		private static string mainPath = string.Empty;
		


		private static void Main(string[] args)
		{
			Print(programName);
			ParseArgs(args);

			try
			{
				string data_path = Path.Combine(
					Path.GetDirectoryName(mainPath) ?? "",
					(Path.GetFileNameWithoutExtension(mainPath) ?? "") + "_data" +
					(Path.GetExtension(mainPath) ?? "")
				);
				string text_path = Path.Combine(
					Path.GetDirectoryName(mainPath) ?? "",
					(Path.GetFileNameWithoutExtension(mainPath) ?? "") + "_text" +
					(Path.GetExtension(mainPath) ?? "")
				);
				string processed_path = Path.Combine(
					Path.GetDirectoryName(mainPath) ?? "",
					(Path.GetFileNameWithoutExtension(mainPath) ?? "") + "_processed" +
					(Path.GetExtension(mainPath) ?? "")
				);
				string final_path = Path.Combine(
					Path.GetDirectoryName(mainPath) ?? "",
					(Path.GetFileNameWithoutExtension(mainPath) ?? "") + "_final" +
					(Path.GetExtension(mainPath) ?? "")
				);
				if (File.Exists(data_path)) File.Delete(data_path);
				if (File.Exists(text_path)) File.Delete(text_path);
				if (File.Exists(processed_path)) File.Delete(processed_path);
				if (File.Exists(final_path)) File.Delete(final_path);

				data_sw = new(File.OpenWrite(data_path));
				text_sw = new(File.OpenWrite(text_path));

				ProcessFile(Path.GetFullPath(mainPath));

				data_sw.Close();
				text_sw.Close();

				foreach (FunctionDecl func in functions.Values)
				{
					if (!func.Complete)
					{
						ErrorMsg($"Function '{func.Name}' is not complete. Missing a '#;endfunc'?");
						Environment.Exit(-1);
					}
					func.DetermineAutos();
				}

				MergeDataText(data_path, text_path, processed_path);
				LinkFile(processed_path, final_path);
			}
			catch (Exception e)
			{
				ErrorMsg($"Unhandled exception: {e}");
				Environment.Exit(-1);
			}

			InfoMsg("Processing complete.");
			InfoMsg("Files included:");
			foreach (string file in includedFiles)
				InfoMsg("\t" + file);
			InfoMsg("Functions defined:");
			foreach (FunctionDecl decl in functions.Values)
				InfoMsg(decl.ToString());
		}

		static void ParseArgs(string[] args)
		{
			if (args.Length < 1)
			{
				PrintUsage();
				Environment.Exit(-1);
			}

			mainPath = args[args.Length-1];

			for (int i = 0; i < args.Length - 1; i++)
			{
				switch (args[i])
				{
					case "-q":
						logLevel = 0;
						break;

					case "-w":
						logLevel = 1;
						break;

					case "-l":
						logLevel = 2;
						break;

					case "-c":
						commentLevel = 0;
						break;
					
					case "-d":
						commentLevel = 1;
						break;

					case "-L":
						commentLevel = 2;
						break;

					default:
						Environment.Exit(-1);
						break;
				}
			}
		}

		static void PrintUsage()
		{
			WarnMsg("Usage: RV_Bozoer [flags] <src_path>");
			Print("Flags:");
			Print("\t -q => Only print errors.");
			Print("\t -w => Print errors and warnings.");
			Print("\t -l => Print errors, warnings and logs.");
			Print("\t -c => Clean output. Removes directives and linker comments.");
			Print("\t -d => Preserve directives.");
			Print("\t -L => Preserve directives and include linker comments.");
		}

		static void ProcessFile(string path)
		{
			if (!File.Exists(path))
			{
				ErrorMsg($"Could not find source file '{path}.'");
				PrintFileStack(path, 0);
				Environment.Exit(1);
			}
			if (includedFiles.Contains(path)) return;
			includedFiles.Add(path);

			string[] lines = File.ReadAllLines(path);
			bool data_sect = true;
			FunctionDecl? cur_func_decl = null;

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				cur_func_decl?.Lines.Add(lines[i]);
				
				if (!line.StartsWith("#;"))
				{
					if (cur_func_decl == null) (data_sect ? data_sw : text_sw)?.WriteLine(lines[i]);
					continue;
				}
				string[] split = line[2..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
				string directive = split[0];
				string[] args = split[1..];

				if (commentLevel >= 1 && directive != "sect" && directive != "include" &&
					!(cur_func_decl != null && directive == "funccall"))
					(data_sect ? data_sw : text_sw)?.WriteLine(lines[i]);

				switch (directive)
				{
					case "sect":
						if (cur_func_decl != null)
						{
							ErrorMsg($"Preprocessor directive 'sect' cannot be inside a funcdecl.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (args.Length != 1)
						{
							ErrorMsg($"Preprocessor directive 'sect' takes exactly one argument. {args.Length} provided.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (args[0] == "data")
							data_sect = true;
						else if (args[0] == "text")
							data_sect = false;
						else
						{
							ErrorMsg($"Unknown section '{args[0]}'.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						break;

					case "include":
						if (cur_func_decl != null)
						{
							ErrorMsg($"Preprocessor directive 'include' cannot be inside a funcdecl.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (args.Length != 1)
						{
							ErrorMsg($"Preprocessor directive 'include' takes exactly one argument. {args.Length} provided.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						string? local = Path.GetDirectoryName(path) ?? string.Empty;
						ProcessFile(Path.GetFullPath(Path.Combine(local, args[0])));
						break;

					case "funcdecl":
						if (cur_func_decl != null)
						{
							ErrorMsg($"Preprocessor directive 'funcdecl' cannot be inside a funcdecl.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						cur_func_decl = FunctionDecl.ParseHeader(args, path, i);
						if (cur_func_decl == null)
						{
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						functions.Add(cur_func_decl.Name, cur_func_decl);
						text_sw?.WriteLine($"#;__IMPLEMENT:{cur_func_decl.Name}");
						break;

					case "endfunc":
						if (cur_func_decl == null)
						{
							ErrorMsg($"Preprocessor directive 'endfunc' must be preceeded by a funcdecl.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (args.Length != 0)
						{
							ErrorMsg($"Preprocessor directive 'endfunc' takes exactly zero arguments. {args.Length} provided.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						cur_func_decl.FlagComplete();
						cur_func_decl = null;
						break;

					case "funccall":
						if (args.Length != 2)
						{
							ErrorMsg($"Preprocessor directive 'funccall' takes exactly two arguments. {args.Length} provided.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (args[1] == cur_func_decl?.Name)
						{
							ErrorMsg($"Inline function '{args[0]}' cannot call itself.");
							PrintFileStack(path, i);
							Environment.Exit(1);
						}
						if (cur_func_decl == null)
							text_sw?.WriteLine($"#;__CALL:{args[0]} {args[1]}");
						else
						{
							cur_func_decl.Lines.RemoveAt(cur_func_decl.Lines.Count - 1);
							cur_func_decl.Lines.Add($"#;__CALL:{args[0]} {args[1]}");
						}
						if (!references.TryAdd(args[0], 1)) references[args[0]]++;
						break;

					default:
						if (logLevel < 1) break;
						WarnMsg($"Unknwon preprocessor directive: '{directive}'");
						PrintFileStack(path, i);
						Print("Ignoring...");
						break;
				}
			}
		}

		static void MergeDataText(string data, string text, string processed)
		{
			File.Copy(data, processed);
			FileStream fr = File.OpenRead(text);
			FileStream fw = File.OpenWrite(processed); fw.Seek(0, SeekOrigin.End);
			fr.CopyTo(fw);
			fr.Close();
			fw.Close();
			File.Delete(data);
			File.Delete(text);
		}

		static void LinkFile(string processed, string final)
		{
			StreamReader sr = File.OpenText(processed);
			StreamWriter sw = new(File.OpenWrite(final));

			sw.WriteLine($"# [===Processed and 'linked' by {programName}===]");
			sw.WriteLine("# [===Included files:===]");
			foreach (string file in includedFiles)
				sw.WriteLine($"# [==={file}===]");

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine() ?? "";
				string trimmed = line.Trim();

				if (trimmed.StartsWith("#;__IMPLEMENT:"))
				{
					string func_name = trimmed[14..];
					ImplementFunc(func_name, sw);
				}
				else if (trimmed.StartsWith("#;__CALL:"))
				{
					string rem = trimmed[9..];
					string[] parts = rem.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					CallOrInline(parts[0], parts[1], sw);
				}
				else sw.WriteLine(line);
			}

			sr.Close();
			sw.Close();
			File.Delete(processed);
		}

		static void ImplementFunc(string func_name, StreamWriter sw)
		{
			if (!functions.TryGetValue(func_name, out FunctionDecl? func))
			{
				ErrorMsg($"Could not find function '{func_name}' for implementation.");
				Environment.Exit(2);
			}
			if (!references.ContainsKey(func.Name))
			{
				InfoMsg($"Skipping implementation of '{func.Name}': no references.");
				return;
			}
			if (func.ForceInline)
			{
				if (commentLevel >= 2)
					sw.WriteLine("# [===LNK: Removed due to forced inlining===]");
				return;
			}

			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: AutoImpl {func}===]");

			//Write till reached label
			bool found = false;
			int i;
			for (i = 0; i < func.Lines.Count; i++)
			{
				string line = func.Lines[i];
				sw.WriteLine(line);
				if (line.Trim().StartsWith($"{func.Name}:"))
				{
					found = true;
					break;
				}
			}
			if (!found)
			{
				ErrorMsg($"Failed to find label for function '{func.Name}'.");
				Environment.Exit(2);
			}

			//Append saving of s0-sX
			if (func.AutoSave && func.SaveCount != 0)
			{
				if (commentLevel >= 2)
				{
					if (func.SaveCount == 1)
						sw.WriteLine($"#[===LNK: AutoSave s0===]");
					else
						sw.WriteLine($"#[===LNK: AutoSave s0-s{func.SaveCount-1}===]");
				}
				sw.WriteLine($"\taddi sp, sp, -{4*func.SaveCount}");
				for (int j = 0; j < func.SaveCount; j++)
					sw.WriteLine($"\tsw s{j}, {j*4}(sp)");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoSave===]");
			}

			//Write till before 'ret'
			found = false;
			for (i++; i < func.Lines.Count; i++)
			{
				string trimmed = func.Lines[i].Trim();;
				if (trimmed.StartsWith("ret"))
				{
					found = true;
					break;
				}
				if (trimmed.StartsWith("#;__CALL:"))
				{
					string rem = trimmed[9..];
					string[] parts = rem.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					CallOrInline(parts[0], parts[1], sw);
					continue;
				}
				sw.WriteLine(func.Lines[i]);
			}
			if (!found)
			{
				ErrorMsg($"Failed to find return for function '{func.Name}'.");
				Environment.Exit(2);
			}
			
			//Append popping of s0-sX
			if (func.AutoSave && func.SaveCount != 0)
			{
				if (commentLevel >= 2)
				{
					if (func.SaveCount == 1)
						sw.WriteLine($"#[===LNK: AutoRestore s0===]");
					else
						sw.WriteLine($"#[===LNK: AutoRestore s0-s{func.SaveCount-1}===]");
				}
				for (int j = func.SaveCount - 1; j >= 0; j--)
					sw.WriteLine($"\tlw s{j}, {j*4}(sp)");
				sw.WriteLine($"\taddi sp, sp, {4*func.SaveCount}");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoRestore===]");
			}

			//Append ret and anything after
			for (; i < func.Lines.Count - 1; i++) //Skip #;endfunc
				sw.WriteLine(func.Lines[i]);

			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: End AutoImpl===]");
		}

		static void CallOrInline(string func_name, string tregs_str, StreamWriter sw)
		{
			if (!int.TryParse(tregs_str, out int tregs))
			{
				ErrorMsg($"Could not parse integer <tregs> from '{tregs_str}'.");
				Environment.Exit(1);
			}
			if (!functions.TryGetValue(func_name, out FunctionDecl? func))
			{
				ErrorMsg($"Could not find function '{func_name}' for calling.");
				Environment.Exit(2);
			}

			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: AutoCall {func.Name} (tregs={tregs})===]");

			int tsave = func.Leaf ? Math.Min(tregs, func.TempCount) : tregs;

			//Push tregs
			if (tsave > 0)
			{
				if (commentLevel >= 2)
				{
					if (tsave == 1) sw.WriteLine($"#[===LNK: AutoSave t0===]");
					else sw.WriteLine($"#[===LNK: AutoSave t0-t{tsave-1}===]");
				}
				sw.WriteLine($"\taddi sp, sp, -{4*tsave}");
				for (int j = 0; j < tsave; j++)
					sw.WriteLine($"\tsw t{j}, {j*4}(sp)");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoSave===]");
			}

			//Call or inline
			if (func.ForceInline) InlineFunc(func, sw);
			else
			{
				sw.WriteLine("\taddi sp, sp, -4");
				sw.WriteLine("\tsw ra, 0(sp)");
				sw.WriteLine($"\tcall {func.Name}");
				sw.WriteLine("\tlw ra, 0(sp)");
				sw.WriteLine("\taddi sp, sp, 4");
			}

			//Pop tregs
			if (tsave > 0)
			{
				if (commentLevel >= 2)
				{
					if (tsave == 1) sw.WriteLine($"#[===LNK: AutoRestore t0===]");
					else sw.WriteLine($"#[===LNK: AutoRestore t0-t{tsave-1}===]");
				}
				for (int j = 0; j < tsave; j++)
					sw.WriteLine($"\tsw t{j}, {j*4}(sp)");
				sw.WriteLine($"\taddi sp, sp, -{4*tsave}");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoRestore===]");
			}
		
			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: End AutoCall===]");
		}

		static void InlineFunc(FunctionDecl func, StreamWriter sw)
		{
			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: Inline {func}===]");

			//Write till reached label
			bool found = false;
			int i;
			for (i = 0; i < func.Lines.Count; i++)
			{
				string line = func.Lines[i];
				if (line.Trim().StartsWith($"{func.Name}:"))
				{
					found = true;
					break;
				}
				sw.WriteLine(line);
			}
			if (!found)
			{
				ErrorMsg($"Failed to find label for function '{func.Name}'.");
				Environment.Exit(2);
			}

			//Append saving of s0-sX
			if (func.AutoSave && func.SaveCount != 0)
			{
				if (commentLevel >= 2)
				{
					if (func.SaveCount == 1)
						sw.WriteLine($"#[===LNK: AutoSave s0===]");
					else
						sw.WriteLine($"#[===LNK: AutoSave s0-s{func.SaveCount-1}===]");
				}
				sw.WriteLine($"\taddi sp, sp, -{4*func.SaveCount}");
				for (int j = 0; j < func.SaveCount; j++)
					sw.WriteLine($"\tsw s{j}, {j*4}(sp)");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoSave===]");
			}

			//Write till before 'ret'
			found = false;
			for (i++; i < func.Lines.Count; i++)
			{
				string trimmed = func.Lines[i].Trim();;
				if (trimmed.StartsWith("ret"))
				{
					found = true;
					break;
				}
				if (trimmed.StartsWith("#;__CALL:"))
				{
					string rem = trimmed[9..];
					string[] parts = rem.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					CallOrInline(parts[0], parts[1], sw);
					continue;
				}
				sw.WriteLine(func.Lines[i]);
			}
			if (!found)
			{
				ErrorMsg($"Failed to find return for function '{func.Name}'.");
				Environment.Exit(2);
			}
			
			//Append popping of s0-sX
			if (func.AutoSave && func.SaveCount != 0)
			{
				if (commentLevel >= 2)
				{
					if (func.SaveCount == 1)
						sw.WriteLine($"#[===LNK: AutoRestore s0===]");
					else
						sw.WriteLine($"#[===LNK: AutoRestore s0-s{func.SaveCount-1}===]");
				}
				for (int j = func.SaveCount - 1; j >= 0; j--)
					sw.WriteLine($"\tlw s{j}, {j*4}(sp)");
				sw.WriteLine($"\taddi sp, sp, {4*func.SaveCount}");
				if (commentLevel >= 2)
					sw.WriteLine($"#[===LNK: End AutoRestore===]");
			}

			//Skip ret and append anything after
			for (i++; i < func.Lines.Count - 1; i++) //Skip #;endfunc
				sw.WriteLine(func.Lines[i]);

			if (commentLevel >= 2)
				sw.WriteLine($"#[===LNK: End Inline===]");
		}



		static void PrintFileStack(string filename, int line)
		{
			Print($"in '{filename}' at line {line}");
			if(fileStack.Count != 0)
				Print("included from:");
			foreach (FileStackEntry entry in fileStack)
				Print($"\tline {entry.line + 1} of '{entry.filename}'");
		}

		public static void Print(string msg)
		{
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


	readonly struct FileStackEntry(string filename, int line)
    {
		public readonly string filename = filename;
		public readonly int line = line;
    }

	class FunctionDecl(string name, 
		bool autosave, bool forceinline, bool leaf,
		int savecount, int tempcount,
		List<string> lines, string filename, int line)
	{
		public string Name { get; } = name;
		public bool AutoSave { get; } = autosave;
		public bool ForceInline { get; } = forceinline;
		public bool Leaf { get; } = leaf;
		public int SaveCount { get; private set; } = savecount;
		public int TempCount { get; private set; } = tempcount;
		public List<string> Lines { get; } = lines;
		public string Filename { get; } = filename;
		public int Line { get; } = line;
		public bool Complete { get; private set; } = false;


		public static FunctionDecl? ParseHeader(string[] parts, string filename, int line)
		{
			if (parts.Length != 6)
			{ Program.ErrorMsg($"funcdecl needs 6 arguments. {parts.Length} were found."); return null; }

			bool autosave = false;
			bool forceinline = false;
			bool leaf = false;
			int savecount, tempcount;

			if (parts[1] == "autosave") autosave = true;
			else if (parts[1] != "handsave") 
			{ Program.ErrorMsg($"funcdecl needs either 'autosave' or 'handsave'. '{parts[1]}' was found."); return null; }

			if (parts[2] == "forceinline") forceinline = true;
			else if (parts[2] != "noinline")
			{ Program.ErrorMsg($"funcdecl needs either 'forceinline' or 'noinline'. '{parts[2]}' was found."); return null; }

			if (parts[3] == "leaf") leaf = true;
			else if (parts[3] != "noleaf")
			{ Program.ErrorMsg($"funcdecl needs either 'leaf' or 'noleaf'. '{parts[3]}' was found."); return null; }

			if (parts[4] == "?") savecount = -1;
			else if (!int.TryParse(parts[4], out savecount) || savecount < 0 || savecount > 12)
			{ Program.ErrorMsg($"funcdecl needs a non-negative integer <savec> up to 12. '{parts[4]}' was found.'"); return null; }

			if (parts[5] == "?") tempcount = -1;
			else if (!int.TryParse(parts[5], out tempcount) || tempcount < 0 || tempcount > 7)
			{ Program.ErrorMsg($"funcdecl needs a non-negative integer <tregc> up to 7 . '{parts[5]}' was found."); return null; }

			return new FunctionDecl(parts[0], autosave, forceinline,
				leaf, savecount, tempcount, [], filename, line);
		}

		public void FlagComplete()
		{
			Complete = true;
		}

		public void DetermineAutos()
		{
			if (SaveCount == -1)
			{
				int lowest = 0;

				for (int i = 0; i < Lines.Count; i++)
				{
					string line = Lines[i].Split('#')[0].Trim(); //Remove comments/preprocessor and trim
					for (int j = lowest; j < 12; j++)
						if (line.Contains($"s{j}"))
							lowest = j + 1;
				}

				SaveCount = lowest;
			}

			//TODO: Determine TempCount if set to -1
			if (TempCount == -1)
			{
				int lowest = 0;

				for (int i = 0; i < Lines.Count; i++)
				{
					string line = Lines[i].Split('#')[0].Trim(); //Remove comments/preprocessor and trim
					for (int j = lowest; j < 7; j++)
						if (line.Contains($"t{j}"))
							lowest = j + 1;
				}

				TempCount = lowest;
			}
		}



        public override string ToString()
        {
            return $"{Name}: autosave={AutoSave} forceinline={ForceInline} leaf={Leaf} savec={SaveCount} tregc={TempCount} filename={Filename} line={Line + 1} line_count={Lines.Count} complete={Complete}";
		}
    }
}
