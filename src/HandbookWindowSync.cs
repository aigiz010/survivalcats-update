using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>
/// У справочника два окна: список («handbook-overview») и страница («handbook-detail»), и каждое само по себе
/// бывает «закреплено» / «откреплено» (меню ≡ в заголовке) и запоминает позицию под своим именем.
/// Здесь они связаны: при переключении между списком и страницей новое окно получает то же состояние
/// и ту же позицию, что было у предыдущего, а сохранённая позиция пишется под оба имени.
/// </summary>
internal static class HandbookWindowSync
{
	private static readonly string[] Names = { "handbook-overview", "handbook-detail" };

	private static readonly AccessTools.FieldRef<GuiComposer, Dictionary<string, GuiElement>> interactiveRef =
		AccessTools.FieldRefAccess<GuiComposer, Dictionary<string, GuiElement>>("interactiveElements");

	private static readonly AccessTools.FieldRef<GuiComposer, Dictionary<string, GuiElement>> staticRef =
		AccessTools.FieldRefAccess<GuiComposer, Dictionary<string, GuiElement>>("staticElements");

	private static readonly MethodInfo setUpMovableState =
		AccessTools.Method(typeof(GuiElementDialogTitleBar), "SetUpMovableState");

	private static readonly MethodInfo setSelectedIndex =
		AccessTools.Method(typeof(GuiElementDialogTitleBar), "SetSelectedIndex");

	private static GuiComposer lastComposer;
	private static bool lastMovable;
	private static double lastX;
	private static double lastY;

	private static GuiElementDialogTitleBar TitleBar(GuiComposer composer)
	{
		if (composer == null) return null;
		Dictionary<string, GuiElement> elements = interactiveRef(composer);
		return elements?.Values.OfType<GuiElementDialogTitleBar>().FirstOrDefault();
	}

	public static void OnRender(GuiDialogHandbook dlg, ICoreClientAPI capi)
	{
		GuiComposer current = dlg.SingleComposer;
		if (current == null || Array.IndexOf(Names, Compat.DialogName(current)) < 0) return;

		GuiElementDialogTitleBar bar = TitleBar(current);
		ElementBounds dialogBounds = bar?.Bounds?.ParentBounds;
		if (bar == null || dialogBounds == null) return;

		if (current != lastComposer && lastComposer != null)
		{
			try
			{
				Apply(capi, current, bar, dialogBounds);
			}
			catch (Exception e)
			{
				capi.Logger.Warning("[survivalcats] Не удалось синхронизировать окна справочника: {0}", e.Message);
			}
		}
		else if (current == lastComposer && lastMovable && !bar.Movable)
		{
			// Только что нажали «Закрепить» — сразу вернуть окно на штатное место.
			foreach (string name in Names) capi.Gui.SetDialogPosition(name, null);
			ResetToFixed(dialogBounds);
		}

		try
		{
			SyncSize(current);
		}
		catch (Exception e)
		{
			capi.Logger.Warning("[survivalcats] Не удалось выровнять размер окна справочника: {0}", e.Message);
		}

		lastComposer = current;
		lastMovable = bar.Movable;
		lastX = dialogBounds.fixedX;
		lastY = dialogBounds.fixedY;
	}

	// Внешний размер окна страницы (фон), в единицах интерфейса; 0 — ещё не открывалось.
	private static double detailWidth;
	private static double detailHeight;

