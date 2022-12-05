using TinyHotKey;

namespace ConsoleApp;

public class Program
{
	public static void Main()
	{
		using var tinyHotKey = new TinyHotKeyInstance();

		using var binding = tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
		{
			Console.WriteLine("Ctrl+Alt+D detected");

			return Task.CompletedTask;
		});

		if (!binding.IsRegistered)
		{
			Console.WriteLine("Couldn't register hotkey Ctrl+Alt+D, maybe it's already taken by something else?");

			return;
		}

		Console.WriteLine("Hello HotKey, press Ctrl+Alt+D anywhere or enter here to quit");

		Console.ReadLine();
	}
}
