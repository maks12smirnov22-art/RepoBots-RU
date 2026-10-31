using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerAvatar), "OnPhotonSerializeView")]
internal static class AvatarSerializePatch
{
	private static bool Prefix(PlayerAvatar __instance)
	{
		return !BotRegistry.IsBot(__instance);
	}
}
