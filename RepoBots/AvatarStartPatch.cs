using HarmonyLib;
using UnityEngine;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "Start")]
internal static class AvatarStartPatch
{
	private static bool Prefix(PlayerAvatar __instance)
	{
		if (!BotRegistry.IsBot(__instance))
		{
			return true;
		}
		__instance.overridePupilSizeSpring.speed = 15f;
		__instance.overridePupilSizeSpring.damping = 0.3f;
		__instance.deadTimer = __instance.deadTime;
		__instance.SoundsSetup();
		((MonoBehaviour)__instance).StartCoroutine(__instance.LateStart());
		return false;
	}
}
