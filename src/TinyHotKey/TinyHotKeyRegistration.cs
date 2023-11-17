namespace TinyHotKey;

public interface ITinyHotKeyRegistration : IDisposable
{
	/// <summary>
	/// Indicates if the hotkey registration was successful, if false then no hotkeys will be detected and the callback will not be invoked.
	/// </summary>
	bool IsRegistered { get; }
}

public sealed class FailedTinyHotKeyRegistration : ITinyHotKeyRegistration
{
	public static ITinyHotKeyRegistration Instance { get; } = new FailedTinyHotKeyRegistration();

	public bool IsRegistered { get; }

	private FailedTinyHotKeyRegistration()
	{
	}

	public void Dispose()
	{
	}
}

public sealed class TinyHotKeyRegistration(
	nuint id,
	Modifier modifiers,
	Key key,
	Func<Task> callback,
	Action<TinyHotKeyRegistration> unregister
)
	: ITinyHotKeyRegistration
{
	public nuint Id { get; } = id;
	public Modifier Modifiers { get; } = modifiers;
	public Key Key { get; } = key;
	public Func<Task> Callback { get; } = callback;
	public bool IsRegistered { get; } = true;

	private bool disposed;

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		unregister(this);

		disposed = true;
	}
}
