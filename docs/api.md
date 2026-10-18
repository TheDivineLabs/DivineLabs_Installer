# Public engine API

[Architecture](architecture.md) · [Embedding examples](embedding.md)

```csharp
IInstallerEngine engine = new Engine(new PlatformServices());
OperationResult result = await engine.InstallAsync(
    new InstallRequest(packagePath, applicationDirectory, Options: new InstallOptions(false)),
    progress, cancellationToken);
await engine.UninstallAsync(new UninstallRequest(applicationDirectory), progress, cancellationToken);
```

| API | Contract |
|---|---|
| `InstallAsync(InstallRequest, progress?, cancellationToken)` | Fresh install or update matching product/runtime in the target directory. |
| `UninstallAsync(UninstallRequest, progress?, cancellationToken)` | Remove owned files; `RemoveData` is false by default. |
| `RecoverAsync(targetDirectory, cancellationToken)` | Restore/finish an interrupted journal while holding the same per-target writer lock. |
| `GetInstallation(targetDirectory)` | Validated receipt or null. Synchronous snapshot; no recovery/writer lock. |

All asynchronous operations run file work off the caller thread. Await the task for completion and errors;
concurrent writers to the same target fail through the existing operation lock. `RecoverAsync` honors
cancellation before starting; once recovery begins, it finishes restoring the transaction.

`InstallRequest` contains local `PackagePath`, `TargetDirectory`, optional `RuntimeDirectory` and `Options`.
The runtime directory is a separately published maintenance host, never the running launcher's folder.
Without it the engine does not invent an uninstaller executable. The default options enable OS integrations;
embedded hosts should explicitly select `new InstallOptions(false)` or provide their own platform integration.
The stock OS integration expects the matching standalone runtime for uninstall commands.

`InstallProgress` has `Phase`, `Detail` and `Percent`. Phases currently include `verify`, `install`, `remove`
and `complete`; use `Percent` for display, not phase names as lifecycle callbacks. Reports may skip percentages.
`new Progress<InstallProgress>(...)` captures the current UI context. Custom reporters run where `Report` is
called, normally on the worker, and must not throw. No progress notification is a place to mutate payload files.

Cancellation before commit rolls back uncommitted file changes and raises `OperationCanceledException`.
Cancellation requested after commit may still return success. `OperationResult.Warnings` reports failed OS
integration after a committed file transaction; it does not imply file rollback.
Other failures surface as exceptions (for example `IOException` or `InvalidDataException`). Read a receipt only
after awaiting your operation; use recovery before opening maintenance after a process interruption.

`IPlatformServices` supplies default directories, register/unregister and explicit launch. The engine never
calls `Launch`. Implement it for launcher-specific OS behavior, or use the stock Platforms library.
`IInstallationLocator` optionally supplies `(Folder, Version)` discovery to Classic; Core does not require it.

The engine validates archive hashes/path ownership; it does not authenticate publishers. Trust/signature
verification and license acceptance belong to the host before `InstallAsync`. Transactions do not cover
host-side downloads, arbitrary scripts, system services or drivers.
