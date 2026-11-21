using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>
/// Вкладки творческого инвентаря как категории справочника выживания.
/// Порядок, отступы и названия — как в творческом инвентаре (config/creativetabs.json, tabname-&lt;код&gt;).
/// Справочник держит их во второй колонке справа от окна.
/// </summary>
internal static class CreativeTabCategories
{
	public const string Prefix = "ctab:";

	private class TabConfig
	{
		public string Code;
		public double ListOrder;
		public double PaddingTop;
	}

	private class TabsConfig
	{
		public TabConfig[] TabConfigs;
	}

	private sealed class TabInfo
	{
		public string Code;
		public double ListOrder;
		public double PaddingTop;
		public bool Vanilla;
	}

	private static List<TabInfo> tabs;
	private static readonly Dictionary<CollectibleObject, HashSet<string>> tabsByCollectible = new();

	public static bool IsCreativeCategory(string categoryCode) => categoryCode != null && categoryCode.StartsWith(Prefix);

	public static void Reset()
	{
		tabs = null;
		lock (tabsByCollectible) tabsByCollectible.Clear();
	}

	public static HashSet<string> TabsOf(CollectibleObject collectible)
	{
		lock (tabsByCollectible)
		{
			if (tabsByCollectible.TryGetValue(collectible, out HashSet<string> set)) return set;
			set = new HashSet<string>();
			if (collectible.CreativeInventoryTabs != null) set.UnionWith(collectible.CreativeInventoryTabs);
			if (collectible.CreativeInventoryStacks != null)
			{
				foreach (CreativeTabAndStackList list in collectible.CreativeInventoryStacks)
				{
					if (list?.Tabs != null) set.UnionWith(list.Tabs);
				}
			}
			tabsByCollectible[collectible] = set;
			return set;
		}
	}

	public static bool PageInTab(GuiHandbookPage page, string tabCode)
	{
		return page is GuiHandbookItemStackPage stackPage && page is not GuiHandbookBookmarkedItemStackPage
			&& stackPage.Stack?.Collectible != null && TabsOf(stackPage.Stack.Collectible).Contains(tabCode);
	}

	private static List<TabInfo> GetTabs(ICoreClientAPI capi)
	{
		if (tabs != null) return tabs;

		Dictionary<string, TabConfig> configs = new Dictionary<string, TabConfig>();
		try
		{
			TabsConfig cfg = capi.Assets.TryGet(new AssetLocation("game", "config/creativetabs.json"))?.ToObject<TabsConfig>();
			if (cfg?.TabConfigs != null)
			{
				foreach (TabConfig c in cfg.TabConfigs)
				{
					if (c?.Code != null) configs[c.Code] = c;
				}
			}
		}
		catch (Exception e)
		{
			capi.Logger.Warning("[survivalcats] Не удалось прочитать config/creativetabs.json: {0}", e.Message);
		}

		// Только вкладки, в которых есть хоть одна страница справочника.
		ItemStack[] stacks = Compat.AllHandbookStacks(capi);
		IEnumerable<CollectibleObject> collectibles = stacks != null
			? stacks.Where(s => s?.Collectible != null).Select(s => s.Collectible).Distinct()
			: capi.World.Collectibles.Where(c => c?.Code != null);

		List<TabInfo> found = new List<TabInfo>();
		Dictionary<string, TabInfo> byCode = new Dictionary<string, TabInfo>();
		foreach (CollectibleObject collectible in collectibles)
		{
			if (collectible.Code == null) continue;
			bool vanillaItem = collectible.Code.Domain == GlobalConstants.DefaultDomain;
			foreach (string code in TabsOf(collectible))
			{
				if (string.IsNullOrEmpty(code) || code == "general") continue;
				if (!byCode.TryGetValue(code, out TabInfo info))
				{
					configs.TryGetValue(code, out TabConfig c);
					info = new TabInfo { Code = code, ListOrder = c?.ListOrder ?? 1.0, PaddingTop = c?.PaddingTop ?? 0.0 };
					byCode[code] = info;
					found.Add(info);
				}
				info.Vanilla |= vanillaItem;
			}
		}

		// Устойчивая сортировка по listOrder, как вставка в творческом инвентаре.
		tabs = found.Select((t, i) => (t, i)).OrderBy(p => p.t.ListOrder).ThenBy(p => p.i).Select(p => p.t).ToList();
		return tabs;
	}

