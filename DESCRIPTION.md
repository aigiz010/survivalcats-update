# Survival Categories Update

## English

An update of **Survival Categories** by **ZigTheHedge**.
Original mod: https://mods.vintagestory.at/show/mod/67 — all credit for the idea and the original code goes to its author.

### Features

- **Creative inventory tabs in the survival handbook.** A second column of tabs on the right of the handbook, in the same order and with the same names as in the creative inventory: Flora, Terrain, Decorative… and tabs added by mods (Expanded Foods, Culinary Artillery, etc.). A tab shows only the items the creative inventory puts into it.
- **Page preview on hover.** Hold the cursor over a handbook entry and a window with the whole page appears next to it: picture, description, ingredients. Move the cursor into the window and it stays open: scroll it with the mouse wheel or the scrollbar, hover icons for tooltips, click an item or a link to open its page.
- **Bookmarks.** The "Bookmark" button on an item page adds it to the "Bookmarks" tab (saved per world).
- **Search prefixes:**
  - `@modid` — items of a mod, e.g. `@expandedfoods` or `@expandedfoods pie`;
  - `#tab` — items of a creative inventory tab;
  - `$code` — items whose code contains the text, e.g. `$iron`.
- **Clear button** "×" next to the handbook search field.
- **One handbook window.** The item list and the item page share "Movable" / "Fixed" (the ≡ menu), position and size.
- **Mod name in item tooltips:** "Mod: name (@modid)".

### Settings

`ModConfig/SurvivalCatsConfig.json`:

| Setting | Default | Meaning |
|---|---|---|
| `addModNameToInfo` | `true` | mod name in item tooltips |
| `displayVanillaTabsAsCategories` | `true` | vanilla creative tabs in the handbook |
| `displayModsAsCategories` | `true` | creative tabs added by mods |
| `hoverPreview` | `true` | page preview on hover |
| `hoverPreviewDelayMs` | `250` | delay before the preview appears, ms |
| `hoverPreviewHeightLimit` | `0` | max preview height; `0` = screen height |

### Compatibility

Tested together with:

- **Handbook Optimizer** (0.19.2) — since 1.1.1.

Client-side mod.

---

## Русский

Обновление мода **Survival Categories** от **ZigTheHedge**.
Оригинал: https://mods.vintagestory.at/show/mod/67 — идея и исходный код принадлежат его автору.

### Возможности

- **Вкладки творческого инвентаря в справочнике выживания.** Вторая колонка вкладок справа от справочника — в том же порядке и с теми же названиями, что в творческом инвентаре: «Флора», «Ландшафт», «Декоративное»… и вкладки модов («Expanded Foods», «Кулинарная артиллерия» и др.). Вкладка показывает только предметы, которые творческий инвентарь кладёт в неё.
- **Предпросмотр страницы при наведении.** Задержите курсор на строке справочника — рядом появится окно со всей страницей: картинка, описание, ингредиенты. Если увести курсор в окно, оно останется: прокрутка колесом или полосой, подсказки на иконках, щелчок по предмету или ссылке открывает его страницу.
- **Закладки.** Кнопка «В закладки» на странице предмета добавляет его во вкладку «Закладки» (отдельно для каждого мира).
- **Префиксы поиска:**
  - `@modid` — предметы мода, например `@expandedfoods` или `@expandedfoods пирог`;
  - `#вкладка` — предметы вкладки творческого инвентаря;
  - `$код` — предметы, в коде которых есть текст, например `$iron`.
- **Кнопка «×»** справа от поля поиска справочника стирает текст.
- **Одно окно справочника.** У списка и страницы предмета общие «Перемещаемое» / «Закреплено» (меню ≡), положение и размер.
- **Название мода в подсказке предмета:** «Mod: название (@modid)».

### Настройки

`ModConfig/SurvivalCatsConfig.json`:

| Параметр | По умолчанию | Что делает |
|---|---|---|
| `addModNameToInfo` | `true` | название мода в подсказке предмета |
| `displayVanillaTabsAsCategories` | `true` | ванильные вкладки творческого в справочнике |
| `displayModsAsCategories` | `true` | вкладки творческого, добавленные модами |
| `hoverPreview` | `true` | предпросмотр страницы при наведении |
| `hoverPreviewDelayMs` | `250` | задержка перед показом окна, мс |
| `hoverPreviewHeightLimit` | `0` | предел высоты окна; `0` — по высоте экрана |

### Совместимость

Проверено вместе с:

- **Handbook Optimizer** (0.19.2) — с версии 1.1.1.

Мод клиентский.
