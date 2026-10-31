using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "Awake")]
internal static class VoiceAwakePatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		return !BotVoice.IsFake(__instance);
	}
}
