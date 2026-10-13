# Manual test checklist

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

## Install (use samples/msc-demo)
- [ ] «Установить»: progress bar, step text, sidebar mini-progress, «Готово!», toast.
- [ ] `<game>\ModCrew-demo\README.txt` and `settings.ini` (`[Demo] InstalledBy=ModCrew`) exist.
- [ ] «Удалить из игры» → confirm → `ModCrew-demo` is gone.
- [ ] A failing step (e.g. replaceText with missing text) shows «Не получилось» and leaves the game folder unchanged.
- [ ] Installing a second pack for the same game asks to remove the first.
- [ ] A `run` step asks before launching; «Отмена» rolls back.
- [ ] Pack for a game that is not installed: «Игра не найдена», install disabled.
