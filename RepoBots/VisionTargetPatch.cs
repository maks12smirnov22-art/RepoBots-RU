using HarmonyLib;
using UnityEngine;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVisionTarget), "Update")]
internal static class VisionTargetPatch
{
	private static void Prefix(PlayerVisionTarget __instance)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		BotInfo botInfo = BotRegistry.Get(__instance.PlayerAvatar);
		if (botInfo != null)
		{
			ref float targetPosition = ref __instance.TargetPosition;
			float num;
			if (botInfo.Crawling)
			{
				num = __instance.CrawlPosition;
			}
			else
			{
				num = (botInfo.Crouching ? __instance.CrouchPosition : __instance.StandPosition);
			}
			targetPosition = num;
			__instance.TargetRotation = Quaternion.Euler(botInfo.Pitch, botInfo.Yaw, 0f);
		}
	}
}
