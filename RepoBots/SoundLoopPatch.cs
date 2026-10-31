using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(Sound), "PlayLoop")]
internal static class SoundLoopPatch
{
	private static void Postfix(Sound __instance, bool playing)
	{
		if (playing && BotSenses.Listening)
		{
			BotSenses.ReportLoop(__instance);
		}
	}
}