	/// <summary>
	/// Фон окна списка подгоняет размер под содержимое, а кнопка «Закрыть» в нём прижата вправо (RightFixed).
	/// При пересчёте (открепление, перетаскивание) её старое смещение прибавляется к ширине — окно растёт.
	/// Поэтому размер фона фиксируется, а заодно приводится к размеру окна страницы, чтобы окна совпадали.
	/// </summary>
	private static void SyncSize(GuiComposer current)
	{
		// Фон окна — элемент AddShadedDialogBG. (Родитель полосы прокрутки не годится: в окне списка
		// это рамка обрезки списка, и её растяжение выпускает список за нижний край.)
		ElementBounds bg = staticRef(current)?.Values.OfType<GuiElementDialogBackground>().FirstOrDefault()?.Bounds;
		if (bg == null || bg.InnerWidth <= 0 || bg.InnerHeight <= 0) return;
		double scale = Vintagestory.API.Config.RuntimeEnv.GUIScale;

		if (Compat.DialogName(current) == Names[1])
		{
			detailWidth = bg.OuterWidth / scale;
			detailHeight = bg.OuterHeight / scale;
			return;
		}

		bool changed = false;
		if (bg.horizontalSizing != ElementSizing.Fixed || bg.verticalSizing != ElementSizing.Fixed)
		{
			bg.fixedWidth = bg.InnerWidth / scale;
			bg.fixedHeight = bg.InnerHeight / scale;
			bg.horizontalSizing = ElementSizing.Fixed;
			bg.verticalSizing = ElementSizing.Fixed;
		}

		if (detailWidth > 0 && detailHeight > 0)
		{
			double width = detailWidth - 2 * bg.fixedPaddingX;
			double height = detailHeight - 2 * bg.fixedPaddingY;
			if (Math.Abs(bg.fixedWidth - width) > 0.5 || Math.Abs(bg.fixedHeight - height) > 0.5)
			{
				bg.fixedWidth = width;
				bg.fixedHeight = height;
				changed = true;
			}
		}

		if (changed)
		{
			current.Bounds.MarkDirtyRecursive();
			current.ReCompose();
		}
	}

	private static void Apply(ICoreClientAPI capi, GuiComposer composer, GuiElementDialogTitleBar bar, ElementBounds dialogBounds)
	{
		if (lastMovable)
		{
			Vec2i pos = new Vec2i((int)lastX, (int)lastY);
			foreach (string name in Names) capi.Gui.SetDialogPosition(name, pos);

			if (!bar.Movable)
			{
				// Как при запуске с сохранённой позицией: заголовок запомнит «закреплённые» границы для возврата.
				setUpMovableState.Invoke(bar, new object[] { null });
			}
			else
			{
				dialogBounds.fixedX = lastX;
				dialogBounds.fixedY = lastY;
				dialogBounds.MarkDirtyRecursive();
				dialogBounds.CalcWorldBounds();
			}
			setSelectedIndex?.Invoke(bar, new object[] { 1 });
		}
		else
		{
			foreach (string name in Names) capi.Gui.SetDialogPosition(name, null);
			if (bar.Movable)
			{
				setUpMovableState.Invoke(bar, new object[] { "auto" });
			}
			setSelectedIndex?.Invoke(bar, new object[] { 0 });
			ResetToFixed(dialogBounds);
		}
	}

	/// <summary>
	/// Штатное место окон справочника (как их создаёт игра): по центру по горизонтали, 70 единиц от верха.
	/// Восстановление из запомненных заголовком границ срабатывает не всегда (окно оставалось на месте до переоткрытия).
	/// </summary>
	private static void ResetToFixed(ElementBounds dialogBounds)
	{
		dialogBounds.Alignment = EnumDialogArea.CenterFixed;
		dialogBounds.fixedX = 0.0;
		dialogBounds.fixedY = 70.0;
		dialogBounds.fixedOffsetX = 0.0;
		dialogBounds.fixedOffsetY = 0.0;
		dialogBounds.absMarginX = 0.0;
		dialogBounds.absMarginY = 0.0;
		dialogBounds.MarkDirtyRecursive();
		dialogBounds.CalcWorldBounds();
		// Отступ выравнивания считается только при готовом родителе — пересчитать ещё раз уже с ним.
		dialogBounds.MarkDirtyRecursive();
		dialogBounds.CalcWorldBounds();
	}

	public static void Reset()
	{
		lastComposer = null;
	}
}
