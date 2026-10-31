using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[BepInPlugin("dcz.repo.playerbots", "RepoBots", "0.22.5")]
public class Plugin : BaseUnityPlugin
{
	public const string Guid = "dcz.repo.playerbots";

	public const string Name = "RepoBots";

	public const string Version = "0.22.5";

	internal static ManualLogSource Log;

	internal static ConfigEntry<int> BotCount;

	internal static ConfigEntry<KeyCode> AddKey;

	internal static ConfigEntry<KeyCode> RemoveKey;

	internal static ConfigEntry<bool> FailWhenPeopleDie;

	internal static ConfigEntry<float> ReviveGiveUpSeconds;

	internal static ConfigEntry<bool> BotsHaul;

	internal static ConfigEntry<bool> BotsFlee;

	internal static ConfigEntry<float> MaxCarryMass;

	internal static ConfigEntry<bool> GentleSetDown;

	internal static ConfigEntry<bool> BotsUseCarts;

	internal static ConfigEntry<bool> BotsExplore;

	internal static ConfigEntry<bool> BotsOpen;

	internal static ConfigEntry<bool> BotsShop;

	internal static ConfigEntry<bool> SingleplayerBots;

	internal static ConfigEntry<bool> CarryLog;

	internal static ConfigEntry<bool> BotsDive;

	internal static ConfigEntry<bool> RealPhysics;

	internal static ConfigEntry<float> CartFanChance;

	internal static ConfigEntry<bool> BotsTalk;

	internal static ConfigEntry<bool> KeyboardMouse;

	internal static ConfigEntry<bool> ChatCommands;

	internal static ConfigEntry<bool> VoiceCommands;

	internal static ConfigEntry<bool> VoiceShowText;

	internal static ConfigEntry<bool> BotSlang;

	internal static ConfigEntry<bool> Personalities;

	internal static ConfigEntry<string> AIApiKey;

	internal static ConfigEntry<string> AIModel;

	public static string SecretsPath => Path.Combine(Application.persistentDataPath, "RepoBots", "RepoBots.secrets.cfg");

