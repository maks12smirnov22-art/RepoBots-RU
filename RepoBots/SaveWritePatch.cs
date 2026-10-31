using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(StatsManager), "SaveGame")]
internal static class SaveWritePatch
{
	private static void Postfix(string fileName)
	{
		if (SemiFunc.IsMasterClientOrSingleplayer())
		{
			BotSave.OnSaved(fileName);
		}
	}
}
