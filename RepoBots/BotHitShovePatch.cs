using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerController), "ForceImpulse")]
internal static class BotHitShovePatch
{
	private static bool Prefix(Vector3 force)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar hittingBot = HurtColliderPatch.HittingBot;
		if ((Object)(object)hittingBot == (Object)null)
		{
			return true;
		}
		BotManager.BrainOf(hittingBot)?.Shove(force);
		return false;
	}
}
