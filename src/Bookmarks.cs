using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace survivalcats.src;

/// <summary>Закладки справочника: файл ModData/bookmarks.&lt;seed&gt;.json, по коду страницы на строку.</summary>
internal static class Bookmarks
{
	public const string CategoryCode = "bookmark";
	public const string ButtonKey = "btn-bookmark";

	public static string Path = "";

	private static readonly AccessTools.FieldRef<GuiDialogHandbook, Stack<BrowseHistoryElement>> browseHistoryRef =
		AccessTools.FieldRefAccess<GuiDialogHandbook, Stack<BrowseHistoryElement>>("browseHistory");

	private static readonly AccessTools.FieldRef<GuiDialogHandbook, List<GuiHandbookPage>> allPagesRef =
		AccessTools.FieldRefAccess<GuiDialogHandbook, List<GuiHandbookPage>>("allHandbookPages");

	private static readonly AccessTools.FieldRef<GuiDialog, ICoreClientAPI> capiRef =
		AccessTools.FieldRefAccess<GuiDialog, ICoreClientAPI>("capi");

	private static readonly System.Reflection.MethodInfo initDetailGui =
		AccessTools.Method(typeof(GuiDialogHandbook), "initDetailGui");

	public static void SetWorld(ICoreClientAPI capi)
	{
		Path = System.IO.Path.Combine(GamePaths.DataPath, "ModData", "bookmarks." + capi.World.Seed + ".json");
	}

	public static List<string> Load()
	{
		try
		{
			if (Path.Length > 0 && File.Exists(Path))
			{
				return File.ReadAllLines(Path).Where(l => l.Length > 0).ToList();
			}
		}
		catch (Exception)
		{
		}
		return new List<string>();
	}

	public static bool Contains(string pageCode) => pageCode != null && Load().Contains(pageCode);

	private static void Save(List<string> codes)
	{
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
		if (codes.Count == 0)
		{
			File.Delete(Path);
			return;
		}
		File.WriteAllLines(Path, codes);
	}

	public static bool IsSupported(GuiDialogHandbook dlg) => Compat.IsSurvivalHandbook(dlg);

	/// <summary>
	/// Копии-закладки — после фоновой загрузки страниц, задачей на основном потоке: список страниц читает
	/// и меняет основной поток (фильтр, Handbook Optimizer дописывает страницы блюд), так гонки нет.
	/// Номер страницы по коду (pageNumberByPageCode) не трогается — ссылки ведут на исходную страницу.
	/// </summary>
	public static void AddPagesAfterLoad(GuiDialogHandbook dlg)
	{
		if (!IsSupported(dlg)) return;
		List<string> codes = Load();
		if (codes.Count == 0) return;

		ICoreClientAPI capi = capiRef(dlg);
		capi.Event.EnqueueMainThreadTask(() =>
		{
			// Справочник перезагружается: страницы добавит следующий вызов после новой загрузки.
			if (Compat.IsLoadingPages(dlg)) return;

			List<GuiHandbookPage> allPages = allPagesRef(dlg);
			HashSet<string> wanted = new HashSet<string>(codes);
			foreach (GuiHandbookPage p in allPages)
			{
				if (p is GuiHandbookBookmarkedItemStackPage) wanted.Remove(p.PageCode);
			}
			if (wanted.Count == 0) return;

			List<GuiHandbookItemStackPage> sources = new List<GuiHandbookItemStackPage>();
			foreach (GuiHandbookPage p in allPages)
			{
				if (p is GuiHandbookItemStackPage page && p is not GuiHandbookBookmarkedItemStackPage && wanted.Remove(p.PageCode))
				{
					sources.Add(page);
				}
			}
			if (sources.Count == 0) return;

			foreach (GuiHandbookItemStackPage page in sources)
			{
				allPages.Add(new GuiHandbookBookmarkedItemStackPage(capi, page.Stack) { PageNumber = allPages.Count });
			}
			if (dlg.IsOpened()) dlg.FilterItems();
		}, "survivalcats-bookmarks");
	}

	/// <summary>Кнопка «В закладки» рядом с «Назад» в окне страницы справочника.</summary>
	public static GuiComposer AddButton(GuiComposer composer, GuiDialogHandbook dlg)
	{
		if (!IsSupported(dlg) || Path.Length == 0) return composer;

		Stack<BrowseHistoryElement> history = browseHistoryRef(dlg);
		GuiHandbookPage page = history.Count > 0 ? history.Peek().Page : null;

		ElementBounds backBounds = composer.LastAddedElement?.Bounds;
		if (backBounds == null) return composer;

		ElementBounds bounds = backBounds.FlatCopy().WithAlignment(EnumDialogArea.LeftFixed).WithFixedAlignmentOffset(120.0, 1.0);
		backBounds.ParentBounds?.WithChildren(bounds);

		string text = Contains(page?.PageCode) ? Lang.Get("game:button-bookmark-remove") : Lang.Get("game:button-bookmark");
		composer.AddSmallButton(text, () => Toggle(dlg), bounds, EnumButtonStyle.Normal, ButtonKey);

		GuiElementTextButton button = composer.GetButton(ButtonKey);
		if (button != null) button.Enabled = page is GuiHandbookItemStackPage;
		return composer;
	}

	private static bool Toggle(GuiDialogHandbook dlg)
	{
		Stack<BrowseHistoryElement> history = browseHistoryRef(dlg);
		if (history.Count == 0 || history.Peek().Page is not GuiHandbookItemStackPage page) return true;

		List<GuiHandbookPage> allPages = allPagesRef(dlg);
		List<string> codes = Load();
		string code = page.PageCode;

		try
		{
			if (codes.Remove(code))
			{
				int idx = allPages.FindLastIndex(p => p is GuiHandbookBookmarkedItemStackPage && p.PageCode == code);
				if (idx >= 0) allPages.RemoveAt(idx);
			}
			else
			{
				codes.Add(code);
				allPages.Add(new GuiHandbookBookmarkedItemStackPage(capiRef(dlg), page.Stack));
			}
			Save(codes);
		}
		catch (Exception e)
		{
			capiRef(dlg).Logger.Error("[survivalcats] Не удалось сохранить закладки {0}: {1}", Path, e);
		}

		initDetailGui.Invoke(dlg, null);
		return true;
	}
}
