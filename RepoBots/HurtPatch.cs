using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerHealth), "Hurt")]
internal static class HurtPatch
{
	private static bool Prefix(PlayerHealth __instance, int damage, bool savingGrace, int enemyIndex, bool hurtByHeal)
	{
		if (!BotRegistry.IsBot(__instance.playerAvatar))
		{
			return true;
		}
		if (PhotonNetwork.IsMasterClient)
		{
			BotHealth.Hurt(__instance, damage, savingGrace, enemyIndex, hurtByHeal);
		}
		return false;
	}
}
