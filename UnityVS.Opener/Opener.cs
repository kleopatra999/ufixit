using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using UnityEngine;

namespace UnityVS
{
	public static class Opener
	{
		public static void OpenFile(string openFile, string file, int line)
		{
			if (!File.Exists(FullPathTo(file)))
				return;

			Process.Start(new ProcessStartInfo
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				Arguments = QuoteIfNeeded(FullPathTo(file)) + " " + (line - 1).ToString(CultureInfo.InvariantCulture),
				FileName = NormalizePath(openFile),
			});
		}

		private static string FullPathTo(string file)
		{
			return Path.GetFullPath(PathCombine(ProjectDirectory(), NormalizePath(file)));
		}

		private static string NormalizePath(string path)
		{
			return path.Replace('/', '\\');
		}

		public static string ProjectDirectory()
		{
			return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		}

		private static string PathCombine(params string[] paths)
		{
			return paths.Aggregate(Path.Combine);
		}

		public static string QuoteIfNeeded(this string str)
		{
			return str.Contains(" ") ? "\"" + str + "\"" : str;
		}
	}
}
