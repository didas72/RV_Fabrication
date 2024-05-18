using System.Collections.Generic;

using static RV_Fabrication.Function;

namespace RV_Fabrication
{
	internal class Function(string name, int argCount, InlineHint inlineHint)
	{
		public enum InlineHint
		{
			AutoInline = 0,
			AgressiveInline = 1,
			NoInline = 2,
		}

		public string Name { get; } = name;
		public int ArgCount { get; } = argCount;
		public InlineHint InlineOption { get; } = inlineHint;
		public List<string> Lines { get; } = [];
		public int References { get; set; } = 0;
	}
}
