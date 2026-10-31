using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal class SlangStyle
{
	public float Level;

	public string Pet;

	public float Typos;

	public int SincePet = 9;

	private static readonly string[] Pets = new string[8] { "bro", "dude", "bruh", "man", "yall", "chat", "guys", "lol" };

	public static SlangStyle For(string steamId, BotPersona p)
	{
		string text = (((Object)(object)StatsManager.instance != (Object)null) ? StatsManager.instance.saveFileCurrent : "");
		uint num = 2166136261u;
		string text2 = steamId + "|" + text;
		foreach (char c in text2)
		{
			num = (num ^ c) * 16777619;
		}
		num ^= num >> 15;
		num *= 739982445;
		num ^= num >> 12;
		float level = Mathf.Clamp01((float)(num & 0xFFFF) / 65535f * 0.75f + (p.Chatty - 0.5f) * 0.4f + (0.5f - p.Careful) * 0.3f + 0.12f);
		return new SlangStyle
		{
			Level = level,
			Pet = Pets[(num >> 16) % (uint)Pets.Length],
			Typos = Mathf.Lerp(0.01f, 0.05f, 1f - p.Careful)
		};
	}
}
