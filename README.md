# TinyHotKey

[![NuGet](https://img.shields.io/nuget/v/TinyHotKey.svg?maxAge=259200)](https://www.nuget.org/packages/TinyHotKey/)
![Build](https://github.com/steamcore/TinyHotKey/workflows/Build/badge.svg)

Windows HotKey handler that does not listen to all keyboard input and has no dependency on
Windows Forms or WPF.

This is using `RegisterHotKey` on the Win32 API directly, no global hooks, it only listens
to what you tell it to listen to.

## Simple example

```csharp
using var tinyHotKey = new TinyHotKeyInstance();

using var binding = tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
{
	Console.WriteLine("Ctrl+Alt+D detected");

	return Task.CompletedTask;
});
```

## Example using dependency injection

```csharp
// Add it to your DI container using this overload to get logging support as well
services.AddTinyHotKey();

// Later in some service with a dependency on ITinyHotKey tinyHotKey
tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
{
	Console.WriteLine("Ctrl+Alt+D detected");

	return Task.CompletedTask;
});
```

## Why? There are hundreds of other similar projects

Actually, no.

I needed trimmable and AOT-compatible hotkey detection and I wasn't able to find any other
projects that do this with no dependency on Windows Forms or WPF which is important because
those UI frameworks are not even remotely compatible with either trimming or AOT compilation.

There are other projects that use global keyboard hooks instead but they listen to every single
key press, just like a keylogger, which is something that I don't like.


## How it works

Windows hotkey detection work by detecting keyboard combinations for you and posts messages
to your Window. That sounds simple enough but that means first you must have a Window and
everything must be done on the UI thread.

So what if you don't have a Window? This works in console apps too, right?

There is no way around having a Window so what TinyHotKey does is create a hidden Window
on a dedicated thread using more Win32 calls that handles all hotkey interaction and calls
callbacks on a new task off the "UI" thread. There are other projects that do exactly this
but none that I was able to find that does all this without parts of Windows Forms or WPF.
