using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch(typeof(SteamManager), "SetLobbyData")]
internal static class OfflineLobbyDataPatch
{
	private static bool Prefix()
	{
		return !PhotonNetwork.OfflineMode;
	}
}
