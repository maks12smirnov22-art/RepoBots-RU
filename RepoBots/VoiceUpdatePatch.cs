using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "Update")]
internal static class VoiceUpdatePatch
{
	private static bool Prefix(PlayerVoiceChat __instance)
	{
		if (!BotVoice.IsFake(__instance))
		{
			return true;
		}
		BotVoice.Tick(__instance);
		return false;
	}
}
