# Contributing

Use the pinned SDK/Avalonia versions. Keep Core independent of UI and platform APIs.
Config/package changes must update the schema, source-generated JSON, examples and compatibility notes.
Native AOT is a build contract: no runtime XAML or code generation, reflection-based JSON or unnecessary dependencies.

Choose proportional checks: file transactions need focused failure/recovery contracts; UI needs affected renders;
documentation needs reference review. Tests use isolated temporary directories and --no-integrations.
Do not launch game/application clients or install into actual user application folders during tests.
Never claim OS acceptance before its native build and smoke check pass. Preserve third-party notices.

## Start here

Read the [build commands](README.md#build-and-verify), [config contract](docs/configuration.md) and [roadmap](docs/ROADMAP.md).
For documentation languages see [translation guidance](docs/i18n/CONTRIBUTING.md).
The Action wrapper has its own six-runner workflow using published SDKs, without recompiling the application.

Public API and layer boundaries are described in [architecture](docs/architecture.md) and [API](docs/api.md).
Keep Components independent of Core and OS services. Classic styles must remain window-scoped.
Packaging changes must retain compilable external consumers for both launcher and Classic packages.
