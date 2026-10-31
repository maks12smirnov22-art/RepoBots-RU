using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerHealth), "HealOtherRPC")]
internal static class HealOtherPatch
{
	private static bool Prefix(PlayerHealth __instance, int healAmount, bool effect)
	{
		if (!BotRegistry.IsBot(__instance.playerAvatar))
		{
			return true;
		}
		if (PhotonNetwork.IsMasterClient)
		{
			BotHealth.Heal(__instance, healAmount, effect);
		}
		return false;
	}
}
