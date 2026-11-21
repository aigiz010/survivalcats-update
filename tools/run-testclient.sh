#!/usr/bin/env bash
# Тестовый клиент Vintage Story со своей папкой данных и сохранённым входом из основной игры.
#   bash tools/run-testclient.sh [тестовая папка данных]   (по умолчанию F:\Vintagestory\testdata-hbo)
# Моды теста — в <папка>\Mods. Основная папка данных не меняется: из неё только читается clientsettings.json.
set -e
cd "$(dirname "$0")/.."
GAME="F:/Vintagestory/Games1.18"
MAIN_DATA="$APPDATA/VintagestoryData"
TEST_DATA="${1:-F:/Vintagestory/testdata-hbo}"

mkdir -p "$TEST_DATA/Mods"
# Каждый раз из основной папки: вход живёт там. Если ключ устарел и игра просит пароль, после входа она
# перезапускает себя уже БЕЗ --dataPath, то есть с основной папкой и основными мирами. Тогда: «Выйти» в главном
# меню (игра запишет новый ключ в основной clientsettings.json) и снова этот скрипт.
py -3 tools/testclient_settings.py "$MAIN_DATA" "$TEST_DATA"
"$GAME/Vintagestory.exe" --dataPath "$(cygpath -w "$TEST_DATA")" > /dev/null 2>&1 &
echo "запущен: $GAME/Vintagestory.exe --dataPath $TEST_DATA"
