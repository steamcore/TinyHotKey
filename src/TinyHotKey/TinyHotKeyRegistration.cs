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
	public static ITinyHotKeyRegistration Instance = new FailedTinyHotKeyRegistration();

	public bool IsRegistered { get; }

	private FailedTinyHotKeyRegistration()
	{
	}

	public void Dispose()
	{
	}
}

public sealed class TinyHotKeyRegistration : ITinyHotKeyRegistration
{
	public nuint Id { get; }
	public Modifier Modifiers { get; }
	public Key Key { get; }
	public Func<Task> Callback { get; }
	public bool IsRegistered { get; } = true;

	private readonly Action<TinyHotKeyRegistration> unregister;

	private bool disposed;

	public TinyHotKeyRegistration(nuint id, Modifier modifiers, Key key, Func<Task> callback, Action<TinyHotKeyRegistration> unregister)
	{
		Id = id;
		Modifiers = modifiers;
		Key = key;
		Callback = callback;

		this.unregister = unregister;
	}

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
