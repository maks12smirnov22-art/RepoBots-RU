using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(StatsManager), "LoadGame")]
internal static class SaveLoadPatch
{
	private static void Postfix(StatsManager __instance, string fileName)
	{
		if (__instance.saveFileCurrent == fileName)
		{
			BotSave.OnLoaded(fileName);
		}
	}
}
