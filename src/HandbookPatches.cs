using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Func = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace survivalcats.src;

/// <summary>Копия страницы предмета во вкладке «Закладки».</summary>
internal class GuiHandbookBookmarkedItemStackPage : GuiHandbookItemStackPage
{
	public override string CategoryCode => Bookmarks.CategoryCode;

	public GuiHandbookBookmarkedItemStackPage(ICoreClientAPI capi, ItemStack stack)
		: base(capi, stack)
	{
		Visible = true;
	}
}

/// <summary>Путь к файлу закладок — до фоновой загрузки страниц; вкладка «Закладки» в списке категорий.</summary>
[HarmonyPatch(typeof(GuiDialogHandbook), "loadEntries")]
internal class PatcherLoadEntries
{
	public static void Prefix(GuiDialogHandbook __instance, ICoreClientAPI ___capi)
	{
		if (!Bookmarks.IsSupported(__instance)) return;
		Bookmarks.SetWorld(___capi);
		CreativeTabCategories.Reset();
	}

	public static void Postfix(GuiDialogHandbook __instance, List<string> ___categoryCodes)
	{
		if (Bookmarks.IsSupported(__instance) && !___categoryCodes.Contains(Bookmarks.CategoryCode))
		{
			___categoryCodes.Add(Bookmarks.CategoryCode);
		}
	}
}

/// <summary>
/// Страницы-закладки — после загрузки страниц справочника. Не через onCreatePagesAsync системы справочника:
/// этот метод Handbook Optimizer подменяет быстрой сборкой и при чужих патчах на нём отключает её.
/// </summary>
[HarmonyPatch(typeof(GuiDialogHandbook), "LoadPages_Async")]
internal class PatcherLoadPagesAsync
{
	public static void Postfix(GuiDialogHandbook __instance)
	{
		Bookmarks.AddPagesAfterLoad(__instance);
	}
}

/// <summary>
/// Кнопка закладки в окне страницы. Кнопка «Обзор» сдвигается из центра влево (x = 280),
/// между «Назад» и «Обзор» встаёт «В закладки» (x = 120).
/// </summary>
[HarmonyPatch(typeof(GuiDialogHandbook), "initDetailGui")]
internal static class PatcherinitDetailGui
{
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo fixedUnder = AccessTools.Method(typeof(ElementBounds), nameof(ElementBounds.FixedUnder));
		MethodInfo withAlignmentOffset = AccessTools.Method(typeof(ElementBounds), nameof(ElementBounds.WithFixedAlignmentOffset));
		MethodInfo addSmallButton = AccessTools.Method(typeof(Vintagestory.API.Client.GuiComposerHelpers), nameof(Vintagestory.API.Client.GuiComposerHelpers.AddSmallButton), new Type[6]
		{
			typeof(GuiComposer), typeof(string), typeof(ActionConsumable), typeof(ElementBounds), typeof(EnumButtonStyle), typeof(string)
		});
		MethodInfo addButton = AccessTools.Method(typeof(Bookmarks), nameof(Bookmarks.AddButton));

		List<CodeInstruction> code = new List<CodeInstruction>(instructions);

		// 1. Кнопка «Обзор»: FixedUnder(...) → WithAlignment(CenterFixed) … WithFixedAlignmentOffset(0.0, 1.0)
		bool movedOverview = false;
		for (int i = 1; i < code.Count && !movedOverview; i++)
		{
			if (!code[i - 1].Calls(fixedUnder) || !code[i].LoadsConstant((long)EnumDialogArea.CenterFixed)) continue;
			for (int j = i + 1; j < code.Count; j++)
			{
				if (code[j].Calls(fixedUnder)) break;
				if (code[j].Calls(withAlignmentOffset) && j >= 2 && code[j - 2].LoadsConstant(0.0))
				{
					code[i] = new CodeInstruction(OpCodes.Ldc_I4, (int)EnumDialogArea.LeftFixed).MoveLabelsFrom(code[i]);
					code[j - 2] = new CodeInstruction(OpCodes.Ldc_R8, 280.0).MoveLabelsFrom(code[j - 2]);
					movedOverview = true;
					break;
				}
			}
		}

