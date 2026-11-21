using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>
/// Всплывающее окно у курсора при наведении на строку списка справочника: страница целиком, без кнопок и заголовка.
/// Если увести курсор в окно, оно остаётся и работает как обычное окно: прокрутка колесом и полосой,
/// подсказки на иконках, щелчок по предмету или ссылке открывает его страницу в справочнике.
/// Окно — часть справочника (рисуется в его OnRenderGUI), события мыши в своей области справочник отдаёт окну.
/// </summary>
internal static class HoverPreview
{
	private const double Width = 500.0;
	private const double Pad = 8.0;
	private const double ScrollbarGap = 7.0;
	private const double ScrollbarWidth = 20.0;
	private const float Z = 400f;


	/// <summary>
	/// Иконки предметов в richtext рисуются с фиксированной глубиной 100 — ниже фона окна (Z).
	/// Пока рисуется окно, глубина иконок поднимается на ItemZOffset (см. PatcherRichtextItemZ).
	/// </summary>
	private const float ItemZOffset = 450f;

	private static bool rendering;

	public static float ItemZ(float z) => rendering ? z + ItemZOffset : z;

	public static double ItemZScaled(double z) => rendering ? z + GuiElement.scaled(ItemZOffset) : z;

	public static double ItemZd(double z) => rendering ? z + ItemZOffset : z;

	private static GuiComposer composer;
	private static GuiElement content;
	private static GuiHandbookPage shownPage;
	private static GuiHandbookPage failedPage;
	private static GuiHandbookPage candidate;
	private static long candidateSince;
	private static long lastAnchoredMs;
	private static bool mouseCaptured;
	private static string pendingOpen;

	public static void Hide()
	{
		// Руководства (GuiHandbookTextPage) создают элементы текста один раз и отдают одни и те же в каждое окно.
		// Dispose окна уничтожил бы их и на открытой странице справочника — ссылки и клавиши там стали бы чёрными.
		// Такие элементы отвязываются; уничтожаются только созданные заново для этого окна.
		if (content is GuiElementRichtext richtext && !CreatesOwnComponents(shownPage))
		{
			richtext.Components = new RichTextComponentBase[0];
		}
		composer?.Dispose();
		composer = null;
		content = null;
		shownPage = null;
		mouseCaptured = false;
	}

	/// <summary>Страница собирает элементы текста заново при каждом ComposePage (предметы, блюда, обучение, команды).</summary>
	private static bool CreatesOwnComponents(GuiHandbookPage page)
	{
		if (page is GuiHandbookItemStackPage) return true;
		string name = page?.GetType().Name;
		return name == "GuiHandbookMealRecipePage" || name == "GuiHandbookTutorialPage" || name == "GuiHandbookCommandPage";
	}

	private static bool ContainsMouse(int x, int y) => composer?.Bounds != null && composer.Bounds.PointInside(x, y);

	// Где был курсор, когда окно появилось (экранные пиксели).
	private static double anchorX;

	/// <summary>
	/// «Мостик» между точкой появления окна и его ближним краем по всей высоте окна:
	/// пока курсор в нём, он едет к окну (может пройти над соседними строками), и окно не закрывается.
	/// </summary>
	private static bool InBridge(int x, int y)
	{
		ElementBounds b = composer?.Bounds;
		if (b == null) return false;
		double left = b.absX;
		double right = b.absX + b.OuterWidth;
		if (y < b.absY || y > b.absY + b.OuterHeight) return false;
		// Курсор должен сдвинуться к окну: при движении по списку вверх-вниз x почти не меняется — тогда закрыть.
		return left >= anchorX
			? x > anchorX + 4 && x <= left + 2
			: x >= right - 2 && x < anchorX - 4;
	}

	/// <summary>Окно забирает событие, если курсор над ним или идёт перетаскивание, начатое в нём.</summary>
	private static bool Owns(GuiDialog dlg, int x, int y) =>
		composer != null && Compat.IsSurvivalHandbook(dlg) && (mouseCaptured || ContainsMouse(x, y));

	public static bool OnMouseDown(GuiDialog dlg, MouseEvent args)
	{
		if (!Owns(dlg, args.X, args.Y)) return true;
		mouseCaptured = true;
		composer.OnMouseDown(args);
		args.Handled = true;
		return false;
	}

