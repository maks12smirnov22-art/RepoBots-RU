using System.Collections;
using Photon.Pun;

namespace RepoBots;

internal static class BotOffline
{
	internal static bool Pending;

	internal static bool Active;

	internal static IEnumerator GoOffline(NetworkConnect nc)
	{
		while ((int)PhotonNetwork.NetworkingClient.State != 14 && (int)PhotonNetwork.NetworkingClient.State != 0)
		{
			yield return null;
		}
		nc.RoomName = "RepoBots offline";
		Active = true;
		Plugin.Log.LogInfo((object)"Starting an offline game (singleplayer with bots)");
		PhotonNetwork.OfflineMode = true;
	}
}
