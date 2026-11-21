using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;

namespace survivalcats.src;

/// <summary>Строка «Mod: имя (@modid)» в начале подсказки предмета.</summary>
[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.GetHeldItemInfo))]
internal class PatcherGetHeldItemInfo
{
	private const string Prefix = "<font color=\"#88FF88\">";

	public static void Postfix(ICoreAPI ___api, ItemSlot inSlot, StringBuilder dsc)
	{
		if (ModConfigFile.Current?.addModNameToInfo != true || ___api == null || dsc == null) return;

		string domain = inSlot?.Itemstack?.Collectible?.Code?.Domain;
		if (domain == null) return;

		Mod mod = ___api.ModLoader.GetMod(domain);
		if (mod != null && !dsc.ToString().StartsWith(Prefix))
		{
			dsc.Insert(0, Prefix + "<strong>Mod: " + mod.Info.Name + " (@" + mod.Info.ModID + ")</strong></font>\n");
		}
	}
}
