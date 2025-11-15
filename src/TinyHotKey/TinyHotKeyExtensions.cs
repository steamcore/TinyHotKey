namespace TinyHotKey;

public static class TinyHotKeyExtensions
{
	extension(ITinyHotKey tinyHotKey)
	{
		/// <summary>
		/// <para>Register a callback to be invoke when a keyboard combination is detected.</para>
		/// <para>
		/// Since only one application at a time may listen to a certain keyboard combination on Windows
		/// this method will throw an error if the registration was not a success.
		/// </para>
		/// <para>Multiple registrations for the same keyboard combination with the same instance is ok.</para>
		/// </summary>
		/// <param name="modifiers">Modifier keys, eg. Modifier.Ctrl | Modifier.Alt or Modifier.None</param>
		/// <param name="key">Key to detect, eg. Key.C or Key.MediaPlayPause</param>
		/// <param name="callback">Callback to be invoked when the keyboard combination is detected</param>
		/// <returns>Registration status, should always be successful with this overload, dispose when done to remove the hotkey detection.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the hotkey combination could not be registered.</exception>
		public ITinyHotKeyRegistration RegisterHotKeyOrThrow(Modifier modifiers, Key key, Func<Task> callback)
		{
			ArgumentNullException.ThrowIfNull(tinyHotKey);

			var registration = tinyHotKey.RegisterHotKey(modifiers, key, callback);

			if (!registration.IsRegistered)
			{
				throw new InvalidOperationException($"Couldn't register hotkey {modifiers} {key}");
			}

			return registration;
		}
	}
}
