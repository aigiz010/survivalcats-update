using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>
/// Различия версий игры 1.18 … 1.22. Мод собран против API 1.18.0, а всё, что появилось или переименовано позже,
/// берётся здесь через отражение — прямые ссылки на отсутствующие члены роняли бы мод при загрузке.
/// <list type="bullet">
/// <item>1.18: один справочник GuiDialogHandbook + ModSystemHandbook (с 1.18.15 рядом GuiDialogCommandHandbook);
/// с 1.19 — GuiDialogSurvivalHandbook + ModSystemSurvivalHandbook.</item>
/// <item>1.18: вкладки — поля right / activeElement; с 1.19 — Right / ActiveElement и GuiTab.Active.</item>
/// <item>до 1.19 включительно: имя окна — internal GuiComposer.dialogName, с 1.20 — public DialogName.</item>
/// <item>до 1.21: все стопки справочника — поле allstacks системы справочника, в 1.22 — кэш «handbookallstacks».</item>
/// </list>
/// </summary>
internal static class Compat
{
	/// <summary>Класс справочника выживания: GuiDialogSurvivalHandbook (1.19+) или сам GuiDialogHandbook (1.18).</summary>
	public static readonly Type SurvivalHandbookType =
		AccessTools.TypeByName("Vintagestory.GameContent.GuiDialogSurvivalHandbook") ?? typeof(GuiDialogHandbook);

	/// <summary>Система справочника выживания: ModSystemSurvivalHandbook (1.19+) или ModSystemHandbook (1.18).</summary>
	public static readonly Type SurvivalHandbookSystemType =
		AccessTools.TypeByName("Vintagestory.GameContent.ModSystemSurvivalHandbook")
		?? AccessTools.TypeByName("Vintagestory.GameContent.ModSystemHandbook");

	/// <summary>Справочник выживания, а не справочник команд.</summary>
	public static bool IsSurvivalHandbook(object dlg) =>
		dlg != null && SurvivalHandbookType.IsInstanceOfType(dlg) && dlg.GetType().Name != "GuiDialogCommandHandbook";

	// ---- имя окна ----

	private static readonly FieldInfo dialogNameField =
		AccessTools.Field(typeof(GuiComposer), "DialogName") ?? AccessTools.Field(typeof(GuiComposer), "dialogName");

	public static string DialogName(GuiComposer composer) =>
		composer == null || dialogNameField == null ? null : dialogNameField.GetValue(composer) as string;

	// ---- вкладки ----

	private static readonly FieldInfo tabActiveField = AccessTools.Field(typeof(GuiTab), "Active");

	private static readonly FieldInfo tabsRightField =
		AccessTools.Field(typeof(GuiElementVerticalTabs), "Right") ?? AccessTools.Field(typeof(GuiElementVerticalTabs), "right");

	private static readonly FieldInfo tabsActiveElementField =
		AccessTools.Field(typeof(GuiElementVerticalTabs), "ActiveElement") ?? AccessTools.Field(typeof(GuiElementVerticalTabs), "activeElement");

	/// <summary>Вкладки прижаты к правому краю окна (язычки смотрят вправо).</summary>
	public static void SetRightTabs(GuiElementVerticalTabs element)
	{
		tabsRightField?.SetValue(element, true);
	}

	/// <summary>Снять выделение со всех вкладок колонки.</summary>
	public static void DeactivateTabs(GuiElementVerticalTabs element, GuiTab[] tabs)
	{
		if (tabActiveField != null)
		{
			// 1.19+: подсветка по GuiTab.Active
			foreach (GuiTab t in tabs) tabActiveField.SetValue(t, false);
		}
		else if (element != null && tabsActiveElementField != null)
		{
			// 1.18: подсветка по номеру activeElement
			tabsActiveElementField.SetValue(element, -1);
		}
	}

	// ---- загрузка страниц ----

	/// <summary>Страницы ещё грузятся в фоне: loadingPagesAsync (1.19+) или notReady (1.18).</summary>
	private static readonly FieldInfo loadingPagesField =
		AccessTools.Field(typeof(GuiDialogHandbook), "loadingPagesAsync") ?? AccessTools.Field(typeof(GuiDialogHandbook), "notReady");

	public static bool IsLoadingPages(GuiDialogHandbook dlg) =>
		loadingPagesField != null && loadingPagesField.GetValue(dlg) is true;

	// ---- стопки справочника ----

	public static ItemStack[] AllHandbookStacks(ICoreClientAPI capi)
	{
		ItemStack[] stacks = ObjectCacheUtil.TryGet<ItemStack[]>(capi, "handbookallstacks");
		if (stacks != null) return stacks;
		if (SurvivalHandbookSystemType == null) return null;
		ModSystem system = capi.ModLoader.Systems.FirstOrDefault(s => SurvivalHandbookSystemType.IsInstanceOfType(s));
		FieldInfo field = AccessTools.Field(SurvivalHandbookSystemType, "allstacks");
		return system == null || field == null ? null : field.GetValue(system) as ItemStack[];
	}
}
