using HarmonyLib;
using Photon.Pun;

namespace RepoBots;

[HarmonyPatch(typeof(TruckScreenText), "SelfDestructPlayersOutsideTruckRPC")]
internal static class SlackerPatch
{
	private static void Postfix()
	{
		if (PhotonNetwork.IsMasterClient)
		{
			BotTruck.StartSlackers();
		}
	}
}