		// 2. Сразу после AddSmallButton(Lang.Get("general-back"), …) — своя кнопка
		bool addedButton = false;
		bool sawBack = false;
		for (int i = 0; i < code.Count; i++)
		{
			if (code[i].opcode == OpCodes.Ldstr && (code[i].operand as string) == "general-back") sawBack = true;
			if (sawBack && code[i].Calls(addSmallButton))
			{
				code.InsertRange(i + 1, new[]
				{
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Call, addButton)
				});
				addedButton = true;
				break;
			}
		}

		Status = $"initDetailGui: кнопка «Обзор» сдвинута={movedOverview}, кнопка закладки добавлена={addedButton}";
		return code;
	}

	public static string Status = "initDetailGui: транспайлер не запускался";
}

/// <summary>
/// Поиск в справочнике: «@мод», «#вкладка», «$код» (можно с текстом через пробел: «@expandedfoods пирог»).
/// Стандартный поиск идёт по остатку строки, затем список отсеивается по префиксу.
/// </summary>
[HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.FilterItems))]
internal static class PatcherFilterItems
{
	public sealed class State
	{
		public string FullSearch;
		public Func Match;
		public string Category;
		public string CreativeTab;
	}

	// Раньше чужих префиксов, которые сами заполняют список (Handbook Optimizer EmptySearchPatch, приоритет 400):
	// к их запуску вкладка творческого инвентаря уже заменена на «Все», а префикс поиска снят.
	[HarmonyPriority(Priority.First)]
	public static void Prefix(GuiDialogHandbook __instance, ref string ___currentSearchText, out State __state)
	{
		__state = new State();

		// Вкладка творческого инвентаря: стандартный фильтр — как «Все», потом отбор по вкладке.
		string category = __instance.currentCatgoryCode;
		if (CreativeTabCategories.IsCreativeCategory(category))
		{
			__state.Category = category;
			__state.CreativeTab = category.Substring(CreativeTabCategories.Prefix.Length);
			__instance.currentCatgoryCode = null;
		}

		string text = ___currentSearchText;
		if (text == null || text.Length <= 3) return;

		char kind = text[0];
		if (kind != '@' && kind != '#' && kind != '$') return;

		int space = text.IndexOf(' ');
		string token = space > -1 ? text.Substring(1, space - 1) : text.Substring(1);
		string rest = space > -1 ? text.Substring(space + 1) : "";
		if (token.Length == 0) return;

		Func match = kind switch
		{
			'@' => stack => stack.Collectible.Code.Domain.StartsWith(token, StringComparison.InvariantCultureIgnoreCase),
			'#' => stack => CreativeTabCategories.TabsOf(stack.Collectible).Any(tab => tab.StartsWith(token, StringComparison.InvariantCultureIgnoreCase)),
			_ => stack => stack.Collectible.Code.ToString().CaseInsensitiveContains(token)
		};

		__state.FullSearch = text;
		__state.Match = match;
		___currentSearchText = rest;
	}

	public static void Postfix(GuiDialogHandbook __instance, ref string ___currentSearchText, List<IFlatListItem> ___shownHandbookPages, GuiComposer ___overviewGui,
		double ___listHeight, State __state)
	{
		// Префикс не запускался: другой префикс с более высоким приоритетом вернул false (Handbook Optimizer
		// HiddenCloseFilterPatch при закрытии справочника), и Harmony пропустил остальные префиксы и сам метод.
		// Список не менялся — отсеивать и восстанавливать нечего.
		if (__state == null) return;

		if (__state.Category != null) __instance.currentCatgoryCode = __state.Category;
		if (__state.Match != null) ___currentSearchText = __state.FullSearch;

		string category = __instance.currentCatgoryCode;
		int removed = ___shownHandbookPages.RemoveAll(item =>
		{
			// Копии-закладки показываются только во вкладке «Закладки», а не дублем во «Все» и прочих.
			if (item is GuiHandbookBookmarkedItemStackPage && category != Bookmarks.CategoryCode) return true;
			if (__state.CreativeTab != null && !(item is GuiHandbookPage p && CreativeTabCategories.PageInTab(p, __state.CreativeTab))) return true;
			if (__state.Match != null && !(item is GuiHandbookItemStackPage page && page.Stack?.Collectible?.Code != null && __state.Match(page.Stack))) return true;
			return false;
		});
		if (removed == 0) return;

		GuiElementFlatList flatList = ___overviewGui?.GetFlatList("stacklist");
		if (flatList == null) return;
		flatList.CalcTotalHeight();
		___overviewGui.GetScrollbar("scrollbar")?.SetHeights((float)___listHeight, (float)flatList.insideBounds.fixedHeight);
	}
}
