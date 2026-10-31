using System.Collections.Generic;
using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerCosmetics), "SetupCosmetics")]
internal static class BotCosmeticsPatch
{
	private static void Prefix(PlayerCosmetics __instance, ref List<int> _cosmetics)
	{
		BotInfo botInfo = BotRegistry.Get(BotLooks.AvatarOf(__instance));
		if (botInfo != null && _cosmetics == null)
		{
			_cosmetics = BotLooks.CosmeticsFor(botInfo);
		}
	}
}