	public static bool OnMouseUp(GuiDialog dlg, MouseEvent args)
	{
		if (!Owns(dlg, args.X, args.Y)) return true;
		mouseCaptured = false;
		composer.OnMouseUp(args);
		args.Handled = true;
		return false;
	}

	public static bool OnMouseMove(GuiDialog dlg, MouseEvent args)
	{
		if (!Owns(dlg, args.X, args.Y)) return true;
		composer.OnMouseMove(args);
		args.Handled = true;
		return false;
	}

	public static bool OnMouseWheel(GuiDialog dlg, ICoreClientAPI capi, MouseWheelEventArgs args)
	{
		if (capi == null || !Owns(dlg, capi.Input.MouseX, capi.Input.MouseY)) return true;
		composer.OnMouseWheel(args);
		args.SetHandled();
		return false;
	}

	public static void OnRender(GuiDialogHandbook dlg, ICoreClientAPI capi, GuiComposer overviewGui, float deltaTime)
	{
		// Щелчок по предмету в окне: открыть его страницу (не внутри обработки события окна — там окно ещё живо).
		if (pendingOpen != null)
		{
			string code = pendingOpen;
			pendingOpen = null;
			Hide();
			dlg.OpenDetailPageFor(code);
			return;
		}

		ModConfigFile cfg = ModConfigFile.Current;
		if (cfg?.hoverPreview != true || overviewGui == null || dlg.SingleComposer != overviewGui)
		{
			Hide();
			candidate = null;
			return;
		}

		long now = capi.ElapsedMilliseconds;
		int mouseX = capi.Input.MouseX;
		int mouseY = capi.Input.MouseY;

		// Курсор в окне (или тащит его полосу прокрутки) — окно остаётся.
		if (composer != null && (mouseCaptured || ContainsMouse(mouseX, mouseY)))
		{
			lastAnchoredMs = now;
			candidate = shownPage;
			candidateSince = now;
			Render(deltaTime);
			return;
		}

		GuiHandbookPage page = HoveredPage(capi, overviewGui.GetFlatList("stacklist"));
		if (page != candidate)
		{
			candidate = page;
			candidateSince = now;
		}

		if (composer != null)
		{
			if (page == shownPage)
			{
				lastAnchoredMs = now;
			}
			else if (InBridge(mouseX, mouseY))
			{
				Render(deltaTime); // курсор едет от строки к окну
				return;
			}
			else
			{
				Hide(); // ушёл со строки не к окну — закрыть сразу
			}
		}

		if (page == null) return;

		if (composer == null)
		{
			if (page == failedPage) return;
			if (now - candidateSince < Math.Max(0, cfg.hoverPreviewDelayMs)) return;
			try
			{
				Show(capi, page, cfg.hoverPreviewHeightLimit);
				lastAnchoredMs = now;
			}
			catch (Exception e)
			{
				capi.Logger.Warning("[survivalcats] Не удалось показать окно для {0}: {1}", page.PageCode, e);
				Hide();
				failedPage = page; // не пытаться заново каждый кадр
				return;
			}
		}

		Render(deltaTime);
	}

	private static void Render(float deltaTime)
	{
		if (composer == null) return;
		rendering = true;
		try
		{
			composer.Render(deltaTime);
			composer.PostRender(deltaTime);
		}
		finally
		{
			rendering = false;
		}
	}

	/// <summary>Та же проверка, что подсветка строки в GuiElementFlatList.RenderInteractiveElements.</summary>
	private static GuiHandbookPage HoveredPage(ICoreClientAPI capi, GuiElementFlatList list)
	{
		if (list?.Bounds?.ParentBounds == null || list.insideBounds == null) return null;
		int mouseX = capi.Input.MouseX;
		int mouseY = capi.Input.MouseY;
		if (!list.Bounds.ParentBounds.PointInside(mouseX, mouseY)) return null;
		if (mouseX <= list.Bounds.absX || mouseX > list.Bounds.absX + list.Bounds.InnerWidth) return null;

		double y = list.insideBounds.absY;
		double yPad = GuiElement.scaled(list.unscalledYPad);
		double cellHeight = GuiElement.scaled(list.unscaledCellHeight);
		double step = GuiElement.scaled(list.unscaledCellHeight + list.unscaledCellSpacing);
		foreach (IFlatListItem element in list.Elements)
		{
			if (!element.Visible) continue;
			double top = 5.0 + list.Bounds.absY + y;
			if (mouseY >= top - yPad && mouseY <= top + cellHeight - yPad)
			{
				return element as GuiHandbookPage;
			}
			y += step;
		}
		return null;
	}

