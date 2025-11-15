using TinyHotKey;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// <para>Add a TinyHotKey singleton instance to the service collection.</para>
		/// <para>Resolve ITinyHotKey and call RegisterHotKey to use hotkey detection.</para>
		/// </summary>
		public IServiceCollection AddTinyHotKey()
		{
			return services.AddSingleton<ITinyHotKey, TinyHotKeyInstance>();
		}
	}
}
