# DivineLabs Installer

A config-driven installer toolkit built with **Avalonia 12 + .NET 10 Native AOT**.
Windows, macOS and Linux; x64 and ARM64. MIT licensed.

The interface follows DivineLabs Idle Slayer's maintenance-window design: branded cover,
step navigation, license acceptance, options, progress and matching uninstall flow.
English/Russian and light/dark themes are included. No game artwork is bundled.

**Initial version: 0.1.0.** No .NET installation or WebView is needed on destination machines;
OS/native graphics dependencies still apply. Native AOT does not make every binary a single file.

## Quick start

Download your platform SDK from a successful **Build and verify** workflow artifact.
Extract it; keep `runtime/` alongside `divinepack`.

```sh
divinepack init --dir my-installer
# Put your application build in application/ and edit installer.json.
divinepack validate --config my-installer/installer.json
divinepack build --config my-installer/installer.json --runtime win-x64 --output my-output
```

Use `divinepack.exe` on Windows and `./divinepack` when it is not on your shell's PATH.
A different target requires its prebuilt native runtime via `--runtime-dir`.
Application developers need no C#, Avalonia, .NET SDK or embedded application SDK.
`init` creates a Windows command-script and Unix shell-script demo for all six targets.

```json
{
  "schemaVersion": 1,
  "id": "com.example.app",
  "name": "My Application",
  "publisher": "Example",
  "version": "1.0.0",
  "description": "Everything you need to get started.",
  "applications": {
    "win-x64": { "source": "dist/windows", "entryPoint": "MyApp.exe" },
    "osx-arm64": { "source": "dist/macos", "entryPoint": "MyApp.app/Contents/MacOS/MyApp" },
    "linux-x64": { "source": "dist/linux", "entryPoint": "myapp" }
  },
  "branding": { "icon": "branding/icon.png", "cover": "branding/cover.png", "accent": "#6658E8" },
  "license": "LICENSE.txt",
  "dataDirectories": ["data", "logs"]
}
```

Paths are relative to the config. List only architectures you actually ship.
PNG is recommended for branding. The reference cover slot is 330×740.
The JSON schema provides editor completion. `links` contains optional `{ "label", "url" }`
HTTP(S) links; `language` accepts `en` or `ru`.

`dataDirectories` are reserved for runtime-created user files; do not ship payload files there.
Updates refuse to change these definitions without an explicit migration.
`executableFiles` marks extra Unix executables when packaging on a host that cannot preserve
Unix modes. The main entry point is automatically marked executable.

## Output and integration

| Platform | Output | Default installed location |
|---|---|---|
| Windows | ZIP: native setup EXE, libraries and `.dlpack` payload | `%LOCALAPPDATA%/Programs/<publisher>/<name>` |
| macOS | ZIP containing installer `.app` | `~/Applications/<publisher>/<name>` |
| Linux | TAR.GZ: native setup, libraries and payload | `$XDG_DATA_HOME/<publisher>/<name>` or `~/.local/share/<publisher>/<name>` |

Extract the archive before launching. Keep the native libraries next to the executable.
This version creates **native installer bundles**, not single self-extracting EXEs, MSI,
signed PKG, DEB or RPM packages. Per-product PE resources/macOS bundle icons are not yet
generated; the executable retains DivineLabs identity and UI branding comes from config.

Windows: Installed Apps registration and optional desktop/Start-menu shortcuts. Shortcuts
use the built-in Windows PowerShell/WScript integration. Linux: applications-menu entry,
uninstall action and optional desktop launcher (desktop environments may require user trust).
macOS: user Applications installation; no Dock/desktop shortcut generation in this version.

Maintenance runtime: `.divinelabs-installer/runtime/DivineLabs.Installer` (`.exe` on Windows).
Run it with `--uninstall --dir PATH`. It relocates itself before removing its installed files.
The application payload is **not** duplicated into the maintenance runtime.

## Automation

```sh
divinepack pack --config installer.json --runtime linux-x64 --output application.dlpack
divinepack inspect --package application.dlpack
divinepack install --package application.dlpack --dir ./test-app --no-integrations
divinepack uninstall --dir ./test-app
divinepack uninstall --dir ./test-app --remove-data
```

For GUI maintenance after CLI installation, supply `--runtime-dir` on `install`.
Native setup supports `--silent --dir PATH --no-integrations` and `--uninstall --silent --dir PATH`.
Exit codes: **0** completed, **2** failed, **3** cancelled where cancellation is exposed.
Integration failures are warnings after the file transaction. Silent mode never launches the app.

## Engine behavior

- Streaming archives, SHA-256 and size checks before application-file replacement.
- Stable ownership, refusal to replace foreign files, removal of obsolete owned files on update.
- Journaled file changes, rollback on failure/cancellation, restart recovery after process interruption.
- User data retained by default; retained ownership supports reinstalling later.
- Traversal, duplicate paths, reserved state and symlink/reparse targets are rejected.
- Per-user installation without elevation; no service, driver or system-package installation.

Checksums detect corruption; they **do not authenticate publishers**. Distributors sign their
own platform artifacts. This release has no trusted online update feed or automatic update
delivery. Packages containing symlinks, including some macOS framework bundles, are rejected.
Process-interruption recovery is tested; power-loss durability and hostile concurrent filesystem
mutation are not claimed. The toolkit is a starting release, not production acceptance for
arbitrary applications or every Linux distribution.

## Build and verify

Use `global.json`'s SDK and the native compiler: Windows MSVC/SDK, macOS Xcode CLI tools,
or Linux clang/zlib development packages. Native AOT builds run on their target OS.

```sh
dotnet build DivineLabs.Installer.slnx -c Release
dotnet run --project tests/DivineLabs.Installer.Tests -c Release
pwsh ./scripts/publish.ps1 -Runtime linux-x64 -Output ./artifacts/sdk-linux-x64
pwsh ./scripts/smoke-native.ps1 -Sdk ./artifacts/sdk-linux-x64
dotnet run --project tests/DivineLabs.Installer.Visuals -c Release -- artifacts/screenshots
```

CI covers six OS/CPU targets: contracts, Native AOT SDK publication, configuration-driven
bundle construction, installation and the **installed** uninstaller, including data retention.
Windows renders all five pages in both themes/languages without opening a desktop window.

`Core` contains portable contracts/transactions; `Platforms` implements system integration;
`Desktop` is the Avalonia shell; `Cli` is the native `divinepack` builder.
See [CONTRIBUTING.md](CONTRIBUTING.md), [NOTICE.md](NOTICE.md) and [README.ru.md](README.ru.md).
