using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerTumble), "BreakImpact")]
internal static class TumbleImpactPatch
{
	private static bool Prefix(PlayerTumble __instance)
	{
		if (!BotRegistry.IsBot(__instance.playerAvatar))
		{
			return true;
		}
		if (PhotonNetwork.IsMasterClient && __instance.impactHurtTimer > 0f)
		{
			BotHealth.Hurt(__instance.playerAvatar.playerHealth, __instance.impactHurtDamage, savingGrace: true, -1, hurtByHeal: false);
			__instance.impactHurtTimer = 0f;
		}
		return false;
	}
}
