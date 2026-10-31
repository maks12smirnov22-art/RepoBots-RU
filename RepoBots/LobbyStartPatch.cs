using System;
using HarmonyLib;

namespace RepoBots;

[HarmonyPatch(typeof(MenuPageLobby), "Start")]
internal static class LobbyStartPatch
{
	private static void Postfix(MenuPageLobby __instance)
	{
		try
		{
			BotLobbyUI.Setup(__instance);
		}
		catch (Exception arg)
		{
			Plugin.Log.LogError((object)$"Lobby bot buttons failed: {arg}");
		}
	}
}
