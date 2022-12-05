using System.Diagnostics;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.Logging;

namespace TinyHotKey;

public delegate nint WndProc(IntPtr hWnd, uint msg, nuint wParam, nint lParam);

/// <summary>
/// Hotkey detection on Windows using RegisterHotKey by P/Invoke in the Win32 API
/// which only works thanks to a hidden Window and a custom message loop on a
/// dedicated thread.
/// </summary>
#if NET
[SupportedOSPlatform("windows")]
#endif
internal sealed partial class TinyHotKeyWindows : ITinyHotKey, IDisposable
{
	private readonly string className = Guid.NewGuid().ToString("n");
	private readonly AutoResetEvent messageLoopDone = new(false);
	private readonly ILogger? logger;
	private readonly object registrationLock = new();
	private readonly List<TinyHotKeyRegistration> registrations = new();

	private ushort atom;
	private nint hWnd;
	private int lastError;

	private bool disposed;
	private nuint hotKeyId = 1_000;

	public TinyHotKeyWindows()
		: this(null)
	{
	}

	public TinyHotKeyWindows(ILogger? logger)
	{
		this.logger = logger;

		// Start the message loop in its own dedicated thread, this is where all the magic happens
		Task.Run(MessageLoop);

		// Wait for the message loop to signal that it is ready, if the signal does not happen we can't continue
		if (!messageLoopDone.WaitOne(TimeSpan.FromSeconds(5)))
		{
			throw new InvalidOperationException("Message loop failed to initialize");
		}

		// If RegisterClassEx failed we can't create a Window to receive messages and can't continue
		if (atom == 0)
		{
			throw new InvalidOperationException($"{nameof(NativeMethods.RegisterClassEx)} failed with error code {lastError}");
		}

		// If CreateWindowEx failed we have nowhere to receive messages and can't continue
		if (hWnd == 0)
		{
			throw new InvalidOperationException($"{nameof(NativeMethods.CreateWindowEx)} failed with error code {lastError}");
		}

		// Reset the signal so it can be used again when it's time to dispose
		messageLoopDone.Reset();
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		// Since disposal modifies the registrations list we have to make a copy first
		var registrationsToDispose = Array.Empty<TinyHotKeyRegistration>();

		lock (registrationLock)
		{
			registrationsToDispose = registrations.ToArray();
		}

		// Dispose any left over registrations
		foreach (var registration in registrationsToDispose)
		{
			registration.Dispose();
		}

		// Signal the window to close so the message loop will post the quit message and exit
		NativeMethods.SendMessage(hWnd, NativeMessage.WM_CLOSE, 0, 0);

		// Wait for the message loop to signal that it is done before moving on
		if (!messageLoopDone.WaitOne(TimeSpan.FromSeconds(5)))
		{
			throw new InvalidOperationException("Message loop failed to exit");
		}

		disposed = true;
	}

	public ITinyHotKeyRegistration RegisterHotKey(Modifier modifiers, Key key, Func<Task> callback)
	{
		lock (registrationLock)
		{
			// First try to find an existing registration for this keyboard combination
			var previousRegistration = registrations.Find(r => r.Modifiers == modifiers && r.Key == key);

			if (previousRegistration is not null)
			{
				// If an existing keyboard combination is found the hotkey is already registered and can just
				// add another callback to the registration list.

				var registration = new TinyHotKeyRegistration(previousRegistration.Id, modifiers, key, callback, Unregister);
				registrations.Add(registration);

				if (logger is not null)
				{
					LogAddedCallback(logger, modifiers, key);
				}

				return registration;
			}
			else
			{
				// If no registration was found we must first register the hotkey

				var id = hotKeyId;

				// Send message to the message loop to register the hotkey
				var lParam = GetHotKeyParam(modifiers, key);
				if (NativeMethods.SendMessage(hWnd, NativeMessage.WM_APP_REGISTER_HOTKEY, id, lParam) != 0)
				{
					if (logger is not null)
					{
						LogRegisterHotkeyFailed(logger, modifiers, key, lastError);
					}

					return FailedTinyHotKeyRegistration.Instance;
				}

				if (logger is not null)
				{
					LogRegisteredHotkey(logger, modifiers, key);
				}

				// Success, increment the hotKeyId so the next registration will have a new id
				hotKeyId++;

				// Add a registration to the list
				var registration = new TinyHotKeyRegistration(id, modifiers, key, callback, Unregister);
				registrations.Add(registration);

				if (logger is not null)
				{
					LogAddedCallback(logger, modifiers, key);
				}

				return registration;
			}
		}

		void Unregister(TinyHotKeyRegistration registration)
		{
			lock (registrationLock)
			{
				// Remove it from the list
				registrations.Remove(registration);

				// If other registrations use the same keyboard combination we are done here
				if (registrations.Any(x => x.Modifiers == modifiers && x.Key == key))
				{
					return;
				}

				// Send a message to the message loop to remove the hotkey registration
				if (NativeMethods.SendMessage(hWnd, NativeMessage.WM_APP_UNREGISTER_HOTKEY, registration.Id, 0) == 0)
				{
					if (logger is not null)
					{
						LogUnregisteredHotkey(logger, registration.Modifiers, registration.Key);
					}

					return;
				}

				if (logger is not null)
				{
					LogUnregisterHotkeyFailed(logger, registration.Modifiers, registration.Key, lastError);
				}
			}
		}
	}

