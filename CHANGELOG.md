# Survival Categories Update — changelog

## 1.1.1

### English
- Compatibility with **Handbook Optimizer**:
  - fixed a crash (NullReferenceException) when closing the handbook;
  - Handbook Optimizer's fast handbook loading is no longer switched off by this mod;
  - creative inventory tabs and search prefixes (`@mod`, `#tab`, `$code`) work together with it.
- Fixed: after the hover preview of a guide page, links and key hints on the opened page could turn into black boxes.
- Bookmarks are added after the handbook pages have loaded; links on pages now lead to the original page, not to its bookmark copy.

### Русский
- Совместимость с **Handbook Optimizer**:
  - исправлен вылет (NullReferenceException) при закрытии справочника;
  - мод больше не отключает быструю загрузку справочника у Handbook Optimizer;
  - вкладки творческого инвентаря и префиксы поиска (`@мод`, `#вкладка`, `$код`) работают вместе с ним.
- Исправлено: после предпросмотра страницы-руководства ссылки и подсказки клавиш на открытой странице могли стать чёрными прямоугольниками.
- Закладки добавляются после загрузки страниц справочника; ссылки на страницах ведут на исходную страницу, а не на её копию в закладках.

## 1.1.0

### English
- One file for game versions 1.18.8 … 1.22.x (checked against 1.18.8, 1.18.15, 1.19.8, 1.20.12, 1.21.6, 1.22.7).
- If some part of the mod does not fit a particular game version, only that part is switched off (with a line in the client log) instead of the whole mod.
- Mod icon and a shorter description in the in-game mod list.
- No changes in features.

### Русский
- Один файл для версий игры 1.18.8 … 1.22.x (проверено на 1.18.8, 1.18.15, 1.19.8, 1.20.12, 1.21.6, 1.22.7).
- Если какая-то часть мода не подходит к версии игры, отключается только она (с записью в логе клиента), а не весь мод.
- Иконка мода и короткое описание в списке модов игры.
- Возможности не менялись.

## 1.0.5

### English
- Handbook windows: the item list and the item page now behave as one window. "Movable" / "Fixed" (the ≡ menu) and the position are shared: unpin or move one and the other follows.
- "Fixed" now returns the window to its place immediately (previously the handbook had to be reopened).
- The item list window no longer grows every time it is unpinned or dragged, and it has the same size as the item page window.
- The second column of creative inventory tabs stays at the right edge of the window when the window is unpinned or moved.
- The "×" clear button now sits right next to the search field.
- Hover preview closes immediately when the cursor leaves the entry (it still stays open while you move the cursor into it).
- Fixed: the item list could overflow below its frame.

### Русский
- Окна справочника: список предметов и страница предмета теперь ведут себя как одно окно. «Перемещаемое» / «Закреплено» (меню ≡) и положение общие: открепили или передвинули одно — второе следует за ним.
- «Закреплено» сразу возвращает окно на место (раньше нужно было переоткрыть справочник).
- Окно списка больше не растёт при каждом откреплении и перетаскивании и совпадает по размеру с окном страницы предмета.
- Вторая колонка вкладок творческого инвентаря держится у правого края окна, когда окно откреплено или передвинуто.
- Кнопка очистки поиска «×» стоит сразу справа от поля поиска.
- Окно предпросмотра закрывается сразу, как только курсор ушёл со строки (и по-прежнему остаётся, если увести курсор в него).
- Исправлено: список предметов мог вылезать за нижний край своей рамки.

## 1.0.0

### English
- Ported Survival Categories by ZigTheHedge to the current game.
- Creative inventory tabs in the survival handbook (second column).
- Page preview on hover, interactive.
- Clear button in the search field.
- Bookmarks and @mod / #tab / $code search reworked.

### Русский
- Перенос Survival Categories от ZigTheHedge на текущую версию игры.
- Вкладки творческого инвентаря в справочнике (вторая колонка).
- Предпросмотр страницы при наведении, с прокруткой и переходами.
- Кнопка очистки в поле поиска.
- Закладки и поиск @мод / #вкладка / $код переделаны.
