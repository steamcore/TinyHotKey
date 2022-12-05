namespace TinyHotKey;

public interface ITinyHotKey
{
	/// <summary>
	/// Register a callback to be invoke when a keyboard combination is detected.
	///
	/// Since only one application at a time may listen to a certain keyboard combination on Windows
	/// you need to check the result to see if the registration was sucessful, if it was the registration
	/// should be disposed when the registration is no longer needed.
	///
	/// Multiple registrations for the same keyboard combination with the same instance is ok.
	/// </summary>
	/// <param name="modifiers">Modifier keys, eg. Modifier.Ctrl | Modifier.Alt or Modifier.None</param>
	/// <param name="key">Key to detect, eg. Key.C or Key.MediaPlayPause</param>
	/// <param name="callback">Callback to be invoked when the keyboard combination is detected</param>
	/// <returns>Registration status, check IsRegistered to see if the registration was successful, dispose when done to remove the hotkey detection.</returns>
	ITinyHotKeyRegistration RegisterHotKey(Modifier modifiers, Key key, Func<Task> callback);
}

public static class TinyHotKeyExtensions
{
	/// <summary>
	/// Register a callback to be invoke when a keyboard combination is detected.
	///
	/// Since only one application at a time may listen to a certain keyboard combination on Windows
	/// this method will throw an error if the registration was not a success.
	///
	/// Multiple registrations for the same keyboard combination with the same instance is ok.
	/// </summary>
	/// <param name="tinyHotKey">A TinyHotKey instance</param>
	/// <param name="modifiers">Modifier keys, eg. Modifier.Ctrl | Modifier.Alt or Modifier.None</param>
	/// <param name="key">Key to detect, eg. Key.C or Key.MediaPlayPause</param>
	/// <param name="callback">Callback to be invoked when the keyboard combination is detected</param>
	/// <returns>Registration status, should always be successful with this overload, dispose when done to remove the hotkey detection.</returns>
	/// <exception cref="InvalidOperationException">Thrown when the hotkey combination could not be registered.</exception>
	public static ITinyHotKeyRegistration RegisterHotKeyOrThrow(this ITinyHotKey tinyHotKey, Modifier modifiers, Key key, Func<Task> callback)
	{
		var registration = tinyHotKey.RegisterHotKey(modifiers, key, callback);

		if (!registration.IsRegistered)
		{
			throw new InvalidOperationException($"Couldn't register hotkey {modifiers} {key}");
		}

		return registration;
	}
}
