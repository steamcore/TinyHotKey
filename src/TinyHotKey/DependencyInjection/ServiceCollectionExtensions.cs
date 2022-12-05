using TinyHotKey;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Add a TinyHotKey singleton instance to the service collection.
	///
	/// Resolve ITinyHotKey and call RegisterHotKey to use hotkey detection.
	/// </summary>
	public static IServiceCollection AddTinyHotKeys(this IServiceCollection services)
	{
		return services.AddSingleton<ITinyHotKey, TinyHotKeyInstance>();
	}
}
