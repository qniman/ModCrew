# Manual test checklist

## Installer & updates
- [ ] `Installer\ModCrew-Setup.exe`: setup window (no admin prompt), «Установить» → «Готово»; Start menu shortcut, optional desktop one, `.modcrew` opens the installed app, entry in «Параметры → Приложения» with the version.
- [ ] Running setup again: «Уже установлена версия …» → reinstall over it, also while ModCrew is open.
- [ ] «Удалить» in «Приложения»: asks to close ModCrew if open, asks about data, removes shortcuts, folder, association.
- [ ] With a newer GitHub release: sidebar shows «Новая версия» / auto-downloads; «Перезапустить» starts the new version (Settings shows it), `ModCrew.exe.old` is gone after that start.
- [ ] Auto-update off: only the «Новая версия → Обновить» card. «Проверить» in Settings: «последняя версия» / «есть новая».
- [ ] Offline: no errors shown on start.

## Play & repair
- [ ] Installed pack: big «Играть»; with an update: «Обновить» + «Играть так». «Переустановить» is in the side links.
- [ ] «Играть» with intact files: «Проверяю моды…» briefly, then Steam starts the game.
- [ ] Delete a mod file / replace a game file the pack patched (simulates a Steam update) → «Часть модов пропала» with the list → «Починить и играть» → progress «Починка» → game starts; files back; «Удалить из игры» afterwards keeps the updated game file.
- [ ] «Мои игры»: poster overlay «Играть» (pack installed) / «Запустить».

## Save backups
- [ ] MSC pack page: «Сохранения» with «Папка», «Бэкап сейчас»; list shows date, reason, size.
- [ ] Install / update / repair / uninstall make an automatic backup («перед установкой «…» 1.2.0»); only the last 15 automatic ones are kept, manual ones stay.
- [ ] «Вернуть»: refuses while the game runs; afterwards saves are as in the backup, and a «перед восстановлением» backup appeared.
- [ ] Game without a known save folder: hint with «Открыть редактор»; editor field «Папка сохранений» (shows the default for known games); `{game}` alone or `{work}/…` → error in checks.

## Settings
- [ ] Theme dark / light / «Как в Windows» (follows Windows switching live), title bar colour follows.
- [ ] Accent colours recolour buttons, logo, toggles, nav; pack hero stays readable in light theme.
- [ ] Scale 90–125% applies at once and after restart; Ctrl+wheel does not change it.
- [ ] Animations off: no fades/slides.

## Friends' side
- [ ] `publish\ModCrew.exe` runs on a PC without .NET (single file, icon in Explorer/taskbar).
- [ ] After the first start, double-clicking a `.modcrew` opens ModCrew on that pack («Сборка … добавлена»); `.modcrew` files show the ModCrew icon.
- [ ] Double-click with ModCrew already open: same window comes to front, no second window.
- [ ] Dragging a `.modcrew` onto the window: drop overlay, pack added and opened. Other files outside the editor: hint toast.
- [ ] Pack with an installer: pack page says an installer will open; install asks «Сейчас откроется установщик», waits for it, then «Готово».
- [ ] Installer: UAC «Нет» → «Установщику не дали права администратора», game folder rolled back. Cancel in an MSI → «Установку … отменили».
- [ ] «Удалить из игры» on such a pack mentions removing the program via «Параметры → Приложения».

## Start
- [ ] App opens with a dark title bar, no white flash; sidebar + «Сборки».
- [ ] «Мои игры» lists Steam games with posters; games without a local poster load it from the CDN or show initials.
- [ ] ↻ rescans Steam; search filters.
- [ ] «Указать папку…» saves a custom folder (shows «своя папка»), survives restart.

## Packs
- [ ] «Добавить сборку» imports a `.modcrew`; the pack opens.
- [ ] «Из папки» imports `samples/msc-demo`.
- [ ] Broken pack.json (bad id, no game) shows a clear error, nothing is added.
- [ ] Re-importing a newer version of an installed pack shows «Есть обновление» / «Обновить».
- [ ] «Сохранить .modcrew» writes the file and opens Explorer on it; that file imports on another PC.
- [ ] «Убрать из списка» is disabled while the pack is installed.

## Pack editor
- [ ] «Новая сборка»: name + game poster grid; «Создать» opens the editor; id is a transliterated slug.
- [ ] Pack detail → «Редактировать» opens the editor with the pack's mods.
- [ ] «Мод из файла» with a zip that has a wrapper folder (`Mod-1.0/Mods/…`): suggests that folder → `{game}`, name/version guessed from the file name; steps preview = extract + copy.
- [ ] Same with a zip laid out like the game (`Mods/…`): whole archive → `{game}`, one extract step.
- [ ] `.dll`: copy → `{game}/Mods/` when the game has a Mods folder.
- [ ] Cancel in the wizard removes the copied file from the pack (no «не используется» warning).
- [ ] «По ссылке» with a GitHub release URL: progress, sha256 filled, download step first. http:// is refused.
- [ ] Zip with `*Installer.exe`/`setup.exe` (e.g. BeamMP): wizard preselects «Запустить установщик», mod name without "Installer", steps = extract + run. A lone `.exe`/`.msi` → run directly.
- [ ] «Запустить установщик» is disabled for files without a program inside.
- [ ] Dropping zip/dll/exe files onto the editor opens the wizard for each in turn; cancel removes the copied file.
- [ ] «По ссылке» with a site page (https://beammp.com/) → «открывается веб-страница, а не файл».
- [ ] Run step «Обзор»: «Из предыдущих шагов» lists the downloaded/unpacked programs; a run path no earlier step creates is an error.
- [ ] Step chips add an open, empty step; up/down/delete work; mods reorder by dragging the grip.
- [ ] Path field menu: pack files list, «Добавить файл в сборку…», «Выбрать папку в игре…» returns `{game}/…`; a folder outside the game/user folders is refused with a message.
- [ ] Problems update within a second: empty required field (red), missing pack file, absolute path, unknown `{var}`, unused file (with «Удалить»).
- [ ] Unsaved dot in the top bar; leaving via sidebar/«Готово» asks before discarding.
- [ ] Saving an installed pack offers a version bump; after it the pack shows «Есть обновление».
- [ ] «Своя обложка» replaces the cover (card and detail show the new image right away).
- [ ] «Установить и проверить» saves, opens the pack and installs.

## Install (use samples/msc-demo)
- [ ] «Установить»: progress bar, step text, sidebar mini-progress, «Готово!», toast.
- [ ] `<game>\ModCrew-demo\README.txt` and `settings.ini` (`[Demo] InstalledBy=ModCrew`) exist.
- [ ] «Удалить из игры» → confirm → `ModCrew-demo` is gone.
- [ ] A failing step (e.g. replaceText with missing text) shows «Не получилось» and leaves the game folder unchanged.
- [ ] Installing a second pack for the same game asks to remove the first.
- [ ] A `run` step asks before launching; «Отмена» rolls back.
- [ ] Pack for a game that is not installed: «Игра не найдена», install disabled.