	private static void OnScroll(float value)
	{
		if (content?.Bounds == null) return;
		content.Bounds.fixedY = -value;
		content.Bounds.CalcWorldBounds();
	}

	private static void Show(ICoreClientAPI capi, GuiHandbookPage page, double heightLimit)
	{
		shownPage = page;
		anchorX = capi.Input.MouseX;
		ItemStack[] allStacks = Compat.AllHandbookStacks(capi);

		// Предел — свободная высота экрана (с полями), либо меньший предел из конфига. Что не влезло — прокруткой.
		double screenLimit = capi.Render.FrameHeight / RuntimeEnv.GUIScale - 2 * Pad - 16.0;
		double maxHeight = heightLimit > 0 ? Math.Min(heightLimit, screenLimit) : screenLimit;
		maxHeight = Math.Max(100.0, maxHeight);

		ElementBounds textBounds = ElementBounds.Fixed(0.0, 0.0, Width, 30.0);
		ElementBounds clipBounds = textBounds.ForkBoundingParent();
		clipBounds.fixedHeight = maxHeight;
		ElementBounds scrollbarBounds = ElementBounds.Fixed(Width + ScrollbarGap, 0.0, ScrollbarWidth, maxHeight);
		ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(Pad);
		bgBounds.BothSizing = ElementSizing.FitToChildren;
		bgBounds.WithChildren(clipBounds, scrollbarBounds);
		ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.None).WithFixedPosition(0.0, 0.0);

		composer = capi.Gui.CreateCompo("survivalcats-hoverpreview", dialogBounds)
			.AddShadedDialogBG(bgBounds, withTitleBar: false)
			.BeginChildElements(bgBounds)
			.BeginClip(clipBounds);
		page.ComposePage(composer, textBounds, allStacks, code =>
		{
			pendingOpen = code;
			return true;
		});
		content = composer.LastAddedElement;
		composer.EndClip()
			.AddVerticalScrollbar(OnScroll, scrollbarBounds, "scrollbar")
			.EndChildElements();
		composer.zDepth = Z;
		if (content is GuiElementRichtext rt) rt.zPos = Z;
		composer.Compose(focusFirstElement: false);

		// Высота содержимого: текст и «плавающие» элементы (картинка предмета слева бывает выше короткого текста).
		double contentHeight = content?.Bounds?.fixedHeight ?? maxHeight;
		if (content is GuiElementRichtext richtext && richtext.Components != null)
		{
			foreach (RichTextComponentBase comp in richtext.Components)
			{
				if (comp?.BoundsPerLine == null) continue;
				foreach (LineRectangled line in comp.BoundsPerLine)
				{
					contentHeight = Math.Max(contentHeight, (line.Y + line.Height) / RuntimeEnv.GUIScale + 4.0);
				}
			}
		}
		double height = Math.Max(40.0, Math.Min(contentHeight, maxHeight));
		clipBounds.fixedHeight = height;
		scrollbarBounds.fixedHeight = height;

		// Положение: справа от курсора, при нехватке места — слева; не выходить за экран.
		double scale = RuntimeEnv.GUIScale;
		double screenW = capi.Render.FrameWidth / scale;
		double screenH = capi.Render.FrameHeight / scale;
		double mx = capi.Input.MouseX / scale;
		double my = capi.Input.MouseY / scale;
		double w = Width + ScrollbarGap + ScrollbarWidth + 2 * Pad;
		double h = height + 2 * Pad;
		double x = mx + 24.0;
		if (x + w > screenW) x = mx - 24.0 - w;
		double y = my - 20.0;
		if (y + h > screenH - 8.0) y = screenH - 8.0 - h;
		dialogBounds.fixedX = Math.Max(0.0, x);
		dialogBounds.fixedY = Math.Max(0.0, y);

		dialogBounds.MarkDirtyRecursive();
		composer.ReCompose();
		composer.GetScrollbar("scrollbar")?.SetHeights((float)height, (float)contentHeight);
	}
}

[HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.OnRenderGUI))]
internal static class PatcherHandbookRender
{
	public static void Postfix(GuiDialogHandbook __instance, ICoreClientAPI ___capi, GuiComposer ___overviewGui, float deltaTime)
	{
		if (!Bookmarks.IsSupported(__instance)) return;
		HandbookWindowSync.OnRender(__instance, ___capi);
		HoverPreview.OnRender(__instance, ___capi, ___overviewGui, deltaTime);
	}
}

[HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.OnGuiClosed))]
internal static class PatcherHandbookClosed
{
	public static void Postfix(GuiDialogHandbook __instance)
	{
		if (Bookmarks.IsSupported(__instance)) HoverPreview.Hide();
	}
}

// События мыши справочника (он их не переопределяет — патчим GuiDialog с проверкой типа внутри).

[HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseDown))]
internal static class PatcherPreviewMouseDown
{
	public static bool Prefix(GuiDialog __instance, MouseEvent args) => HoverPreview.OnMouseDown(__instance, args);
}

[HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseUp))]
internal static class PatcherPreviewMouseUp
{
	public static bool Prefix(GuiDialog __instance, MouseEvent args) => HoverPreview.OnMouseUp(__instance, args);
}

[HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseMove))]
internal static class PatcherPreviewMouseMove
{
	public static bool Prefix(GuiDialog __instance, MouseEvent args) => HoverPreview.OnMouseMove(__instance, args);
}

[HarmonyPatch(typeof(GuiDialog), nameof(GuiDialog.OnMouseWheel))]
internal static class PatcherPreviewMouseWheel
{
	public static bool Prefix(GuiDialog __instance, ICoreClientAPI ___capi, MouseWheelEventArgs args) => HoverPreview.OnMouseWheel(__instance, ___capi, args);
}

/// <summary>
/// Иконки предметов в richtext (ItemstackTextComponent, SlideshowItemstackTextComponent и др.) рисуются с глубиной 100.
/// На время отрисовки всплывающего окна глубина поднимается выше фона окна, иначе фон их закрывает.
/// </summary>
[HarmonyPatch]
internal static class PatcherRichtextItemZ
{
	public static string Status = "иконки: транспайлер не запускался";

	private static IEnumerable<MethodBase> TargetMethods()
	{
		string[] types =
		{
			"Vintagestory.API.Client.ItemstackTextComponent",
			"Vintagestory.API.Client.SlideshowItemstackTextComponent",
			"Vintagestory.API.Client.SlideshowGridRecipeTextComponent",
			// Картинка блюда/пирога на странице рецепта (VSSurvivalMod, с 1.21)
			"Vintagestory.API.Client.MealstackTextComponent"
		};
		foreach (string name in types)
		{
			Type type = AccessTools.TypeByName(name);
			MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, "RenderInteractiveElements");
			if (method != null) yield return method;
		}
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
	{
		MethodInfo zFloat = AccessTools.Method(typeof(HoverPreview), nameof(HoverPreview.ItemZ));
		MethodInfo zScaled = AccessTools.Method(typeof(HoverPreview), nameof(HoverPreview.ItemZScaled));
		MethodInfo zDouble = AccessTools.Method(typeof(HoverPreview), nameof(HoverPreview.ItemZd));
		MethodInfo scaled = AccessTools.Method(typeof(GuiElement), nameof(GuiElement.scaled), new[] { typeof(double) });
		List<CodeInstruction> code = new List<CodeInstruction>(instructions);
		int patched = 0;
		for (int i = 0; i < code.Count; i++)
		{
			// 100f + renderOffset.Z
			if (code[i].opcode == OpCodes.Ldc_R4 && code[i].operand is float f && f == 100f)
			{
				code.Insert(++i, new CodeInstruction(OpCodes.Call, zFloat));
				patched++;
			}
			// GuiElement.scaled(100.0)
			else if (code[i].opcode == OpCodes.Ldc_R8 && code[i].operand is double d && d == 100.0
				&& i + 1 < code.Count && code[i + 1].Calls(scaled))
			{
				i++;
				code.Insert(++i, new CodeInstruction(OpCodes.Call, zScaled));
				patched++;
			}
			// 100.0 без масштаба (сетка рецепта крафта)
			else if (code[i].opcode == OpCodes.Ldc_R8 && code[i].operand is double d2 && d2 == 100.0)
			{
				code.Insert(++i, new CodeInstruction(OpCodes.Call, zDouble));
				patched++;
			}
		}
		Status = (Status.StartsWith("иконки: транспайлер не") ? "иконки:" : Status) + $" {original.DeclaringType.Name}={patched}";
		return code;
	}
}
