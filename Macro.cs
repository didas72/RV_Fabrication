using System.Collections.Generic;

namespace RV_Fabrication
{
	internal class Macro(string name, string[] args)
	{
		public string Name { get; } = name;
		public string[] Args { get; } = args;
		public List<string> Lines { get; } = [];
	}
}
