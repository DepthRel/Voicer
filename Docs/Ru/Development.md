# Разработка Voicer

[English](../En/Development.md) · [Deutsch](../De/Development.md) · [Español](../Es/Development.md) · [Français](../Fr/Development.md) · [Português](../Pt/Development.md) · **Русский**

## Сборка, тесты, запуск

```bash
dotnet build Voicer.slnx                     # ожидается 0 предупреждений: правила кода — ошибки
dotnet test Voicer.slnx                      # xUnit 2 на VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

В VS Code профиль «Voicer» из `.vscode/launch.json` держит данные отладочной копии в `.vscode/dev/data` (вне git),
поэтому скачанная модель переживает чистую сборку. Рабочий процесс можно запустить отдельно:

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <модель ggml> --audio <wav 16 кГц> --device 0
```

## Правила

Правила кода — в [AGENTS.md](../../AGENTS.md); их проверяют сборка и `Tests/Voicer.Conventions.Tests`.
`Tests/Voicer.Localization.Tests` проверяет, что во всех пакетах одинаковые ключи и подстановки, что каждый ключ из
кода и разметки существует и что в исходниках нет русского текста.

## Как добавить язык

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` со всеми ключами `en.json` (подхватывается сам).
2. Флаг: блок в `Scripts/Assets/generate-flags.ps1`, затем запустить его (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. Установщик: строка в `[Languages]` и блоки в `[Messages]`, `[CustomMessages]` файла `Scripts/App/voicer.iss`
   (UTF-8 с BOM), и `Scripts/Shared/License/intro.<code>.txt`.
4. Заметки пакетов: `Scripts/App/Notes/README.<code>.txt`; пункты меню в `Scripts/App/build-deb.sh` и
   `publish-common.sh`.
5. Документация: `Docs/<Code>/` с тем же набором файлов, что и `Docs/En` (см. [Документация](#документация)).
6. Страница проекта: запись в `list` и `docs` файла `Sources/Apps/Voicer.Pages/assets/languages.js`, её флаг
   `assets/flags/<code>.svg` и таблица в `assets/site.js`.

## Документация

Документы разложены по языкам: `Docs/En` — основной набор; `Docs/De`, `Docs/Es`, `Docs/Fr`, `Docs/Pt` и `Docs/Ru`
хранят тот же набор на немецком, испанском, французском, португальском и русском. Строка под заголовком каждого
документа ведёт на тот же документ на других языках.

- набор файлов и их имена одинаковы во всех языках; изменение смысла вносится во все языки сразу;
- ссылки снаружи (README) ведут в `Docs/En`; страница проекта открывает документы на выбранном на ней языке.

## Как добавить модель

По записи на каждое квантование в `SpeechModelCatalog` (семейство = имя папки, квантование, адрес HTTPS, точный
размер, SHA-256) в порядке каталога: семейства от самого тяжёлого к самому лёгкому, квантования от самого точного к
самому маленькому. Текст `Model.<семейство>.Description` — во всех пакетах; новому квантованию нужны ещё
`Quantization.<значение>.Name` и `.Description`. Лицензия модели — в `THIRD-PARTY-NOTICES.txt`.

Модель голоса — запись в `VoiceModelCatalog`: её id (имя папки), веса и проектор (`mmproj`) из GGUF, опубликованного
для llama.cpp, каждый с фиксированной ревизии, со своим точным размером и SHA-256, её языки (первый — по умолчанию),
голоса, которые она несёт, умеет ли она говорить голосом образца, нужен ли он ей и хорошо ли она работает на
процессоре. Текст `VoiceModel.<id>.Description` и `SpeechLanguage.<код>` нового языка — во всех пакетах; выпуск
llama.cpp в `LlamaCppPackage` должен поддерживать модель. Новый выпуск llama.cpp закрепляется там же, с размером и
SHA-256 каждого архива.

## Пакеты и страница проекта

См. [Scripts/README.md](../../Scripts/README.md). Пакет несёт только нативные библиотеки своей платформы; моделей,
FFmpeg и llama.cpp в нём нет — пользователь скачивает их из настроек.

GitHub Actions: `ci.yml` собирает и тестирует на Windows и Linux при каждом пуше в `main` и в каждом pull request;
`packages.yml` собирает все пакеты вручную или по тегу версии и прикладывает их к черновику выпуска; `pages.yml`
публикует страницу проекта (`Sources/Apps/Voicer.Pages`, см. её README) при каждом её изменении в `main`.
