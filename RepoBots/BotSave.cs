using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotSave
{
	private static readonly string[] AllFolders = new string[3] { "saves", "personas", "colors" };

	private static string _save;

	private static readonly Dictionary<string, int> Data = new Dictionary<string, int>();

	private static bool _pruned;

	private static string Root => Path.Combine(Paths.ConfigPath, "RepoBots");

	private static string Folder => Path.Combine(Root, "saves");

	public static string Current => _save;

	private static string GameSave()
	{
		string text = (((Object)(object)StatsManager.instance != (Object)null) ? StatsManager.instance.saveFileCurrent : null);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return null;
	}

	public static void OnCreated(string save)
	{
		_save = save;
		Data.Clear();
		Data["count"] = Mathf.Clamp(Plugin.BotCount.Value, 0, 2);
		Plugin.Log.LogInfo((object)("New save " + save + ": bots start fresh"));
	}

	public static void OnLoaded(string save)
	{
		_save = save;
		Read(save);
		if (Data.TryGetValue("count", out var value))
		{
			value = Mathf.Clamp(value, 0, 2);
			if (Plugin.BotCount.Value != value)
			{
				Plugin.BotCount.Value = value;
			}
		}
		else
		{
			Data["count"] = Plugin.BotCount.Value;
		}
		Plugin.Log.LogInfo((object)$"Loaded bots for save {save} ({Plugin.BotCount.Value} bots)");
	}

	public static void OnSaved(string save)
	{
		if (!string.IsNullOrEmpty(save))
		{
			if (_save != save)
			{
				_save = save;
			}
			Data["count"] = Mathf.Clamp(Plugin.BotCount.Value, 0, 2);
			Write(save);
		}
	}

	public static void OnDeleted(string save)
	{
		if (string.IsNullOrEmpty(save))
		{
			return;
		}
		string[] allFolders = AllFolders;
		foreach (string path in allFolders)
		{
			try
			{
				string path2 = Path.Combine(Root, path, save + ".txt");
				if (File.Exists(path2))
				{
					File.Delete(path2);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning((object)("Couldn't delete bot file for " + save + ": " + ex.Message));
			}
		}
		if (_save == save)
		{
			_save = null;
			Data.Clear();
		}
	}

	public static void Tick()
	{
		if (!_pruned)
		{
			_pruned = true;
			Prune();
		}
		string text = GameSave();
		if (text != null && text != _save)
		{
			OnLoaded(text);
		}
		if (_save != null)
		{
			Data["count"] = Mathf.Clamp(Plugin.BotCount.Value, 0, 2);
		}
	}

	public static int Get(string steamId, string key)
	{
		if (!Data.TryGetValue(steamId + "." + key, out var value))
		{
			return 0;
		}
		return value;
	}

	public static void Add(string steamId, string key, int amount = 1)
	{
		if (!string.IsNullOrEmpty(steamId) && _save != null && amount != 0)
		{
			string key2 = steamId + "." + key;
			Data.TryGetValue(key2, out var value);
			Data[key2] = value + amount;
		}
	}

	private static void Read(string save)
	{
		Data.Clear();
		try
		{
			string path = Path.Combine(Folder, save + ".txt");
			if (!File.Exists(path))
			{
				return;
			}
			string[] array = File.ReadAllLines(path);
			foreach (string text in array)
			{
				int num = text.LastIndexOf('=');
				if (num > 0 && int.TryParse(text.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					Data[text.Substring(0, num)] = result;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't read bot save data: " + ex.Message));
		}
	}

	private static void Write(string save)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, int> datum in Data)
			{
				list.Add(datum.Key + "=" + datum.Value.ToString(CultureInfo.InvariantCulture));
			}
			list.Sort();
			File.WriteAllLines(Path.Combine(Folder, save + ".txt"), list);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't write bot save data: " + ex.Message));
		}
	}

	private static void Prune()
	{
		try
		{
			string text = Path.Combine(Application.persistentDataPath, "saves");
			if (!Directory.Exists(text))
			{
				return;
			}
			string[] allFolders = AllFolders;
			foreach (string path in allFolders)
			{
				string path2 = Path.Combine(Root, path);
				if (!Directory.Exists(path2))
				{
					continue;
				}
				string[] files = Directory.GetFiles(path2, "REPO_SAVE_*.txt");
				foreach (string path3 in files)
				{
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path3);
					if (!Directory.Exists(Path.Combine(text, fileNameWithoutExtension)) && fileNameWithoutExtension != GameSave())
					{
						File.Delete(path3);
						Plugin.Log.LogInfo((object)("Removed bot file for deleted save " + fileNameWithoutExtension));
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't tidy old bot files: " + ex.Message));
		}
	}
}
