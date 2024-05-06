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
				string final_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					(Path.GetFileNameWithoutExtension(args[0]) ?? "") + "_processed" +
					(Path.GetExtension(args[0]) ?? "")
				);
				if (File.Exists(data_path)) File.Delete(data_path);
				if (File.Exists(text_path)) File.Delete(text_path);
				if (File.Exists(final_path)) File.Delete(final_path);
				data_sw = new(File.OpenWrite(data_path));
				text_sw = new(File.OpenWrite(text_path));

				ProcessFile(Path.GetFullPath(args[0]));

				data_sw.Close();
				text_sw.Close();

				//Write data
				File.Copy(data_path, final_path);
				//Append text
				FileStream fr = File.OpenRead(text_path);
				FileStream fw = File.OpenWrite(final_path); fw.Seek(0, SeekOrigin.End);
				fr.CopyTo(fw);
				fr.Close();
				fw.Close();
				File.Delete(data_path);
				File.Delete(text_path);
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
					(data_sect ? data_sw : text_sw)?.WriteLine(lines[i]);
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


		static void PrintFileStack(string filename, int line)
		{
			Print($"in '{filename}' at line {line}");
			if(fileStack.Count != 0)
				Print("included from:");
			foreach (FileStackEntry entry in fileStack)
				Print($"\tline {entry.line} of '{entry.filename}'");
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

			return new FunctionDecl(parts[0], autosave, forceinline,
				leaf, savecount, tempcount, [], filename, line);
		}


        public override string ToString()
        {
            return $"{Name}: autosave={AutoSave} forceinline={ForceInline} leaf={Leaf} savec={SaveCount} tregc={TempCount} filename={Filename} line={Line} line_count={Lines.Count}";
        }
    }
}
