using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Foundation.Extensions;

public static class ApiExtensions
{
	public static TConfig LoadOrCreateConfig<TConfig>(this ICoreAPI api, string filename) where TConfig : new()
	{
		try
		{
			TConfig val = api.LoadModConfig<TConfig>(filename);
			if (val != null)
			{
				api.StoreModConfig(val, filename);
				return val;
			}
		}
		catch (Exception value)
		{
			api.World.Logger.Error("{0}", $"Failed loading file ({filename}), error {value}. Will initialize new one");
		}
		TConfig val2 = new TConfig();
		api.StoreModConfig(val2, filename);
		return val2;
	}

	public static string GetWorldId(this ICoreAPI api)
	{
		return api?.World?.Seed.ToString();
	}

	public static TData LoadOrCreateDataFile<TData>(this ICoreAPI api, string filename) where TData : new()
	{
		string text = Path.Combine(GamePaths.DataPath, "ModData", api.GetWorldId(), filename);
		try
		{
			if (File.Exists(text))
			{
				return JsonUtil.FromString<TData>(File.ReadAllText(text));
			}
		}
		catch (Exception value)
		{
			api.World.Logger.Log(EnumLogType.Error, $"Failed loading file ({text}), error {value}. Will initialize new one");
		}
		TData val = new TData();
		api.SaveDataFile(filename, val);
		return val;
	}

	public static void SaveDataFile<TData>(this ICoreAPI api, string filename, TData data) where TData : new()
	{
		string text = Path.Combine(GamePaths.DataPath, "ModData", api.GetWorldId(), filename);
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			string contents = JsonUtil.ToString(data);
			File.WriteAllText(text, contents);
		}
		catch (Exception value)
		{
			api.World.Logger.Log(EnumLogType.Error, $"Failed loading file ({text}), error {value}. Will initialize new one");
		}
	}
}
