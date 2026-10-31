using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(StatsManager), "SaveFileCreate")]
internal static class SaveCreatePatch
{
	private static void Postfix(StatsManager __instance)
	{
		BotSave.OnCreated(__instance.saveFileCurrent);
	}
}
