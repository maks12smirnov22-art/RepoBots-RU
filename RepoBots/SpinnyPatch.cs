using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(EnemySpinny), "Update")]
internal static class SpinnyPatch
{
	private static void Prefix(EnemySpinny __instance, out bool __state)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		__state = PhotonNetwork.IsMasterClient && (Object)(object)__instance.playerTarget != (Object)null && BotRegistry.IsBot(__instance.playerTarget) && (int)__instance.currentState == 11 && !__instance.effectDone && __instance.RouletteGoingOn();
	}

	private static void Postfix(EnemySpinny __instance, bool __state)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected I4, but got Unknown
		if (!__state || !__instance.effectDone)
		{
			return;
		}
		PlayerAvatar playerTarget = __instance.playerTarget;
		PlayerHealth val = (((Object)(object)playerTarget != (Object)null) ? playerTarget.playerHealth : null);
		if (!((Object)(object)val == (Object)null))
		{
			EnemySpinny.Colors currentColorbyAngle = __instance.GetCurrentColorbyAngle(__instance.targetAngleDegrees);
			Plugin.Log.LogInfo((object)$"Roulette landed on {currentColorbyAngle} for {playerTarget.playerName}");
			switch ((int)currentColorbyAngle)
			{
			case 2:
				val.Heal(25, true);
				break;
			case 0:
				val.Hurt(50, false, -1, false);
				break;
			case 4:
				val.Hurt(Mathf.Max(0, val.health - 1), false, -1, false);
				break;
			case 3:
				val.HealOther(val.maxHealth, true);
				break;
			case 1:
				break;
			}
		}
	}
}
