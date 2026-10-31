using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "OnDestroy")]
internal static class AvatarDestroyPatch
{
	private static void Prefix(PlayerAvatar __instance)
	{
		BotRegistry.Unregister(__instance);
	}
}
