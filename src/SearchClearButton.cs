using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>
/// Кнопка «×» сразу справа от поля поиска справочника: стирает текст поиска.
/// Поверх поля её ставить нельзя — щелчок заберёт поле ввода.
/// </summary>
internal static class SearchClearButton
{
	private const double ButtonWidth = 30.0;
	private const double Gap = 4.0;
	public const string Key = "survivalcats-clearsearch";

	public static GuiComposer Add(GuiComposer composer, GuiDialogHandbook dlg)
	{
		if (!Compat.IsSurvivalHandbook(dlg) || composer.Composed) return composer;

		ElementBounds field = composer.LastAddedElement?.Bounds;
		if (field == null) return composer;

		// Сразу справа от поля (поле не трогаем: справа от него в окне списка свободно).
		ElementBounds bounds = ElementBounds.Fixed(field.fixedX + field.fixedWidth + Gap, field.fixedY, ButtonWidth, field.fixedHeight);

		GuiComposer target = composer;
		composer.AddSmallButton("×", () =>
		{
			target.GetTextInput("searchField")?.SetValue("");
			return true;
		}, bounds, EnumButtonStyle.Normal, Key);
		return composer;
	}
}

[HarmonyPatch(typeof(GuiDialogHandbook), nameof(GuiDialogHandbook.initOverviewGui))]
internal static class PatcherSearchClearButton
{
	public static string Status = "поиск: транспайлер не запускался";

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo addButton = AccessTools.Method(typeof(SearchClearButton), nameof(SearchClearButton.Add));
		bool done = false;
		foreach (CodeInstruction ins in instructions)
		{
			yield return ins;
			if (!done && ins.opcode == OpCodes.Call && ins.operand is MethodInfo m
				&& m.Name == nameof(Vintagestory.API.Client.GuiComposerHelpers.AddTextInput)
				&& m.DeclaringType == typeof(Vintagestory.API.Client.GuiComposerHelpers))
			{
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Call, addButton);
				done = true;
			}
		}
		Status = $"поиск: кнопка очистки={done}";
	}
}
