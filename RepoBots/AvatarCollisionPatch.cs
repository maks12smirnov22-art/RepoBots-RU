using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatarCollision), "Update")]
internal static class AvatarCollisionPatch
{
	private static void Prefix(PlayerAvatarCollision __instance)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		BotInfo botInfo = BotRegistry.Get(__instance.PlayerAvatar);
		PlayerCollision instance = PlayerCollision.instance;
		if (botInfo != null && !((Object)(object)instance == (Object)null) && !((Object)(object)instance.StandCollision == (Object)null) && !((Object)(object)instance.CrouchCollision == (Object)null))
		{
			__instance.Scale = ((botInfo.Crouching || botInfo.Crawling) ? instance.CrouchCollision.localScale : instance.StandCollision.localScale);
		}
	}
}
