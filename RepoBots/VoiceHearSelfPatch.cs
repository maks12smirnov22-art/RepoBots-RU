using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "OverrideHearSelf")]
internal static class VoiceHearSelfPatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		return !BotVoice.IsFake(__instance);
	}
}
