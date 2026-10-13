# ModCrew

Мод-органайзер для своей компании: автор собирает **сборку** (набор модов + инструкции установки),
друзья добавляют файл `.modcrew` и жмут «Установить». ModCrew сам находит игру в Steam, всё скачивает,
распаковывает и раскладывает, а «Удалить из игры» возвращает папку игры точно как было.

## Стек
- **C# / .NET 8, WinForms + WebView2**: окно, вся логика (Steam, установка, журнал отката).
- **Svelte 5 + Vite**: интерфейс (`ui/`), собирается в `wwwroot` рядом с exe.

## Папка ModCrewFinal
```
ModCrewFinal\
  Source\      этот репозиторий
  App\         ModCrew.exe — готовая программа, запускается без установки
  Installer\   ModCrew-Setup.exe — установщик для друзей
```
`powershell -ExecutionPolicy Bypass -File build-final.ps1` — тесты, сборка и обновление App\ и Installer\.

**Установщик** — тот же exe под именем `ModCrew-Setup.exe` (имя включает режим установки). Ставит без прав
администратора в `%LOCALAPPDATA%\Programs\ModCrew`, ярлык в «Пуск» (и на рабочем столе по желанию),
связывает `.modcrew`, добавляет запись в «Параметры → Приложения» (оттуда же удаляется).

## Обновления (GitHub Releases)
Новая версия = тег. GitHub Actions (`.github/workflows/release.yml`) сам соберёт и выложит релиз:
```
git tag v0.2.1
git push origin master --tags
```
В релизе: `ModCrew-Setup.exe` (для новых), `ModCrew.exe` (его качают обновления), `ModCrew.exe.sha256`.
ModCrew проверяет `qniman/ModCrew` через 4 секунды после запуска и потом каждые 6 часов. С автообновлением
(по умолчанию) сам скачивает новый exe, проверяет sha256 и подменяет себя — новая версия включается при
перезапуске (кнопка в боковой панели). Без него — только показывает, что вышла новая версия.
Сборки из исходников (`bin\…`) не обновляются.

## Разработка
```
dotnet build src/ModCrew -c Release        # соберёт и UI (npm install при первом запуске)
dotnet test tests/ModCrew.Tests
```
Нужны .NET SDK 8+ и Node.js.
`-p:SkipUi=true` собирает только C#.

**Разработка интерфейса:** `cd ui && npm run dev`: страница на http://localhost:5173 с мок-данными
(`ui/src/lib/mock.js`), без exe. Чтобы exe грузил живой dev-сервер: `set MODCREW_DEV_URL=http://localhost:5173`.

## Как сделать сборку
«Сборки» → «Новая сборка» → выбрал игру → перетащи файлы модов в окно или «Мод из файла» (zip/dll/exe с диска,
в том числе скачанные с Nexus), или «По ссылке» (прямая https-ссылка). ModCrew заглянет в файл и предложит
«Положить в папку игры» (подскажет куда) или «Запустить установщик» (для модов вроде BeamMP), и сам составит шаги;
шаги можно поправить вручную. Ошибки подсвечиваются сразу. «Установить и проверить» → «Сохранить .modcrew» → друзьям.

## Формат сборки
Папка с `pack.json` (+ обложка, + `files/` с модами). `.modcrew` = эта папка в zip.
Пример: [`samples/msc-demo`](samples/msc-demo/pack.json).

```jsonc
{
  "id": "msc-friends", "name": "MSC с друзьями", "version": "1.2.0",
  "game": { "steamAppId": 516750 },
  "cover": "cover.jpg",
  "mods": [
    { "name": "MSCLoader", "version": "1.4.2", "steps": [
      { "action": "download", "url": "https://…/MSCLoader.zip", "sha256": "…", "saveAs": "loader.zip" },
      { "action": "extract", "from": "loader.zip", "to": "loader" },
      { "action": "copy", "from": "loader/Mods", "to": "{game}/Mods" }
    ]}
  ]
}
```

| action | поля | |
|---|---|---|
| `download` | `url` (https), `sha256?`, `saveAs?` | только во временную папку; по sha256 кэшируется |
| `extract` | `from` (.zip), `to?` | |
| `copy` | `from` (файл/папка), `to`, `overwrite?` | `to` со `/` на конце = «в эту папку» |
| `delete` | `path` | |
| `replaceText` | `file`, `find`, `replace` | ошибка, если `find` не найден |
| `setIni` | `file`, `section?`, `key`, `value` | |
| `run` | `path`, `args?` | спрашивает игрока; откатить нельзя |

Пути: `{game}` `{pack}` `{work}` `{documents}` `{appdata}` `{localappdata}` `{locallow}`; относительный путь = `{work}`.
Писать можно только в папку игры, `{work}` и подпапки пользовательских папок, но не в данные ModCrew.

## Данные
`%LOCALAPPDATA%\ModCrew\`: `packs\<id>` (сборки), `installs\<id>` (`install.json` с журналом + `backup\`),
`cache\` (загрузки, временные папки), `settings.json` (ручные пути игр), `webview\`.
