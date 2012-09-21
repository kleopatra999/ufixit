using Mono.Cecil;

namespace UnityVS
{
	abstract class RewriteStep
	{
		protected readonly ModuleDefinition _module;

		protected RewriteStep(ModuleDefinition module)
		{
			_module = module;
		}

		public abstract bool IsNecessary();
		public abstract void Process();
	}
}