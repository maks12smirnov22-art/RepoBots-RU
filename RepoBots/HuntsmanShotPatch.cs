using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(EnemyHunter), "ShootRPC")]
internal static class HuntsmanShotPatch
{
	private static void Postfix(EnemyHunter __instance, Vector3 _hitPosition)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		if (!PhotonNetwork.IsMasterClient || (Object)(object)__instance.hurtCollider == (Object)null)
		{
			return;
		}
		foreach (BotInfo value in BotRegistry.Bots.Values)
		{
			PlayerAvatar avatar = value.Avatar;
			if ((Object)(object)avatar == (Object)null || avatar.isDisabled)
			{
				continue;
			}
			Collider val = BotManager.BrainOf(avatar)?.BodyCollider;
			float num = Vector3.Distance(((Object)(object)val != (Object)null && val.enabled) ? val.ClosestPoint(_hitPosition) : (((Component)avatar).transform.position + Vector3.up), _hitPosition);
			if (!(num > 1.5f))
			{
				Plugin.Log.LogInfo((object)$"Huntsman shot {num:0.00}m from {value.Name}");
				if (num < 0.45f)
				{
					__instance.hurtCollider.PlayerHurt(avatar);
				}
			}
		}
	}
}
