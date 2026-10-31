using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "Start")]
internal static class VoiceStartPatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		return !BotVoice.IsFake(__instance);
	}
}