	/// <summary>Добавляет вкладки творческого инвентаря к вкладкам справочника выживания.</summary>
	public static GuiTab[] AppendTabs(ICoreClientAPI capi, GuiTab[] baseTabs, string currentCategory, ref int curTab)
	{
		ModConfigFile cfg = ModConfigFile.Current;
		if (cfg == null || (!cfg.displayVanillaTabsAsCategories && !cfg.displayModsAsCategories)) return baseTabs;

		List<GuiTab> result = new List<GuiTab>(baseTabs);
		bool first = true;
		foreach (TabInfo tab in GetTabs(capi))
		{
			if (tab.Vanilla ? !cfg.displayVanillaTabsAsCategories : !cfg.displayModsAsCategories) continue;

			string category = Prefix + tab.Code;
			if (currentCategory == category) curTab = result.Count;
			result.Add(new HandbookTab
			{
				DataInt = result.Count,
				Name = Lang.Get("tabname-" + tab.Code),
				PaddingTop = first ? 0.0 : tab.PaddingTop,
				CategoryCode = category
			});
			first = false;
		}
		return result.ToArray();
	}

	// ---- две колонки вкладок ----

	private sealed class Pair
	{
		public GuiElementVerticalTabs Left;
		public GuiElementVerticalTabs Right;
		public GuiTab[] LeftTabs;
		public GuiTab[] RightTabs;

		public void DeactivateLeft() => Compat.DeactivateTabs(Left, LeftTabs);

		public void DeactivateRight() => Compat.DeactivateTabs(Right, RightTabs);
	}

	private static readonly ConditionalWeakTable<GuiElementVerticalTabs, Pair> pairs = new();

	/// <summary>Замена AddVerticalTabs в initOverviewGui/initDetailGui справочника.</summary>
	public static GuiComposer AddVerticalTabs(GuiComposer composer, GuiTab[] tabs, ElementBounds bounds, Action<int, GuiTab> onTabClicked, string key, GuiDialogHandbook dlg)
	{
		int split = tabs == null ? -1 : Array.FindIndex(tabs, t => t is HandbookTab ht && IsCreativeCategory(ht.CategoryCode));
		if (split <= 0 || !Compat.IsSurvivalHandbook(dlg) || composer.Composed)
		{
			return composer.AddVerticalTabs(tabs, bounds, onTabClicked, key);
		}

		GuiTab[] left = tabs.Take(split).ToArray();
		GuiTab[] right = tabs.Skip(split).ToArray();
		Pair pair = new Pair { LeftTabs = left, RightTabs = right };

		composer.AddVerticalTabs(left, bounds, (i, tab) =>
		{
			pair.DeactivateRight();
			onTabClicked(i, tab);
		}, key);
		GuiElementVerticalTabs leftElem = composer.GetVerticalTab(key);
		if (leftElem == null) return composer; // условный блок AddIf(false)
		pair.Left = leftElem;

		// Ширина правой колонки = ширина вкладок: щелчок ловится по правому краю области.
		CairoFont font = CairoFont.WhiteDetailText().WithFontSize(17f);
		double maxText = right.Max(t => font.GetTextExtents(t.Name ?? "").Width);
		double width = Math.Ceiling((maxText + 1.0) / RuntimeEnv.GUIScale + 2.0 * 3.0 + 2.0);
		double height = 5.0 + right.Sum(t => t.PaddingTop + 25.0 + 5.0);

		ElementBounds rightBounds = new RightEdgeBounds(width)
		{
			fixedY = bounds.fixedY,
			fixedHeight = height,
			verticalSizing = ElementSizing.Fixed
		};
		string rightKey = (key ?? "verticalTabs") + "R";
		composer.AddVerticalTabs(right, rightBounds, (i, tab) =>
		{
			pair.DeactivateLeft();
			onTabClicked(split + i, tab);
		}, rightKey);

		pair.Right = composer.GetVerticalTab(rightKey);
		if (pair.Right != null)
		{
			Compat.SetRightTabs(pair.Right);
			pair.DeactivateRight(); // 1.18 подсвечивает вкладку 0 новой колонки по умолчанию
			pairs.Remove(leftElem); // AddOrUpdate нет в .NET Framework (1.18.0–1.18.7)
			pairs.Add(leftElem, pair);
		}
		return composer;
	}

