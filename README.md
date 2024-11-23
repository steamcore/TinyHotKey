# TinyHotKey

[![NuGet](https://img.shields.io/nuget/v/TinyHotKey.svg?maxAge=259200)](https://www.nuget.org/packages/TinyHotKey/)
![Build](https://github.com/steamcore/TinyHotKey/workflows/Build/badge.svg)

TinyHotKey is a Windows HotKey handler with no dependency on Windows Forms or WPF that only listen
to specified key combinations. It uses `RegisterHotKey` from the Win32 API directly on a dedicated
thread using a hidden window, avoiding global hooks.

## Simple example

```csharp
using var tinyHotKey = new TinyHotKeyInstance();

using var registration = tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
{
	Console.WriteLine("Ctrl+Alt+D detected");

	return Task.CompletedTask;
});
```

## Example using dependency injection

```csharp
// Add TinyHotKey to your DI container with logging support
services.AddTinyHotKey();

// In a service with a dependency on ITinyHotKey
using var registration = tinyHotKey.RegisterHotKey(Modifier.Control | Modifier.Alt, Key.D, () =>
{
    Console.WriteLine("Ctrl+Alt+D detected");

    return Task.CompletedTask;
});
```

## Why TinyHotKey?
Unlike other projects TinyHotKey has no dependency on Windows Form or WPF and does not use global
hooks, which makes it possible to avoid keylogger-like behavior and also support trimming and AOT.

It is possible to use TinyHotKey in alternate UI frameworks such as Avalonia or even in console
applications.

## How it works
The Windows hotkey mechanism works by detecting keyboard combinations and posting messages to a
Window. TinyHotKey creates a hidden Window on a dedicated thread using Win32 calls, handling
all hotkey interactions and invoking callbacks on a new task off the "UI" thread. This approach
allows it to work in any kind of application running on Windows without relying on Windows Forms
or WPF which are not trimming or AOT compatible.
