using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UnityVS
{
	class Program
	{
		private static void Main()
		{
			try
			{
				Run();
			}
			catch (Exception e)
			{
				Console.WriteLine("Fatal exception: {0}", e);
			}
		}

		private static void Run()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
			{
				var name = new AssemblyName(args.Name);
				var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name.Name);
				if (stream == null)
					return null;

				var memory = new MemoryStream((int) stream.Length);
				stream.WriteTo(memory);
				return Assembly.Load(memory.ToArray());
			};

			var unityLocation = UnityLocation();

			while (!File.Exists(UnityEditorFor(unityLocation)))
			{
				Console.Write("Input Unity location: ");
				unityLocation = Console.ReadLine();
			}

			var patcher = Patcher.For(UnityEditorFor(unityLocation));

			Console.WriteLine("List of commands: ");

			if (patcher.HasBackup)
				Console.WriteLine("  restore            Restore the backup");
			if (patcher.ModuleCanBePatched())
				Console.WriteLine("  patch              Patch Unity");

			Console.WriteLine("  quit               Exit");

			Console.WriteLine();
			Console.Write("Input: ");
			switch (Console.ReadLine().ToLowerInvariant())
			{
				case "restore":
					patcher.RestoreBackup();
					break;
				case "patch":
					patcher.Patch();
					break;
				case "quit":
					break;
				default:
					Console.WriteLine("Unknown command");
					break;
			}
		}

		private static string UnityEditorFor(string unityLocation)
		{
			return PathCombine(unityLocation, "Editor", "Data", "Managed", "UnityEditor.dll");
		}

		private static string UnityLocation()
		{
			return PathCombine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity");
		}

		private static string PathCombine(params string[] paths)
		{
			return paths.Aggregate(Path.Combine);
		}
	}
}
