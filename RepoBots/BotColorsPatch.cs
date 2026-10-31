using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerCosmetics), "SetupColors")]
internal static class BotColorsPatch
{
	private static bool Prefix(PlayerCosmetics __instance, int[] _colors)
	{
		if (_colors == null)
		{
			return BotRegistry.Get(BotLooks.AvatarOf(__instance)) == null;
		}
		return true;
	}
}
