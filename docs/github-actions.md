# Build installers in GitHub Actions

[Home](../README.md) · [Configuration](configuration.md) · [Copyable workflow](../examples/github-actions/build-installer.yml)

Build your application as usual, then package it with the composite action. No .NET SDK, C# project or changes
to application code are required. The action downloads a published native SDK, checks its SHA-256 against
that release's `SHA256SUMS.txt`, validates the config and builds the bundle. Checksums detect corruption;
publisher trust comes from the selected repository/release.

```yaml
- uses: actions/checkout@v4
# Your application build goes here.
- uses: TheDivineLabs/DivineLabs_Installer@main
  id: installer
  with:
    config: installer.json
    runtime: win-x64
    output: installer-output
    sdk-version: v0.2.0
- uses: actions/upload-artifact@v4
  with:
    name: my-app-installer
    path: ${{ steps.installer.outputs.archive }}
```

The action reference selects wrapper source; `sdk-version` independently selects the native SDK.
For reproducible releases pin actions to reviewed full commit SHAs and keep an explicit SDK version.
The action produces an artifact, without publishing a release or running your application.

| Input | Default | Meaning |
|---|---|---|
| `config` | Required | Config relative to the repository workspace. |
| `runtime` | Required | Supported RID; must match runner OS/CPU. |
| `output` | `installer-output` | New/empty directory; occupied directories fail without replacement. |
| `sdk-version` | `v0.2.0` | Published SDK release tag, including `v`. |

Output `archive` is the absolute installer ZIP/TAR.GZ path. Upload with `upload-artifact`.
Use `permissions: contents: read`; no write token or publishing secret is required.
PowerShell 7 and Unix `tar` are available on GitHub-hosted runners. Self-hosted runners need
those tools and the SDK's native OS dependencies.

| Runtime | GitHub-hosted runner |
|---|---|
| `win-x64` | `windows-2025` |
| `win-arm64` | `windows-11-arm` |
| `osx-x64` | `macos-15-intel` |
| `osx-arm64` | `macos-15` |
| `linux-x64` | `ubuntu-22.04` |
| `linux-arm64` | `ubuntu-22.04-arm` |

For cross-packaging use CLI `--runtime-dir` with a separately downloaded target SDK.
Application binaries must already exist under each configured source.
