using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "SetDebug")]
internal static class VoiceSetDebugPatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		return !BotVoice.IsFake(__instance);
	}
}
