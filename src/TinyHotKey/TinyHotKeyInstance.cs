using Microsoft.Extensions.Logging;

namespace TinyHotKey;

public sealed class TinyHotKeyInstance : ITinyHotKey, IDisposable
{
	private readonly ITinyHotKey platformInstance;

	private bool disposed;

	public TinyHotKeyInstance()
		: this(null)
	{
	}

	public TinyHotKeyInstance(ILoggerFactory? loggerFactory)
	{
#if NET
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Operating system not supported");
		}
#endif

		platformInstance = new TinyHotKeyWindows(loggerFactory?.CreateLogger("TinyHotKey"));
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		if (platformInstance is IDisposable disposableInstance)
		{
			disposableInstance.Dispose();
		}

		disposed = true;
	}

	public ITinyHotKeyRegistration RegisterHotKey(Modifier modifiers, Key key, Func<Task> callback)
	{
		return platformInstance.RegisterHotKey(modifiers, key, callback);
	}
}
