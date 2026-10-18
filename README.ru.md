<div align="center">
<img src="assets/DivineLabs.svg" width="72" height="72" alt="DivineLabs">
<h1>DivineLabs Installer</h1>
<p><strong>Ваше приложение. Ваш дизайн. Один конфиг.</strong></p>
<p>Открытый конструктор установщиков для Windows, macOS и Linux.<br>Avalonia + .NET Native AOT · MIT</p>

[Сайт](https://thedivinelabs.github.io/installer/) · [Скачать SDK](https://github.com/TheDivineLabs/DivineLabs_Installer/releases/tag/v0.2.0) · [Настройки](docs/configuration.md) · [GitHub Action](docs/github-actions.md) · [Сообщить об ошибке](https://github.com/TheDivineLabs/DivineLabs_Installer/issues/new/choose)

[English](README.md) · **Русский** · [Deutsch](docs/i18n/README.de.md) · [Español](docs/i18n/README.es.md) · [简体中文](docs/i18n/README.zh-CN.md)
</div>

![Установщик — снимок настоящего интерфейса](docs/images/welcome.png)

Упакуйте готовую программу, задав название, версию, издателя, точку входа, обложку,
иконку и лицензию в JSON. Встраивать библиотеки установщика в приложение не нужно.
Интерфейс следует дизайну Idle Slayer: шаги, лицензии, параметры, прогресс и удалятор
в одном стиле. Игровые изображения не включены.

## Выберите способ подключения

| Задача | Что использовать |
|---|---|
| Собрать готовый установщик по JSON | CLI + шаблон DivineLabs Classic |
| Встроить установку в свой лаунчер | Core: публичный `IInstallerEngine` + опционально Platforms |
| Сделать собственные экраны | Components: шаги, выбор папки, прогресс |
| Открыть готовый дизайн в своём приложении | Templates.Classic: `ClassicSession` + `ClassicWindow` |

[Архитектура](docs/architecture.md) · [Встраивание и примеры](docs/embedding.md) · [Контракт API](docs/api.md).
Это четыре отдельные библиотеки `.nupkg` для .NET 10. Они доступны в [выпуске 0.2.0](https://github.com/TheDivineLabs/DivineLabs_Installer/releases/tag/v0.2.0);
публикация на nuget.org пока не выполнялась. Документация API — на английском.

## Быстрый старт

Скачайте [SDK 0.2.0 Preview](https://github.com/TheDivineLabs/DivineLabs_Installer/releases/tag/v0.2.0)
для своей ОС и архитектуры. Распакуйте; `runtime/` должна остаться рядом с `divinepack`.
В PowerShell:

```powershell
.\divinepack.exe init --dir my-installer
.\divinepack.exe validate --config my-installer/installer.json
.\divinepack.exe build --config my-installer/installer.json --runtime win-x64 --output my-output
```

Сначала получите рабочий демо-установщик, затем замените файлы в `application/` и
отредактируйте конфиг. На Linux/macOS используйте `./divinepack` и соответствующий RID.
При сборке по конфигу C#, Avalonia и .NET SDK не нужны. Для встраивания API используется .NET 10.

| Платформа | Архитектуры | Результат сборки |
|---|---|---|
| Windows | x64, ARM64 | ZIP с EXE, библиотеками и полезной нагрузкой |
| macOS | Intel x64, Apple Silicon | ZIP с установщиком `.app` |
| Linux | x64, ARM64 | TAR.GZ с нативным установщиком |

SDK и результат сборки установщика — разные архивы. Перед запуском результат нужно распаковать.
Для другой платформы CLI принимает её готовый `runtime/` через `--runtime-dir`.

## Для разработчиков

- [Полный пример конфига и справочник полей](docs/configuration.md) — английский.
- [Рабочий Hello-пример для шести платформ](examples/hello/installer.json).
- [GitHub Action без установки .NET](docs/github-actions.md) и [готовый workflow](examples/github-actions/build-installer.yml).
- [JSON Schema для подсказок редактора](schemas/installer.schema.json); в VS Code привязка уже настроена.
- [Разбор ошибок](docs/troubleshooting.md), [правила участия](CONTRIBUTING.md) и [планы](docs/ROADMAP.md) — английский.

Пути к приложению, картинкам и лицензии считаются от конфига. `entryPoint` — от `source`.
`dataDirectories` сохраняются при обновлении и обычном удалении; не помещайте туда поставляемые файлы.
`id` и определения каталогов данных должны оставаться стабильными между версиями.
`branding.showFrameworkBranding: false` скрывает знак и подпись фреймворка в интерфейсе.

## Возможности и границы preview

Установка и обновление используют журнал, SHA-256 и откат изменений при ошибке.
Есть восстановление после аварийного завершения процесса, удаление и повторная установка
с сохранением данных. Чужие файлы не заменяются. Нативные сборки и проверки установки/удаления
выполняются на всех шести платформах CI.

Интерфейс сейчас на русском и английском, со светлой и тёмной темами.
Пять языков документации не означают пять языков приложения.
Native AOT убирает необходимость устанавливать .NET; системные графические зависимости остаются.

Это ранняя версия: одиночный самораспаковывающийся EXE, MSI, PKG, DEB/RPM пока не создаются.
Исполняемый файл сохраняет идентичность DivineLabs; оформление из конфига применяется к интерфейсу.
Нет автоматической доставки обновлений, установки служб/драйверов и встроенной подписи/notarization.
SHA-256 проверяет целостность, а не подлинность издателя. Пакеты с symlink отклоняются,
включая некоторые macOS framework bundles. На macOS не создаются ярлыки Dock/рабочего стола.

[Подробности движка и команды](README.md) · [Лицензия MIT](LICENSE) · [Компоненты](NOTICE.md)
