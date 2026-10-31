using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "OnDestroy")]
internal static class VoiceDestroyPatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		return !BotVoice.IsFake(__instance);
	}
}