	private ConfigEntry<string> BindSecrets()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected Obj, but got Unknown
		ConfigFile val = new ConfigFile(SecretsPath, true);
		ConfigEntry<string> val2 = val.Bind<string>("Talking", "ClaudeApiKey", "", "Optional, host only. An Anthropic API key (console.anthropic.com). With it, bots answer anything you say to them by name using Claude, still only from what they know, and can act on it. Each reply is one small API call that you pay for. Empty = built-in replies only. Kept here, outside your mod profile, so sharing a profile never shares your key.");
		ConfigEntry<string> val3 = ((BaseUnityPlugin)this).Config.Bind<string>("Talking", "ClaudeApiKey", "", "Moved out of this file, see " + SecretsPath);
		if (!string.IsNullOrWhiteSpace(val3.Value) && string.IsNullOrWhiteSpace(val2.Value))
		{
			val2.Value = val3.Value;
			val.Save();
			Log.LogInfo((object)("Moved ClaudeApiKey out of the profile config to " + SecretsPath));
		}
		((BaseUnityPlugin)this).Config.Remove(((ConfigEntryBase)val3).Definition);
		((BaseUnityPlugin)this).Config.Save();
		return val2;
	}

	private void Awake()
	{
		//IL_0373: Unknown result type (might be due to invalid IL or missing references)
		//IL_0379: Expected Obj, but got Unknown
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Expected Obj, but got Unknown
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0403: Unknown result type (might be due to invalid IL or missing references)
		Log = this.Logger;
		BotCount = ((BaseUnityPlugin)this).Config.Bind<int>("Bots", "Count", 2, "How many bots join the host on every level (host only; maximum 2).");
		AddKey = ((BaseUnityPlugin)this).Config.Bind<KeyCode>("Controls", "AddBot", (KeyCode)289, "Host only. Adds one bot right now (and raises Count).");
		RemoveKey = ((BaseUnityPlugin)this).Config.Bind<KeyCode>("Controls", "RemoveBots", (KeyCode)290, "Host only. Removes every bot and sets Count to 0.");
		FailWhenPeopleDie = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "FailWhenPeopleDie", false, "If true, the level fails the moment every real player is dead, even with bots alive. If false, the bots get a chance to carry your heads to the truck and revive you.");
		ReviveGiveUpSeconds = ((BaseUnityPlugin)this).Config.Bind<float>("Bots", "ReviveGiveUpSeconds", 120f, "With every real player dead, the level fails if the bots haven't revived anyone after this many seconds.");
		BotsHaul = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsHaul", true, "Bots carry valuables they've seen to the active extraction point, and turn on the next extraction point.");
		BotsFlee = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsFlee", true, "Bots drop what they're carrying and run when a monster gets close or hurts them.");
		RealPhysics = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "RealPhysics", true, "Bots move with a real physics body built like a player's (momentum, gravity, falling, stamina, jumping, getting pushed). Off = the older smooth walking that ignores physics.");
		BotsDive = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsDive", false, "Bots throw themselves into a roll to escape a monster that's right on them (leaves them helpless for a moment).");
		CarryLog = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "CarryLog", true, "Write a line to the log every few seconds while a bot carries loot (helps track down carrying problems).");
		SingleplayerBots = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "SingleplayerBots", true, "Singleplayer starts a private offline game (no internet needed, nobody can join) so bots can play with you; pick how many on the lobby screen. Turn off for the game's normal singleplayer.");
		BotsShop = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsShop", true, "In the shop, bots put things the team needs in the buy zone (health packs, crystals, upgrades), staying within their share of the money. People still press the buy button.");
		BotsOpen = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsOpen", true, "Bots open chests, cabinets, wardrobes, fridges, lockers and safes to look for loot inside.");
		BotsExplore = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsExplore", true, "Bots split up and search rooms nobody has checked yet (finding loot as they go) instead of following you around.");
		CartFanChance = ((BaseUnityPlugin)this).Config.Bind<float>("Bots", "CartFanChance", 0.5f, "Each round, the chance a bot really wants the cart: it takes charge of it straight away (if nobody's using it), brings it to the loot, fills it and takes it to extraction. Without a fan, a bot still takes over a cart nobody has touched for a while.");
		BotsUseCarts = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "BotsUseCarts", true, "Bots load valuables into a nearby cart and push loaded carts to extraction.");
		KeyboardMouse = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "KeyboardMouse", true, "Bots move like a player on keyboard and mouse: eight key directions at full speed (tapping to go slow), steering by turning the camera, mouse-like flicks when they look around. Off = smooth analog movement.");
		BotsTalk = ((BaseUnityPlugin)this).Config.Bind<bool>("Talking", "BotsTalk", true, "Bots talk out loud (the game's text-to-speech voice): calling out monsters, asking for help, saying where their head is when they die, answering you.");
		ChatCommands = ((BaseUnityPlugin)this).Config.Bind<bool>("Talking", "ChatCommands", true, "Bots listen to chat. Say a bot's name (or 'everyone') plus a command or question, e.g. 'bolt follow me', 'everyone to the truck', 'rusty where's the cart', 'grab that'. Unnamed commands go to the nearest bot that can hear you.");
		VoiceCommands = ((BaseUnityPlugin)this).Config.Bind<bool>("Talking", "VoiceCommands", true, "Talk to the bots with your mic instead of typing: anything you could type in chat works said out loud ('bolt follow me', 'everyone to the truck'). Uses Windows speech recognition, which needs 'Online speech recognition' turned on in Windows Settings > Privacy & security > Speech. Only listens while bots are around and your in-game mic is open (push to talk and mute count). Each player sets this for themselves.");
		BotSlang = ((BaseUnityPlugin)this).Config.Bind<bool>("Talking", "BotSlang", true, "Bots type like players: gonna, u, ngl, fr, bro, the odd typo fixed with *word. Each bot has its own amount of slang and favourite words (new ones for each save). Off = plainer talk.");
		VoiceShowText = ((BaseUnityPlugin)this).Config.Bind<bool>("Talking", "VoiceShowText", true, "Show what the speech recognition heard at the bottom of the screen for a few seconds.");
		Personalities = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "Personalities", true, "Each bot gets its own personality (brave/jumpy, greedy, helpful, chatty, clingy/lone wolf, careful/clumsy) that changes how it plays and talks. Kept per save in BepInEx/config/RepoBots/personas.");
		AIApiKey = BindSecrets();
		AIModel = ((BaseUnityPlugin)this).Config.Bind<string>("Talking", "ClaudeModel", "claude-haiku-5-5", "Which Claude model writes the replies.");
		GentleSetDown = ((BaseUnityPlugin)this).Config.Bind<bool>("Bots", "GentleCarry", true, "Bots lower loot to the floor before letting go (instead of dropping it), carry fragile things slower and brake before they bump into walls.");
		MaxCarryMass = ((BaseUnityPlugin)this).Config.Bind<float>("Bots", "MaxCarryMass", 5f, "Bots only pick up valuables up to this heavy (physics mass). Big stuff is left for you.");
		Harmony val = new Harmony("dcz.repo.playerbots");
		Type[] types = typeof(Plugin).Assembly.GetTypes();
		foreach (Type type in types)
		{
			if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length != 0)
			{
				try
				{
					val.CreateClassProcessor(type).Patch();
				}
				catch (Exception arg)
				{
					Log.LogError((object)$"Patch {type.Name} failed: {arg}");
				}
			}
		}
		GameObject val2 = new GameObject("RepoBots");
		Object.DontDestroyOnLoad((Object)val2);
		((Object)val2).hideFlags = (HideFlags)61;
		val2.AddComponent<BotNet>();
		val2.AddComponent<BotManager>();
		val2.AddComponent<BotListen>();
		Log.LogInfo((object)"RepoBots 0.22.5 loaded");
	}
}
