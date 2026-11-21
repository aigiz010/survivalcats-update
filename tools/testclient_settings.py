"""Копия clientsettings.json основной игры в тестовую папку данных, с modPaths на её Mods.

Нужна, чтобы тестовый клиент (--dataPath <папка>) входил в аккаунт сам: сохранённый вход
лежит в clientsettings.json. Остальные настройки (графика, язык) копируются как есть.

py -3 tools/testclient_settings.py <основная папка данных> <тестовая папка данных>
"""
import json
import os
import sys


def set_mod_paths(node, mods_dir):
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "modPaths" and isinstance(value, list):
                node[key] = ["Mods", mods_dir]
                return True
            if set_mod_paths(value, mods_dir):
                return True
    elif isinstance(node, list):
        for value in node:
            if set_mod_paths(value, mods_dir):
                return True
    return False


def main():
    src_dir, dst_dir = sys.argv[1], sys.argv[2]
    src = os.path.join(src_dir, "clientsettings.json")
    dst = os.path.join(dst_dir, "clientsettings.json")
    with open(src, "r", encoding="utf-8-sig") as f:
        data = json.load(f)
    mods_dir = os.path.abspath(os.path.join(dst_dir, "Mods"))
    if not set_mod_paths(data, mods_dir):
        print("modPaths не найден в " + src)
        return 1
    os.makedirs(dst_dir, exist_ok=True)
    with open(dst, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print("clientsettings.json -> " + dst + " (modPaths: Mods, " + mods_dir + ")")
    return 0


if __name__ == "__main__":
    sys.exit(main())
