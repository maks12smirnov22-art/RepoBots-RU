using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace RepoBots;

internal class BotPersona
{
	public float Brave = 0.5f;

	public float Greed = 0.5f;

	public float Helpful = 0.5f;

	public float Chatty = 0.5f;

	public float Social = 0.5f;

	public float Careful = 0.5f;

	private static string _loadedFor;

	private static readonly Dictionary<string, BotPersona> Saved = new Dictionary<string, BotPersona>();

	private static readonly BotPersona Plain = new BotPersona();

	private static string Folder => Path.Combine(Paths.ConfigPath, "RepoBots", "personas");

	public static float VoicePitch(string steamId)
	{
		int num = 17;
		string text = steamId ?? "";
		foreach (char c in text)
		{
			num = num * 31 + c;
		}
		return 0.82f + (float)(Mathf.Abs(num) % 37) / 100f;
	}

	public string Describe()
	{
		List<string> list = new List<string>();
		if (Brave > 0.7f)
		{
			list.Add("brave");
		}
		else if (Brave < 0.3f)
		{
			list.Add("jumpy");
		}
		if (Greed > 0.7f)
		{
			list.Add("greedy");
		}
		else if (Greed < 0.3f)
		{
			list.Add("easygoing");
		}
		if (Helpful > 0.7f)
		{
			list.Add("helpful");
		}
		else if (Helpful < 0.3f)
		{
			list.Add("independent");
		}
		if (Chatty > 0.7f)
		{
			list.Add("chatty");
		}
		else if (Chatty < 0.3f)
		{
			list.Add("quiet");
		}
		if (Social > 0.7f)
		{
			list.Add("clingy");
		}
		else if (Social < 0.3f)
		{
			list.Add("a lone wolf");
		}
		if (Careful > 0.7f)
		{
			list.Add("careful");
		}
		else if (Careful < 0.3f)
		{
			list.Add("clumsy");
		}
		if (list.Count != 0)
		{
			return string.Join(", ", list);
		}
		return "pretty average";
	}

	public static BotPersona For(string steamId)
	{
		if (!Plugin.Personalities.Value)
		{
			return Plain;
		}
		Load();
		if (Saved.TryGetValue(steamId, out var value))
		{
			return value;
		}
		value = Roll();
		Saved[steamId] = value;
		Store();
		return value;
	}

	private static BotPersona Roll()
	{
		BotPersona botPersona = new BotPersona();
		float[] array = new float[6];
		for (int i = 0; i < 6; i++)
		{
			array[i] = (Random.value + Random.value) * 0.5f;
		}
		int num = Random.Range(0, 6);
		int num2 = (num + Random.Range(1, 6)) % 6;
		array[num] = ((Random.value < 0.5f) ? Random.Range(0f, 0.18f) : Random.Range(0.82f, 1f));
		array[num2] = ((Random.value < 0.5f) ? Random.Range(0f, 0.18f) : Random.Range(0.82f, 1f));
		botPersona.Brave = array[0];
		botPersona.Greed = array[1];
		botPersona.Helpful = array[2];
		botPersona.Chatty = array[3];
		botPersona.Social = array[4];
		botPersona.Careful = array[5];
		return botPersona;
	}

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
				string[] v;
				if (array2.Length == 2)
				{
					v = array2[1].Split(',');
					if (v.Length >= 6)
					{
						Saved[array2[0]] = new BotPersona
						{
							Brave = F(0),
							Greed = F(1),
							Helpful = F(2),
							Chatty = F(3),
							Social = F(4),
							Careful = F(5)
						};
					}
				}
				float F(int num)
				{
					if (!float.TryParse(v[num], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						return 0.5f;
					}
					return Mathf.Clamp01(result);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't read bot personalities: " + ex.Message));
		}
	}

	private static void Store()
	{
		try
		{
			Directory.CreateDirectory(Folder);
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, BotPersona> item in Saved)
			{
				BotPersona value = item.Value;
				list.Add(item.Key + "=" + S(value.Brave) + "," + S(value.Greed) + "," + S(value.Helpful) + "," + S(value.Chatty) + "," + S(value.Social) + "," + S(value.Careful));
			}
			File.WriteAllLines(Path.Combine(Folder, SaveName() + ".txt"), list);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("Couldn't save bot personalities: " + ex.Message));
		}
		static string S(float f)
		{
			return f.ToString("0.00", CultureInfo.InvariantCulture);
		}
	}
}
