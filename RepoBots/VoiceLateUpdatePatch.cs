using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(PlayerVoiceChat), "LateUpdate")]
internal static class VoiceLateUpdatePatch
{
	private static void Postfix(PlayerVoiceChat __instance)
	{
		if (BotVoice.IsFake(__instance))
		{
			BotVoice.Pitch(__instance);
		}
	}
}
