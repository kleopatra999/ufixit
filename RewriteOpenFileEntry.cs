using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using ParameterAttributes = Mono.Cecil.ParameterAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace UnityVS
{
	class RewriteOpenFileEntry : RewriteStep
	{
		private TypeDefinition _logEntries;
		private TypeDefinition _logEntry;
		private TypeDefinition _console;

		private MethodDefinition _openInUvs;

		public RewriteOpenFileEntry(ModuleDefinition module) : base(module)
		{
		}

		public override bool IsNecessary()
		{
			return _module.GetType("UnityVS.OpenFile.Marker") == null;
		}

		public override void Process()
		{
			_logEntries = _module.GetType("UnityEditorInternal.LogEntries");
			_logEntry = _module.GetType("UnityEditorInternal.LogEntry");
			_console = _module.GetType("UnityEditor.ConsoleWindow");

			if (_logEntries == null || _logEntry == null || _console == null)
				throw new InvalidOperationException();

			CreateOpenInUvsMethod();
			PatchConsoleOnGui();
			MarkAssembly();
		}

		private void CreateOpenInUvsMethod()
		{
			_openInUvs = new MethodDefinition("OpenEntryFileInUnityVS", MethodAttributes.Public | MethodAttributes.Static, _module.TypeSystem.Void);
			_openInUvs.Parameters.Add(new ParameterDefinition("row", ParameterAttributes.None, _module.TypeSystem.Int32));

			_logEntries.Methods.Add(_openInUvs);

			var il = _openInUvs.Body.GetILProcessor();
			_openInUvs.Body.InitLocals = true;

			_openInUvs.Body.Variables.Add(new VariableDefinition("entry", _logEntry));
			_openInUvs.Body.Variables.Add(new VariableDefinition("scriptTool", _module.TypeSystem.String));

			// var scriptTool = EditorPrefs.GetString("kScriptsDefaultApp");

			il.Emit(OpCodes.Ldstr, "kScriptsDefaultApp");
			il.Emit(OpCodes.Call, _module.GetType("UnityEditor.EditorPrefs").Methods.Single(m => m.Name == "GetString" && m.Parameters.Count == 1));
			il.Emit(OpCodes.Stloc_1);

			// if (!scriptTool.EndsWith("UnityVS.OpenFile.exe")) {
			//     OpenEntryFile(row);
			//     return;
			// }

			var useOpenFile = Instruction.Create(OpCodes.Nop);

			il.Emit(OpCodes.Ldloc_1);
			il.Emit(OpCodes.Ldstr, "UnityVS.OpenFile.exe");
			il.Emit(OpCodes.Callvirt, EndsWithMethod());
			il.Emit(OpCodes.Brtrue, useOpenFile);

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, _logEntries.Methods.Single(m => m.Name == "OpenEntryFile"));
			il.Emit(OpCodes.Ret);

			il.Append(useOpenFile);

			// var entry = new LogEntry();

			il.Emit(OpCodes.Newobj, _logEntry.Methods.Single(m => m.IsConstructor && m.Parameters.Count == 0));
			il.Emit(OpCodes.Stloc_0);

			var entryFound = Instruction.Create(OpCodes.Nop);

			// if (!GetEntryInternal(row, entry))
			//     return;

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Call, _logEntries.Methods.Single(m => m.Name == "GetEntryInternal"));
			il.Emit(OpCodes.Brtrue, entryFound);

			il.Emit(OpCodes.Ret);

			il.Append(entryFound);

			// UnityVS.Opener.OpenFile(scriptTool, entry.file, entry.line);

			il.Emit(OpCodes.Ldloc_1);
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ldfld, _logEntry.Fields.Single(f => f.Name == "file"));
			il.Emit(OpCodes.Ldloc_0);
			il.Emit(OpCodes.Ldfld, _logEntry.Fields.Single(f => f.Name == "line"));
			il.Emit(OpCodes.Call, OpenFileMethod());
			il.Emit(OpCodes.Ret);
		}

		private MethodReference EndsWithMethod()
		{
			var method = new MethodReference("EndsWith", _module.TypeSystem.Boolean, _module.TypeSystem.String) { HasThis = true };
			method.Parameters.Add(new ParameterDefinition(_module.TypeSystem.String));
			return method;
		}

		private void PatchConsoleOnGui()
		{
			var onGui = _console.Methods.Single(m => m.Name == "OnGUI");
			if (onGui == null)
				throw new InvalidOperationException();

			var instructions = onGui.Body.Instructions
				.Where(IsCallToOpenEntryFile);

			foreach (var instruction in instructions)
				instruction.Operand = _openInUvs;
		}

		private bool IsCallToOpenEntryFile(Instruction instruction)
		{
			if (instruction.OpCode != OpCodes.Call)
				return false;

			var method = instruction.Operand as MethodReference;
			if (method == null)
				return false;

			return method.Name == "OpenEntryFile" && method.DeclaringType == _logEntries;
		}

		private MethodReference OpenFileMethod()
		{
			return _module.Import(UvsOpenerModule().GetType("UnityVS.Opener").Methods.Single(m => m.Name == "OpenFile"));
		}

		private static ModuleDefinition UvsOpenerModule()
		{
			var memory = new MemoryStream();
			Assembly.GetExecutingAssembly().GetManifestResourceStream(Patcher.SupportAssemblyName).WriteTo(memory);

			return ModuleDefinition.ReadModule(new MemoryStream(memory.ToArray()));
		}

		private void MarkAssembly()
		{
			var marker = new TypeDefinition(
				"UnityVS.OpenFile", "Marker",
				TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed,
				_module.TypeSystem.Object);

			_module.Types.Add(marker);
		}
	}
}