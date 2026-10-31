using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(StatsManager), "SaveFileDelete")]
internal static class SaveDeletePatch
{
	private static void Postfix(string saveFileName)
	{
		BotSave.OnDeleted(saveFileName);
	}
}
