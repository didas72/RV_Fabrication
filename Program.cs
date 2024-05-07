using System;
using System.IO;
using System.Collections.Generic;

namespace RV_Bozoer
{
	static class Program
	{
		private static readonly Stack<FileStackEntry> fileStack = new();
		private static readonly List<string> includedFiles = [];
		private static readonly Dictionary<string, FunctionDecl> functions = [];
		private static StreamWriter? data_sw, text_sw;


		private static void Main(string[] args)
		{
			InfoMsg($"RV_Bozoer v0.1 by Didas72");

			if (args.Length != 1)
			{
				WarnMsg("Usage: RV_Bozoer <src_path>");
				Environment.Exit(1);
			}

			try
			{
				string data_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					(Path.GetFileNameWithoutExtension(args[0]) ?? "") + "_data" +
					(Path.GetExtension(args[0]) ?? "")
				);
				string text_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					(Path.GetFileNameWithoutExtension(args[0]) ?? "") + "_text" +
					(Path.GetExtension(args[0]) ?? "")
				);
				string processed_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					(Path.GetFileNameWithoutExtension(args[0]) ?? "") + "_processed" +
					(Path.GetExtension(args[0]) ?? "")
				);
				string final_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					(Path.GetFileNameWithoutExtension(args[0]) ?? "") + "_final" +
					(Path.GetExtension(args[0]) ?? "")
				);
				if (File.Exists(data_path)) File.Delete(data_path);
				if (File.Exists(text_path)) File.Delete(text_path);
				if (File.Exists(processed_path)) File.Delete(processed_path);
				if (File.Exists(final_path)) File.Delete(final_path);

				data_sw = new(File.OpenWrite(data_path));
				text_sw = new(File.OpenWrite(text_path));

				ProcessFile(Path.GetFullPath(args[0]));

				//TODO: Check all functions complete

				data_sw.Close();
				text_sw.Close();

				MergeDataText(data_path, text_path, processed_path);
				LinkFile(processed_path, final_path);
			}
			catch (Exception e)
			{
				ErrorMsg($"Unhandled exception: {e}");
				Environment.Exit(1);
			}

			InfoMsg("Processing complete.");
			InfoMsg("Files included:");
			foreach (string file in includedFiles)
				InfoMsg("\t" + file);
			InfoMsg("Functions defined:");
			foreach (FunctionDecl decl in functions.Values)
				InfoMsg(decl.ToString());
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

				if (directive != "sect")
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

					default:
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

			sw.WriteLine("# ==Processed and 'linked' by RV_Bozoer v0.1 by Didas72==");

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine() ?? "";
				string trimmed = line.Trim();

				if (trimmed.StartsWith("#;__IMPLEMENT:"))
				{
					string func_name = trimmed[14..];
					ImplementFunc(func_name, sw);
				}
				else if (trimmed.StartsWith("#;__INLINE:"))
				{
					string func_name = trimmed[11..];
					InlineFunc(func_name, sw);
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
			if (func.References == 0)
			{
				InfoMsg($"Skipping implementation of '{func_name}': no references.");
				return;
			}

			sw.WriteLine($"#[===LNK: AUTOIMPL {func}===]");

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
				if (func.SaveCount == 1)
					sw.WriteLine($"#[===LNK: Autosave s0===]");
				else
					sw.WriteLine($"#[===LNK: Autosave s0-s{func.SaveCount-1}===]");
				sw.WriteLine($"\taddi sp, sp, -{4*func.SaveCount}");
				for (int j = 0; j < func.SaveCount; j++)
					sw.WriteLine($"\tsw s{j}, {j*4}(sp)");
				sw.WriteLine($"#[===LNK: End autosave===]");
			}

			//Write till before 'ret'
			found = false;
			for (i++; i < func.Lines.Count; i++)
			{
				string line = func.Lines[i];
				if (line.Trim().StartsWith($"ret"))
				{
					found = true;
					break;
				}
				sw.WriteLine(line);
			}
			if (!found)
			{
				ErrorMsg($"Failed to find return for function '{func.Name}'.");
				Environment.Exit(2);
			}
			
			//Append popping of s0-sX
			if (func.AutoSave && func.SaveCount != 0)
			{
				if (func.SaveCount == 1)
					sw.WriteLine($"#[===LNK: Autorestore s0===]");
				else
					sw.WriteLine($"#[===LNK: Autorestore s0-s{func.SaveCount-1}===]");
				for (int j = func.SaveCount - 1; j >= 0; j--)
					sw.WriteLine($"\tlw s{j}, {j*4}(sp)");
				sw.WriteLine($"\taddi sp, sp, {4*func.SaveCount}");
				sw.WriteLine($"#[===LNK: End autorestore===]");
			}

			//Append ret and anything after
			for (; i < func.Lines.Count - 1; i++) //Skip #;endfunc
				sw.WriteLine(func.Lines[i]);
		}

		static void InlineFunc(string func_name, StreamWriter sw)
		{
			if (!functions.TryGetValue(func_name, out FunctionDecl? func))
			{
				ErrorMsg($"Could not find function '{func_name}' for inlining.");
				Environment.Exit(2);
			}

			//TODO: Finish implementation
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
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(msg);
		}

		public static void WarnMsg(string msg)
		{
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
		public int SaveCount { get; } = savecount;
		public int TempCount { get; } = tempcount;
		public List<string> Lines { get; } = lines;
		public string Filename { get; } = filename;
		public int Line { get; } = line;
		public bool Complete { get; private set; } = false;
		public int References { get; private set; } = 0;


		public static FunctionDecl? ParseHeader(string[] parts, string filename, int line)
		{
			if (parts.Length != 6)
			{ Program.ErrorMsg($"funcdecl needs 6 arguments. {parts.Length} were found."); return null; }

			bool autosave = false;
			bool forceinline = false;
			bool leaf = false;

			if (parts[1] == "autosave") autosave = true;
			else if (parts[1] != "handsave") 
			{ Program.ErrorMsg($"funcdecl needs either 'autosave' or 'handsave'. '{parts[1]}' was found."); return null; }

			if (parts[2] == "forceinline") forceinline = true;
			else if (parts[2] != "noinline")
			{ Program.ErrorMsg($"funcdecl needs either 'forceinline' or 'noinline'. '{parts[2]}' was found."); return null; }

			if (parts[3] == "leaf") leaf = true;
			else if (parts[3] != "noleaf")
			{ Program.ErrorMsg($"funcdecl needs either 'leaf' or 'noleaf'. '{parts[3]}' was found."); return null; }

			if (!int.TryParse(parts[4], out int savecount))
			{ Program.ErrorMsg($"funcdecl needs a non-negative integer <savec>. '{parts[4]}' was found.'"); return null; }

			if (!int.TryParse(parts[5], out int tempcount))
			{ Program.ErrorMsg($"funcdecl needs a non-negative integer <tregc>. '{parts[5]}' was found."); return null; }

			//TODO: Check savecount and tempcount

			return new FunctionDecl(parts[0], autosave, forceinline,
				leaf, savecount, tempcount, [], filename, line);
		}

		public void FlagComplete()
		{
			Complete = true;
		}

		public void IncrementRefs()
		{
			References++;
		}



        public override string ToString()
        {
            return $"{Name}: autosave={AutoSave} forceinline={ForceInline} leaf={Leaf} savec={SaveCount} tregc={TempCount} filename={Filename} line={Line + 1} line_count={Lines.Count} complete={Complete} references={References}";        }
    }
}
