# DivineLabs Installer

**Deine App. Dein Design. Eine Konfiguration.**

[English](../../README.md) · [Русский](../../README.ru.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [简体中文](README.zh-CN.md)

Native Installer für Windows, macOS und Linux, jeweils x64 und ARM64. Avalonia + .NET Native AOT, MIT-Lizenz.

[SDK 0.1.1 Preview](https://github.com/TheDivineLabs/DivineLabs_Installer/releases/tag/v0.1.1) · [Website](https://thedivinelabs.github.io/installer/) · [GitHub](https://github.com/TheDivineLabs/DivineLabs_Installer)

![DivineLabs Installer](../images/welcome.png)

## Schnellstart

Lade das passende SDK herunter und entpacke es. Behalte `runtime/` neben `divinepack`. Die Befehle erzeugen einen funktionsfähigen Beispiel-Installer. Ersetze danach die Dateien unter `application/` und bearbeite `installer.json`. Unter Windows verwende `.\divinepack.exe` und `win-x64`, unter macOS `osx-arm64` oder `osx-x64`.

```sh
./divinepack init --dir my-installer
./divinepack validate --config my-installer/installer.json
./divinepack build --config my-installer/installer.json --runtime linux-x64 --output my-output
```

## Konfiguration und Automatisierung

Anwendungscode braucht weder C# noch Avalonia oder ein zusätzliches SDK. Pfade werden relativ zur Konfiguration aufgelöst; `entryPoint` ist relativ zu `source`. Ein stabiler Produkt-`id` und unveränderte Datenverzeichnisse ermöglichen Updates. `dataDirectories` bleiben standardmäßig erhalten; dort dürfen keine ausgelieferten Dateien liegen.

- [JSON configuration (English)](../configuration.md)
- [Hello example](../../examples/hello/installer.json)
- [GitHub Action (English)](../github-actions.md) · [Workflow](../../examples/github-actions/build-installer.yml)
- [Troubleshooting (English)](../troubleshooting.md)
- [Contributing (English)](../../CONTRIBUTING.md) · [Roadmap (English)](../ROADMAP.md)

## Grenzen der Vorschau

Die Oberfläche unterstützt derzeit Englisch und Russisch sowie helle/dunkle Themes. Übersetzte Dokumentation erweitert die Sprachen der Oberfläche nicht. Installer-Archive müssen vor dem Start entpackt werden. Diese Vorschau erzeugt keine einzelnen selbstentpackenden EXEs, MSI-, PKG-, DEB- oder RPM-Pakete. Keine automatische Update-Verteilung oder integrierte Signierung. Symlinks werden abgelehnt. Native Grafikabhängigkeiten des Betriebssystems bleiben erforderlich.

[MIT](../../LICENSE) · [Third-party notices](../../NOTICE.md)
