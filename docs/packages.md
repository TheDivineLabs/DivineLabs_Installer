# DivineLabs Installer libraries

A UI-independent installation engine, optional OS integrations, reusable Avalonia controls and the DivineLabs Classic template.

- DivineLabs.Installer.Core: IInstallerEngine, typed requests, package/receipt contracts and transactional file engine. No UI dependency.
- DivineLabs.Installer.Platforms: per-user Windows/macOS/Linux integrations and installation discovery.
- DivineLabs.Installer.Components: StepIndicator, DirectoryPicker and OperationProgressView. No engine/platform/theme dependency.
- DivineLabs.Installer.Templates.Classic: complete Classic setup/uninstall window, consuming the same API and controls.

Target: .NET 10; Native AOT compatible. Avalonia libraries use Avalonia 12.1.4.
These preview packages are distributed as local-feed .nupkg files; publication to nuget.org is not implied.

Source and license: https://github.com/TheDivineLabs/DivineLabs_Installer
Embedding guide: https://github.com/TheDivineLabs/DivineLabs_Installer/blob/main/docs/embedding.md
API reference: https://github.com/TheDivineLabs/DivineLabs_Installer/blob/main/docs/api.md
MIT. Preserve framework and third-party notices when distributing applications.
