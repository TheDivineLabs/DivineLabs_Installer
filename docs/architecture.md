# Engine, components and templates

[Home](../README.md) · [Embedding](embedding.md) · [API](api.md) · [Configuration](configuration.md)

Choose the smallest layer your application needs:

| Layer / package | Responsibility | Depends on |
|---|---|---|
| `DivineLabs.Installer.Core` | Public engine API, package/receipt contracts, validation, file transactions, rollback and recovery | .NET 10 only |
| `DivineLabs.Installer.Platforms` | Per-user OS registration, shortcuts, discovery and explicit launch | Core |
| `DivineLabs.Installer.Components` | Bindable directory picker, step indicator and progress display | Avalonia |
| `DivineLabs.Installer.Templates.Classic` | Complete DivineLabs Classic setup/uninstall window and scoped styling | Core, Components, Fluent theme |
| Desktop host | Startup, Native AOT executable, maintenance relocation and cleanup | Classic, Platforms |
| CLI | Config/package/bundle tools | Core, Platforms |

Core has no Avalonia dependency. Components has no engine, platform or Classic dependency.
Classic receives `IInstallerEngine` and `IPlatformServices` through `ClassicSession`;
it does not create an engine or mutate host application resources. Desktop and CLI consume the public API.

## Three ways to use it

1. **Configuration:** `divinepack build` combines a config, payload and prebuilt Classic runtime.
2. **Custom launcher:** reference Core and optional Platforms/Components, then call `IInstallerEngine` from your own UI.
3. **Classic host:** reference Templates.Classic, create a session and open `ClassicWindow` in your Avalonia application.

The [custom launcher](../examples/embedded-launcher/) and [Classic host](../examples/classic-host/)
are independent, compilable applications. Neither references the Desktop executable.

## Templates

`template: "divinelabs-classic"` is the default. The builder compares config identity with `runtime.json`.
A mismatch fails before creating bundle output. Core carries template identity as metadata and performs
installation independently of it. Custom hosts may use packages without showing their declared template.
Classic itself accepts packages declaring Classic.

Templates are compiled libraries and native runtimes, not dynamically loaded DLL plugins.
To provide another template, build a host using Core/Components, publish a matching native runtime and
set its `runtime.json.template`. Keep the `formatVersion`, `runtime`, `entryPoint` and `nativeAot` runtime contract.
The stock publish script produces Classic; arbitrary template projects are not automatically published.

## Host policy

The host owns license acceptance, package acquisition/authentication, UI-thread dispatch, application launch,
signing and update scheduling. Core performs verified local file transactions. Progress callbacks must not throw.
Custom platform integrations implement `IPlatformServices`; discovery is optional through `IInstallationLocator`.
No arbitrary install-script hooks or dynamic assembly discovery are introduced in this version.

The current in-process API is .NET 10, not a C ABI. Other languages can use the native CLI;
shipping a Native AOT host removes the destination machine's .NET runtime requirement.

## Compatibility

0.2 reads 0.1 packages/configs/receipts with the default Classic identity. Newly written metadata includes
`template`; strict 0.1 readers cannot read it. Keep the new maintenance runtime when updating a standalone
installation; downgrade to the old reader is not supported. Do not switch a standalone installation into
launcher-owned maintenance without an explicit integration/runtime migration.
The synchronous `Engine` methods remain available; async hosts should use `IInstallerEngine`.
