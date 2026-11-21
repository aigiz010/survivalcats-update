# Survival Categories Update — bug reports / сообщения об ошибках

Mod page / страница мода: https://mods.vintagestory.at/survivalcatsupdate

## English

This repository is the bug tracker for the **Survival Categories Update** mod for Vintage Story
(an update of Survival Categories by ZigTheHedge).

Found a bug? Open an issue: **Issues → New issue → Bug report**.
Please include the game version, the mod version, the list of other mods and the client log
`%APPDATA%\VintagestoryData\Logs\client-main.log` (Linux: `~/.config/VintagestoryData/Logs/`).

## Русский

Этот репозиторий — трекер ошибок мода **Survival Categories Update** для Vintage Story
(обновление Survival Categories от ZigTheHedge).

Нашли ошибку? Создайте issue: **Issues → New issue → Bug report**.
Укажите версию игры, версию мода, список остальных модов и приложите лог клиента
`%APPDATA%\VintagestoryData\Logs\client-main.log` (Linux: `~/.config/VintagestoryData/Logs/`).

## Source code / исходники

`src/` — mod code, `assets/` — translations, `survivalcats.csproj` — build, `tools/` — compatibility checks.
The mod is built for .NET 7 against the API of game 1.18.8 (the oldest supported version) and runs on 1.18.8–1.22.x;
version differences are handled via reflection in `src/Compat.cs`. Game dlls are not in the repository:
put `VintagestoryAPI.dll`, `Mods/VSSurvivalMod.dll` and `Lib/*.dll` of the game into `gamerefs/1.18.8/`, then run
`dotnet build -c Release` (another version: `-p:GameDir=gamerefs/<version>`).

`src/` — код мода, `assets/` — переводы, `survivalcats.csproj` — сборка, `tools/` — проверки совместимости.
Мод собирается под .NET 7 против API игры 1.18.8 (самой старой поддерживаемой) и работает на 1.18.8–1.22.x;
отличия версий обрабатываются отражением в `src/Compat.cs`. dll игры в репозиторий не входят:
положите `VintagestoryAPI.dll`, `Mods/VSSurvivalMod.dll` и `Lib/*.dll` игры в `gamerefs/1.18.8/` и запустите
`dotnet build -c Release` (другая версия: `-p:GameDir=gamerefs/<версия>`).