	/// <summary>SetValue(индекс в общем списке) на левой колонке — переводит на правую, если надо.</summary>
	public static bool SetValuePrefix(GuiElementVerticalTabs element, int index, bool triggerHandler)
	{
		if (!pairs.TryGetValue(element, out Pair pair)) return true;
		if (index < pair.LeftTabs.Length)
		{
			pair.DeactivateRight();
			return true;
		}
		int ri = index - pair.LeftTabs.Length;
		if (ri >= pair.RightTabs.Length) return false;
		pair.DeactivateLeft();
		pair.Right.SetValue(ri, triggerHandler);
		return false;
	}
}

/// <summary>
/// Границы «сразу справа от окна»: позиция считается при каждом обращении от текущего размера и места родителя.
/// Так колонка держится у края и после «Открепить» / перетаскивания окна, где выравнивание RightFixed
/// пересчитывается раньше размера окна и съезжает. Ширина — «процентная» (0 %) с переопределением:
/// такие дочерние границы окно не учитывает, когда подгоняет свой размер под содержимое.
/// </summary>
internal class RightEdgeBounds : ElementBounds
{
	private readonly double unscaledWidth;

	public RightEdgeBounds(double unscaledWidth)
	{
		this.unscaledWidth = unscaledWidth;
		horizontalSizing = ElementSizing.Percentual;
		percentWidth = 0.0;
		percentX = 0.0;
		Alignment = EnumDialogArea.None;
	}

	public override double InnerWidth => scaled(unscaledWidth);

	public override double OuterWidth => scaled(unscaledWidth) + 2.0 * absPaddingX;

	public override double relX => (ParentBounds?.InnerWidth ?? 0.0) + absOffsetX;

	public override double absX => relX + (ParentBounds == null ? 0.0 : ParentBounds.absPaddingX + ParentBounds.absX);

	public override double renderX => relX + (ParentBounds == null ? 0.0 : ParentBounds.absPaddingX + ParentBounds.renderX) + renderOffsetX;

	public override double bgDrawX => relX + (ParentBounds == null ? 0.0 : (ParentBounds.IsDrawingSurface ? ParentBounds.absPaddingX : ParentBounds.drawX));
}

/// <summary>genTabs справочника выживания (1.19+ — GuiDialogSurvivalHandbook, 1.18 — сам GuiDialogHandbook).</summary>
[HarmonyPatch]
internal static class PatcherGenTabs
{
	private static MethodBase TargetMethod() => AccessTools.Method(Compat.SurvivalHandbookType, "genTabs");

	public static void Postfix(GuiDialogHandbook __instance, ICoreClientAPI ___capi, ref GuiTab[] __result, ref int curTab)
	{
		if (!Compat.IsSurvivalHandbook(__instance)) return;
		__result = CreativeTabCategories.AppendTabs(___capi, __result, __instance.currentCatgoryCode, ref curTab);
	}
}

[HarmonyPatch(typeof(GuiElementVerticalTabs), nameof(GuiElementVerticalTabs.SetValue), new Type[] { typeof(int), typeof(bool) })]
internal static class PatcherVerticalTabsSetValue
{
	public static bool Prefix(GuiElementVerticalTabs __instance, int index, bool triggerHandler)
	{
		return CreativeTabCategories.SetValuePrefix(__instance, index, triggerHandler);
	}
}

/// <summary>AddVerticalTabs в окнах справочника → две колонки.</summary>
[HarmonyPatch]
internal static class PatcherHandbookVerticalTabs
{
	public static string Status = "вкладки: транспайлер не запускался";

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.initOverviewGui));
		yield return AccessTools.Method(typeof(GuiDialogHandbook), "initDetailGui");
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
	{
		MethodInfo addTabs = AccessTools.Method(typeof(Vintagestory.API.Client.GuiComposerHelpers), nameof(Vintagestory.API.Client.GuiComposerHelpers.AddVerticalTabs));
		MethodInfo replacement = AccessTools.Method(typeof(CreativeTabCategories), nameof(CreativeTabCategories.AddVerticalTabs));
		int replaced = 0;
		foreach (CodeInstruction ins in instructions)
		{
			if (ins.Calls(addTabs))
			{
				yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(ins);
				yield return new CodeInstruction(OpCodes.Call, replacement);
				replaced++;
				continue;
			}
			yield return ins;
		}
		Status = (Status.StartsWith("вкладки: транспайлер не") ? "вкладки:" : Status) + $" {original.Name}={replaced}";
	}
}
