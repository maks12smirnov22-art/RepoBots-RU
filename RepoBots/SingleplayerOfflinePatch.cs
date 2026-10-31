using System.Collections.Generic;
using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(SemiFunc), "MenuActionSingleplayerGame")]
internal static class SingleplayerOfflinePatch
{
	private static bool Prefix(string saveFileName, List<string> saveFileBackups)
	{
		if (!Plugin.SingleplayerBots.Value)
		{
			return true;
		}
		BotOffline.Pending = true;
		SemiFunc.MenuActionHostGame(saveFileName, saveFileBackups);
		return false;
	}
}
