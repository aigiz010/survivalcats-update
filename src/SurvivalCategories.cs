using System;
using System.Linq;
using Foundation.Extensions;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace survivalcats.src;

public class ModConfigFile
{
	public static ModConfigFile Current { get; set; }

	public bool addModNameToInfo { get; set; } = true;

	public bool displayVanillaTabsAsCategories { get; set; } = true;

	public bool displayModsAsCategories { get; set; } = true;

	/// <summary>Всплывающее окно с началом страницы при наведении на строку списка справочника.</summary>
	public bool hoverPreview { get; set; } = true;

	/// <summary>Задержка перед показом, мс.</summary>
	public int hoverPreviewDelayMs { get; set; } = 250;

	/// <summary>Предел высоты окна в единицах интерфейса; 0 — по высоте экрана (страница целиком, если влезает).</summary>
	public double hoverPreviewHeightLimit { get; set; } = 0.0;
}

internal class SurvivalCategories : ModSystem
{
	public const string HarmonyId = "com.cwelth.survivalcats";

	private Harmony harmony;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);
		ModConfigFile.Current = api.LoadOrCreateConfig<ModConfigFile>("SurvivalCatsConfig.json");
		harmony = new Harmony(HarmonyId);
		// Каждый патч — отдельно: если в этой версии игры какого-то метода нет, отключится только он.
		int failed = 0;
		foreach (Type type in typeof(SurvivalCategories).Assembly.GetTypes())
		{
			if (!type.IsDefined(typeof(HarmonyPatch), false)) continue;
			try
			{
				harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception e)
			{
				failed++;
				api.Logger.Warning("[survivalcats] Патч {0} не применён: {1}", type.Name, (e.InnerException ?? e).Message);
			}
		}
		api.Logger.Notification("[survivalcats] Harmony: {0} патчей, не применено {1}; справочник: {2}; {3}", harmony.GetPatchedMethods().Count(), failed,
			Compat.SurvivalHandbookType.Name,
			PatcherinitDetailGui.Status + "; " + PatcherHandbookVerticalTabs.Status + "; " + PatcherRichtextItemZ.Status + "; " + PatcherSearchClearButton.Status);
	}

	public override void Dispose()
	{
		harmony?.UnpatchAll(HarmonyId);
		harmony = null;
		base.Dispose();
	}
}
