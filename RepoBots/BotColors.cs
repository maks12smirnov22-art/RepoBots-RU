using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotColors
{
	private static string _loadedFor;

	private static readonly Dictionary<string, int> Saved = new Dictionary<string, int>();

	private static string Folder => Path.Combine(Paths.ConfigPath, "RepoBots", "colors");

	private static string SaveName()
	{
		string text = (((Object)(object)StatsManager.instance != (Object)null) ? StatsManager.instance.saveFileCurrent : null);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return "_nosave";
	}

	private static void Load()
	{
		string text = SaveName();
		if (_loadedFor == text)
		{
			return;
		}
		_loadedFor = text;
		Saved.Clear();
		try
		{
			string path = Path.Combine(Folder, text + ".txt");
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('=');
				if (array2.Length == 2 && int.TryParse(array2[1], out var result))
				{
					Saved[array2[0]] = result;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't read bot colors: " + ex.Message));
		}
	}

	private static void Store()
	{
		try
		{
			Directory.CreateDirectory(Folder);
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, int> item in Saved)
			{
				list.Add(item.Key + "=" + item.Value);
			}
			File.WriteAllLines(Path.Combine(Folder, SaveName() + ".txt"), list);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't save bot colors: " + ex.Message));
		}
	}

	public static int For(string steamId)
	{
		Load();
		int num = ((!((Object)(object)MetaManager.instance != (Object)null) || MetaManager.instance.colors == null || MetaManager.instance.colors.Count <= 0) ? 1 : MetaManager.instance.colors.Count);
		int num2 = -1;
		if ((Object)(object)StatsManager.instance != (Object)null)
		{
			num2 = StatsManager.instance.GetPlayerColor(steamId);
		}
		if (num2 < 0 || num2 >= num)
		{
			num2 = ((Saved.TryGetValue(steamId, out var value) && value >= 0 && value < num) ? value : Random.Range(0, num));
		}
		if ((Object)(object)StatsManager.instance != (Object)null)
		{
			StatsManager.instance.SetPlayerColor(steamId, num2);
		}
		if (!Saved.TryGetValue(steamId, out var value2) || value2 != num2)
		{
			Saved[steamId] = num2;
			Store();
		}
		return num2;
	}
}
