# Classic host

A minimal .NET 10 / Avalonia application using **Templates.Classic + Platforms**, independently of Desktop.

```sh
dotnet run --project examples/classic-host -- /path/to/application.dlpack /path/to/separate-app
```

The host owns the package session and application lifetime. No installation occurs before the user completes the wizard.
Integrations are disabled by default because this sample supplies no standalone maintenance runtime.
For a full standalone installer use `divinepack build` or supply the matching published runtime/options explicitly.

[Embedding guide](../../docs/embedding.md) · [Architecture](../../docs/architecture.md)
