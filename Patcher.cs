using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Mdb;

namespace UnityVS
{
	class Patcher
	{
		private readonly string _unityEditor;
		private readonly ModuleDefinition _module;
		private readonly RewriteStep _rewriteStep;

		public const string SupportAssemblyName = "UnityVS.Opener.dll";

		public bool HasSymbols
		{
			get { return File.Exists(SymbolsFile()); }
		}

		public bool HasBackup
		{
			get { return File.Exists(BackupFile()); }
		}

		private string SymbolsFile()
		{
			return _unityEditor + ".mdb";
		}

		private string BackupFile()
		{
			return _unityEditor + ".backup";
		}

		private string BackupSymbolsFile()
		{
			return SymbolsFile() + ".backup";
		}

		private Patcher(string unityEditor)
		{
			_unityEditor = unityEditor;
			_module = ModuleDefinition.ReadModule(_unityEditor, HasSymbols
				? new ReaderParameters { SymbolReaderProvider = new MdbReaderProvider() }
				: new ReaderParameters());
			_rewriteStep = new RewriteOpenFileEntry(_module);
		}

		public bool ModuleCanBePatched()
		{
			return _rewriteStep.IsNecessary();
		}

		public void Patch()
		{
			if (!ModuleCanBePatched())
				return;

			if (!HasBackup)
				MakeBackup();

			PatchModule();

			_module.Write(_unityEditor, HasSymbols
				? new WriterParameters { SymbolWriterProvider = new MdbWriterProvider() }
				: new WriterParameters());

			MoveSupportAssembly();
		}

		private void PatchModule()
		{
			new RewriteOpenFileEntry(_module).Process();
		}

		private void MakeBackup()
		{
			if (HasBackup)
				return;

			File.Copy(_unityEditor, BackupFile(), overwrite: true);

			if (HasSymbols)
				File.Copy(SymbolsFile(), BackupSymbolsFile(), overwrite: true);
		}

		public void RestoreBackup()
		{
			if (!HasBackup)
				return;

			File.Copy(BackupFile(), _unityEditor, overwrite: true);
			File.Delete(BackupFile());

			if (File.Exists(BackupSymbolsFile()))
			{
				File.Copy(BackupSymbolsFile(), SymbolsFile(), overwrite: true);
				File.Delete(BackupSymbolsFile());
			}

			var supportAssembly = SupportAssembly();
			if (File.Exists(supportAssembly))
				File.Delete(SupportAssembly());
		}

		private void MoveSupportAssembly()
		{
			var supportAssembly = SupportAssembly();
			if (File.Exists(supportAssembly))
				File.Delete(supportAssembly);

			using (var file = new FileStream(supportAssembly, FileMode.Create, FileAccess.Write))
				Assembly.GetExecutingAssembly().GetManifestResourceStream(SupportAssemblyName).WriteTo(file);
		}

		private string SupportAssembly()
		{
			return Path.Combine(Path.GetDirectoryName(_unityEditor), SupportAssemblyName);
		}

		public static Patcher For(string unityLocation)
		{
			return new Patcher(unityLocation);
		}
	}
}