	private nint MainWndProc(IntPtr hWnd, uint msg, nuint wParam, nint lParam)
	{
		switch (msg)
		{
			case NativeMessage.WM_APP_REGISTER_HOTKEY:
				{
					var id = (int)wParam;
					var (modifiers, key) = ReadHotKeyParam(lParam);

					if (!NativeMethods.RegisterHotKey(hWnd, id, (uint)modifiers, (uint)key))
					{
						lastError = Marshal.GetLastWin32Error();
						return 1;
					}

					return 0;
				}

			case NativeMessage.WM_HOTKEY:
				{
					var (modifiers, key) = ReadHotKeyParam(lParam);

					Task.Run(() => OnHotKeyDetected(modifiers, key));

					return 0;
				}

			case NativeMessage.WM_APP_UNREGISTER_HOTKEY:
				{
					var id = (int)wParam;

					if (!NativeMethods.UnregisterHotKey(hWnd, id))
					{
						lastError = Marshal.GetLastWin32Error();
						return 1;
					}

					return 0;
				}

			case NativeMessage.WM_CLOSE:
				{
					// We have been asked to close, probably by the Dispose method, post a quit message to exit the message loop
					NativeMethods.PostQuitMessage(0);
					return 0;
				}
		}

		return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
	}

	private async Task OnHotKeyDetected(Modifier modifiers, Key key)
	{
		if (logger is not null)
		{
			LogHotkeyDetected(logger, modifiers, key);
		}

		var registrationsToInvoke = Array.Empty<TinyHotKeyRegistration>();

		lock (registrationLock)
		{
			registrationsToInvoke = registrations
				.Where(r => r.Modifiers == modifiers && r.Key == key)
				.ToArray();
		}

		foreach (var registration in registrationsToInvoke)
		{
			try
			{
				await registration.Callback();
			}
			catch (Exception ex)
			{
				if (logger is not null)
				{
					LogCallbackFailed(logger, registration.Modifiers, registration.Key, ex);
				}
			}
		}
	}

	private void MessageLoop()
	{
		// Get the process instance handle
		var hInstance = Process.GetCurrentProcess().Handle;

		// Create and register a Window class with a callback to the MainWndProc
		var wndClass = new WNDCLASSEX
		{
			cbSize = Marshal.SizeOf<WNDCLASSEX>(),
			hInstance = hInstance,
			lpfnWndProc = Marshal.GetFunctionPointerForDelegate<WndProc>(MainWndProc),
			lpszClassName = className
		};

		atom = NativeMethods.RegisterClassEx(wndClass);

		if (atom == 0)
		{
			lastError = Marshal.GetLastWin32Error();
			messageLoopDone.Set();
			return;
		}

		// Create a hidden Window use the registered Window class
		hWnd = NativeMethods.CreateWindowEx(
			0,
			atom,
			"TinyHotKey",
			0,
			NativeConstants.CW_USEDEFAULT,
			NativeConstants.CW_USEDEFAULT,
			NativeConstants.CW_USEDEFAULT,
			NativeConstants.CW_USEDEFAULT,
			IntPtr.Zero,
			IntPtr.Zero,
			hInstance,
			IntPtr.Zero
		);

		if (hWnd == IntPtr.Zero)
		{
			lastError = Marshal.GetLastWin32Error();
			NativeMethods.UnregisterClass(atom, hInstance);
			messageLoopDone.Set();
			return;
		}

		// Signal to the constructor that we are ready and starting the message loop
		messageLoopDone.Set();

		// Process messages until WM_CLOSE causes MainWndProc to call PostQuitMessage
		while (NativeMethods.GetMessage(out var msg, hWnd, 0, 0) != 0)
		{
			NativeMethods.TranslateMessage(msg);
			NativeMethods.DispatchMessage(msg);
		}

		// The window is probably already destroyed but calling DestroyWindow just to be sure doesn't hurt
		NativeMethods.DestroyWindow(hWnd);

		// Unregister the Window class since we don't need it any more
		NativeMethods.UnregisterClass(atom, hInstance);

		// Signal to the Dispose method that we are done
		messageLoopDone.Set();
	}

	/// <summary>
	/// Encode Modifier and Key as a single native int for lParam
	/// </summary>
	private static nint GetHotKeyParam(Modifier modifiers, Key key)
	{
		return (nint)modifiers | ((nint)key << 16);
	}

