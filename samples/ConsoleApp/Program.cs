using TinyHotKey;

namespace ConsoleApp;

public class Program
{
	public static void Main()
	{
		// Create a new instance of TinyHotKey, this contains the hidden Window and the message loop.
		// Dispose of it when the application is shutting down.
		using var tinyHotKey = new TinyHotKeyInstance();

		// Register a hotkey, this returns a registration object that can be used to unregister the hotkey.
		// Dispose of it when the hotkey is no longer needed.
		using var registration = tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
		{
			Console.WriteLine("Ctrl+Alt+D detected");

			return Task.CompletedTask;
		});

		// Hotkey registration can fail if the hotkey is already registered by another application.
		if (!registration.IsRegistered)
		{
			Console.WriteLine("Couldn't register hotkey Ctrl+Alt+D, maybe it's already taken by something else?");

			return;
		}

		Console.WriteLine("Hello HotKey, press Ctrl+Alt+D anywhere or enter here to quit");

		Console.ReadLine();
	}
}
