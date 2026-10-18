# Troubleshooting

[Home](../README.md) · [Config](configuration.md) · [Report a problem](https://github.com/TheDivineLabs/DivineLabs_Installer/issues/new/choose)

| Symptom | Check |
|---|---|
| CLI not found | Extract SDK; run `./divinepack` on Unix or `.\divinepack.exe` in PowerShell. |
| Missing/mismatched runtime | Keep `runtime/` beside CLI; `--runtime-dir` needs the target's `runtime.json`. |
| Output is occupied | Use a new directory. Existing files are intentionally not overwritten. |
| Missing entry point/branding | Paths resolve beside config; `entryPoint` resolves inside `source`. `validate` does not replace a build. |
| Payload intersects data directory | Move shipped files out of `dataDirectories`, reserved for user-created files. |
| Symlink/reparse rejection | Package actual files in staging. Some macOS framework layouts are unsupported. |
| macOS reports untrusted app | Use your normal signing/notarization release process. The SDK does not sign products. |
| Linux UI cannot start | Check X11/display and fontconfig/native graphics dependencies for your distribution. |
| Action runner mismatch | Select a matching runner from the automation guide. The wrapper does not cross-execute SDKs. |
| Foreign files at install target | Choose an empty folder or matching previous installation. Preserve user files. |

Reports should include SDK version, OS/CPU, command, expected behavior and relevant error output.
Use a minimal config with private paths removed. Do not upload credentials, proprietary payloads or personal data.
