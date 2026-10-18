# DivineLabs Installer

**Tu aplicación. Tu diseño. Una configuración.**

[English](../../README.md) · [Русский](../../README.ru.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [简体中文](README.zh-CN.md)

Instaladores nativos para Windows, macOS y Linux, con x64 y ARM64. Avalonia + .NET Native AOT y licencia MIT.

[SDK 0.1.1 Preview](https://github.com/TheDivineLabs/DivineLabs_Installer/releases/tag/v0.1.1) · [Website](https://thedivinelabs.github.io/installer/) · [GitHub](https://github.com/TheDivineLabs/DivineLabs_Installer)

![DivineLabs Installer](../images/welcome.png)

## Inicio rápido

Descarga y extrae el SDK de tu plataforma. Mantén `runtime/` junto a `divinepack`. Estos comandos crean un instalador de ejemplo funcional. Después sustituye los archivos de `application/` y edita `installer.json`. En Windows usa `.\divinepack.exe` y `win-x64`; en macOS, `osx-arm64` u `osx-x64`.

```sh
./divinepack init --dir my-installer
./divinepack validate --config my-installer/installer.json
./divinepack build --config my-installer/installer.json --runtime linux-x64 --output my-output
```

## Configuración y automatización

Tu aplicación no necesita C#, Avalonia ni integrar otro SDK. Las rutas parten del archivo de configuración; `entryPoint` parte de `source`. Conserva el `id` del producto y las definiciones de datos entre versiones. `dataDirectories` se conserva por defecto; no incluyas archivos distribuidos en esas carpetas.

- [JSON configuration (English)](../configuration.md)
- [Hello example](../../examples/hello/installer.json)
- [GitHub Action (English)](../github-actions.md) · [Workflow](../../examples/github-actions/build-installer.yml)
- [Troubleshooting (English)](../troubleshooting.md)
- [Contributing (English)](../../CONTRIBUTING.md) · [Roadmap (English)](../ROADMAP.md)

## Límites de la versión preliminar

La interfaz está disponible en inglés y ruso, con temas claro y oscuro. Traducir la documentación no añade idiomas a la interfaz. Extrae el archivo del instalador antes de ejecutarlo. Esta versión no genera EXE autoextraíbles de un solo archivo ni MSI, PKG, DEB o RPM. No incluye distribución automática de actualizaciones ni firma integrada. Se rechazan enlaces simbólicos. Siguen siendo necesarias las dependencias gráficas nativas del sistema.

[MIT](../../LICENSE) · [Third-party notices](../../NOTICE.md)
