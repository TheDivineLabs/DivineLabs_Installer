# Configuration reference

[Home](../README.md) · [Automation](github-actions.md) · [Troubleshooting](troubleshooting.md) · [JSON schema](../schemas/installer.schema.json)

`installer.json` is the integration contract. Use `$schema` for editor completion:

```json
{
  "$schema": "https://raw.githubusercontent.com/TheDivineLabs/DivineLabs_Installer/main/schemas/installer.schema.json",
  "schemaVersion": 1,
  "id": "com.example.myapp",
  "name": "My App",
  "publisher": "Example",
  "version": "1.0.0",
  "applications": {
    "win-x64": { "source": "dist/windows", "entryPoint": "MyApp.exe" },
    "osx-arm64": { "source": "dist/macos", "entryPoint": "MyApp.app/Contents/MacOS/MyApp" },
    "linux-x64": { "source": "dist/linux", "entryPoint": "myapp" }
  },
  "branding": { "icon": "branding/icon.png", "cover": "branding/cover.png", "accent": "#6658E8" },
  "license": "LICENSE.txt",
  "language": "en",
  "links": [{ "label": "Support", "url": "https://example.com/support" }],
  "dataDirectories": ["data", "logs"]
}
```

Only configure targets you actually build. This example requires your application and image files.
Use [the runnable Hello example](../examples/hello/installer.json) to try the toolkit immediately.

| Field | Purpose |
|---|---|
| `id` | Stable ownership identity, 3–128 letters/digits/dots/hyphens; starts with letter/digit. Keep it unchanged across updates. |
| `name`, `publisher`, `version` | Required display and installation metadata. |
| `description` | Welcome text supplied by the developer. |
| `installDirectoryName` | Optional folder override; one directory name, without separators. |
| `applications` | Build inputs keyed by `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`. |
| `source` | App directory relative to config. Its contents become the installed payload. |
| `entryPoint` | Relative to `source`; automatically executable on Unix. |
| `executableFiles` | Additional paths relative to `source` requiring Unix executable mode. |
| `branding.icon`, `branding.cover` | Optional images relative to config; use PNG. Reference cover slot: 330 × 740. |
| `branding.accent` | Six-digit hex color; default `#6658E8`. |
| `branding.showFrameworkBranding` | `false` hides the framework logo/attribution in the GUI. Executable identity remains DivineLabs. |
| `license` | Optional UTF-8 license relative to config. Framework/component notices remain available. |
| `language` | Initial GUI language: `en` (default) or `ru`; users can switch. |
| `links` | Optional label and HTTP(S) URL pairs. |
| `dataDirectories` | Runtime-created user directories retained by default; defaults to `data` and `logs`. |

Source, branding and license paths resolve beside `installer.json`, independently of the shell directory.
Never put source payload files inside reserved `dataDirectories` or `.divinelabs-installer`.
Keep data-directory definitions stable across updates; changes require an explicit migration.
Symlinks/reparse points are rejected, including symlinks within some macOS framework bundles.

`validate` checks structure and path rules. `build` additionally reads payload/assets, checks the entry point
and produces the bundle. A successful `validate` alone is not a successful package build.

## Package a different architecture

Run the CLI for your **host**, then supply the **target** SDK's `runtime/`:

```sh
./divinepack build --config installer.json --runtime win-arm64 --runtime-dir ../sdk-win-arm64/runtime --output output-win-arm64
```

Do not execute a different architecture's CLI on your host. The GitHub Action uses matching native runners.
Do not place output inside the application or SDK source directories.

## Updates and removal

Build a new version with the same `id` and install into the same directory. Owned files update transactionally;
obsolete owned files are removed. Foreign files are retained. User data is preserved unless the user explicitly
chooses full removal. This preview has no automatic online update feed.