	/// <summary>
	/// Decode lParam to a Modifier and Key
	/// </summary>
	private static (Modifier modifiers, Key key) ReadHotKeyParam(nint param)
	{
		return ((Modifier)(uint)(param & 0xFFFF), (Key)((param >> 16) & 0xFFFF));
	}

	[LoggerMessage(0, LogLevel.Information, "Registered system hotkey {modifiers} {key}")]
	private static partial void LogRegisteredHotkey(ILogger logger, Modifier modifiers, Key key);

	[LoggerMessage(1, LogLevel.Information, "Unregistered system hotkey {modifiers} {key}")]
	private static partial void LogUnregisteredHotkey(ILogger logger, Modifier modifiers, Key key);

	[LoggerMessage(2, LogLevel.Warning, "Could not register system hotkey {modifiers} {key}, lastError {lastError}")]
	private static partial void LogRegisterHotkeyFailed(ILogger logger, Modifier modifiers, Key key, int lastError);

	[LoggerMessage(3, LogLevel.Warning, "Could not unregister system hotkey {modifiers} {key}, lastError {lastError}")]
	private static partial void LogUnregisterHotkeyFailed(ILogger logger, Modifier modifiers, Key key, int lastError);

	[LoggerMessage(4, LogLevel.Information, "Added hotkey callback {modifiers} {key}")]
	private static partial void LogAddedCallback(ILogger logger, Modifier modifiers, Key key);

	[LoggerMessage(5, LogLevel.Error, "HotKey callback threw an error {modifiers} {key}")]
	private static partial void LogCallbackFailed(ILogger logger, Modifier modifiers, Key key, Exception ex);

	[LoggerMessage(6, LogLevel.Information, "Hotkey detected {modifiers} {key}")]
	private static partial void LogHotkeyDetected(ILogger logger, Modifier modifiers, Key key);
}

internal static class NativeConstants
{
	public const int CW_USEDEFAULT = -1;
}

internal static class NativeMessage
{
	public const int WM_CLOSE = 0x0010;
	public const int WM_HOTKEY = 0x0312;
	public const int WM_APP = 0x8000;
	public const int WM_APP_REGISTER_HOTKEY = WM_APP + 0x0032;
	public const int WM_APP_UNREGISTER_HOTKEY = WM_APP + 0x0033;
}

internal static partial class NativeMethods
{
#if NET7_0_OR_GREATER
	[LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	public static partial IntPtr CreateWindowEx(
	   uint dwExStyle,
	   ushort lpClassName,
	   string lpWindowName,
	   uint dwStyle,
	   int x,
	   int y,
	   int nWidth,
	   int nHeight,
	   IntPtr hWndParent,
	   IntPtr hMenu,
	   IntPtr hInstance,
	   IntPtr lpParam);

	[LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
	public static partial nint DefWindowProc(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool DestroyWindow(IntPtr hWnd);

	[LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
	public static partial IntPtr DispatchMessage(in uint lpmsg);

	[LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
	public static partial sbyte GetMessage(out uint lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[LibraryImport("user32.dll")]
	public static partial void PostQuitMessage(int nExitCode);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern ushort RegisterClassEx(in WNDCLASSEX lpwcx);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
	public static partial nint SendMessage(IntPtr hWnd, uint Msg, nuint wParam, nint lParam);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool TranslateMessage(in uint lpMsg);

	[LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool UnregisterClass(ushort lpClassName, IntPtr hInstance);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
#else
	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr CreateWindowEx(
		uint dwExStyle,
		ushort lpClassName,
		string lpWindowName,
		uint dwStyle,
		int x,
		int y,
		int nWidth,
		int nHeight,
		IntPtr hWndParent,
		IntPtr hMenu,
		IntPtr hInstance,
		IntPtr lpParam);

	[DllImport("user32.dll")]
	public static extern nint DefWindowProc(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr DispatchMessage(in uint lpmsg);

	[DllImport("user32.dll")]
	public static extern sbyte GetMessage(out uint lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

	[DllImport("user32.dll")]
	public static extern void PostQuitMessage(int nExitCode);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern ushort RegisterClassEx(in WNDCLASSEX lpwcx);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	public static extern nint SendMessage(IntPtr hWnd, uint Msg, nuint wParam, nint lParam);

	[DllImport("user32.dll")]
	public static extern bool TranslateMessage(in uint lpMsg);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool UnregisterClass(ushort lpClassName, IntPtr hInstance);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
#endif
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct WNDCLASSEX
{
	[MarshalAs(UnmanagedType.U4)]
	public int cbSize;
	[MarshalAs(UnmanagedType.U4)]
	public int style;
	public IntPtr lpfnWndProc;
	public int cbClsExtra;
	public int cbWndExtra;
	public IntPtr hInstance;
	public IntPtr hIcon;
	public IntPtr hCursor;
	public IntPtr hbrBackground;
	public string lpszMenuName;
	public string lpszClassName;
	public IntPtr hIconSm;
}
