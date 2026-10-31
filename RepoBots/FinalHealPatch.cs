using System;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "FinalHealCheck")]
internal static class FinalHealPatch
{
	private static void Postfix(PlayerAvatar __instance)
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Invalid comparison between Unknown and I4
		if (PhotonNetwork.IsMasterClient && BotRegistry.IsBot(__instance) && !__instance.finalHeal && !__instance.isDisabled && (SemiFunc.RunIsLevel() || SemiFunc.RunIsTutorial()) && SemiFunc.PerSecond(5f, (Object)(object)__instance) && RoundDirector.instance.allExtractionPointsCompleted && __instance.RoomVolumeCheck.inTruck && !((Object)(object)TruckHealer.instance == (Object)null) && (int)TruckHealer.instance.currentState == 2)
		{
			int amount;
			if (SemiFunc.MoonLevel() == 1)
			{
				amount = 35;
			}
			else
			{
				amount = ((SemiFunc.MoonLevel() >= 2) ? 25 : 50);
			}
			__instance.photonView.RPC("FinalHealRPC", (RpcTarget)0, Array.Empty<object>());
			BotHealth.Heal(__instance.playerHealth, amount, effect: true);
		}
	}
}
