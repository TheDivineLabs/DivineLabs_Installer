# Custom launcher

.NET 10 / Avalonia example consuming **Core + Platforms + Components**. No Classic/Desktop reference.

```sh
dotnet run --project examples/embedded-launcher -- /path/to/application.dlpack /path/to/separate-app
```

Create the package with `divinepack pack` first. Inspect its license, accept it, then install/update or uninstall.
Nothing installs at startup. Cancellation and user-data retention use the same public engine API as Classic.
The launcher owns maintenance; OS integrations are disabled. Use a separate application target, not the running launcher directory.

[Embedding guide](../../docs/embedding.md) · [API](../../docs/api.md)
