using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerLocalCamera), "OnPhotonSerializeView")]
internal static class LocalCameraSerializePatch
{
	private static bool Prefix(PlayerLocalCamera __instance)
	{
		return !BotRegistry.IsBot(__instance.playerAvatar);
	}
}
