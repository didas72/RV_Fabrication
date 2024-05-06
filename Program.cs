using System;
using System.IO;
using System.Collections.Generic;

namespace RV_Bozoer
{
	static class Program
	{
		private static readonly Stack<FileStackEntry> fileStack = new();
		private static readonly List<string> includedFiles = [];
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
					Path.GetFileNameWithoutExtension(args[0]) ?? "" + "_data" +
					Path.GetExtension(args[0]) ?? ""
				);
				string text_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					Path.GetFileNameWithoutExtension(args[0]) ?? "" + "_text" +
					Path.GetExtension(args[0]) ?? ""
				);
				string final_path = Path.Combine(
					Path.GetDirectoryName(args[0]) ?? "",
					Path.GetFileNameWithoutExtension(args[0]) ?? "" + "_processed" +
					Path.GetExtension(args[0]) ?? ""
				);
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

			for (int i = 0; i < lines.Length; i++)
			{
				(data_sect ? data_sw : text_sw)?.WriteLine(lines[i]);

				string line = lines[i].Trim();
				
				if (!line.StartsWith("#;")) continue;
				string directive = line[2..].Split(' ')[0];
				string[] args = line[2..].Split(' ')[1..];

				switch (directive)
				{
					case "include":
						string? local = Path.GetDirectoryName(path) ?? string.Empty;
						ProcessFile(Path.GetFullPath(Path.Combine(local, args[0])));
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

		static void Print(string msg)
		{
			Console.WriteLine(msg);
		}

		static void InfoMsg(string msg)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(msg);
		}

		static void WarnMsg(string msg)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(msg);
		}

		static void ErrorMsg(string msg)
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
}
