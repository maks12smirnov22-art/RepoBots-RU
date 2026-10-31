using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerHealth), "HurtOtherRPC")]
internal static class HurtOtherPatch
{
	private static bool Prefix(PlayerHealth __instance, int damage, Vector3 hurtPosition, bool savingGrace, int enemyIndex, bool hurtByHeal, PhotonMessageInfo _info)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (!BotRegistry.IsBot(__instance.playerAvatar))
		{
			return true;
		}
		if (PhotonNetwork.IsMasterClient && SemiFunc.MasterAndOwnerOnlyRPC(_info, __instance.photonView) && (hurtPosition == Vector3.zero || Vector3.Distance(((Component)__instance.playerAvatar).transform.position, hurtPosition) < 2f))
		{
			BotHealth.Hurt(__instance, damage, savingGrace, enemyIndex, hurtByHeal);
		}
		return false;
	}
}
