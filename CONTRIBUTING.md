# Contributing

Use the pinned SDK/Avalonia versions. Keep Core independent of UI and platform APIs.
Config/package changes must update the schema, source-generated JSON, examples and compatibility notes.
Native AOT is a build contract: no runtime XAML or code generation, reflection-based JSON or unnecessary dependencies.

Choose proportional checks: file transactions need focused failure/recovery contracts; UI needs affected renders;
documentation needs reference review. Tests use isolated temporary directories and --no-integrations.
Do not launch game/application clients or install into actual user application folders during tests.
Never claim OS acceptance before its native build and smoke check pass. Preserve third-party notices.
