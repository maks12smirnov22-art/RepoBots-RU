using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotRegistry
{
	public const string Marker = "RepoBot";

	public const int FakeActorBase = 990000;

	public static readonly Dictionary<PlayerAvatar, BotInfo> Bots = new Dictionary<PlayerAvatar, BotInfo>();

	private static readonly Dictionary<int, BotInfo> ByView = new Dictionary<int, BotInfo>();

	public static bool IsBot(PlayerAvatar a)
	{
		if ((Object)(object)a != (Object)null)
		{
			return Bots.ContainsKey(a);
		}
		return false;
	}

	public static BotInfo Get(PlayerAvatar a)
	{
		if (!((Object)(object)a != (Object)null) || !Bots.TryGetValue(a, out var value))
		{
			return null;
		}
		return value;
	}

	public static BotInfo GetByView(int viewId)
	{
		if (!ByView.TryGetValue(viewId, out var value))
		{
			return null;
		}
		return value;
	}

	public static BotInfo TryRegister(PlayerAvatar a, PhotonView pv)
	{
		object[] array = (((Object)(object)pv != (Object)null) ? pv.InstantiationData : null);
		if (array == null || array.Length < 5 || !(array[0] is string text) || text != "RepoBot")
		{
			return null;
		}
		if (Bots.TryGetValue(a, out var value))
		{
			return value;
		}
		BotInfo botInfo = new BotInfo
		{
			Avatar = a,
			Index = (int)array[1],
			Name = (string)array[2],
			SteamId = (string)array[3],
			Color = (int)array[4],
			Views = ((Component)((Component)a).transform.root).GetComponentsInChildren<PhotonView>(true)
		};
		Bots[a] = botInfo;
		ByView[pv.ViewID] = botInfo;
		ClaimViews(botInfo);
		return botInfo;
	}

	public static void Unregister(PlayerAvatar a)
	{
		if (Bots.TryGetValue(a, out var _))
		{
			Bots.Remove(a);
			if ((Object)(object)a.photonView != (Object)null)
			{
				ByView.Remove(a.photonView.ViewID);
			}
		}
	}

	public static void ClaimViews(BotInfo info)
	{
		int num = 990000 + info.Index;
		if (info.Views == null)
		{
			return;
		}
		PhotonView[] views = info.Views;
		foreach (PhotonView val in views)
		{
			if ((Object)(object)val != (Object)null && val.ControllerActorNr != num)
			{
				val.ControllerActorNr = num;
			}
		}
	}

	public static void Prune()
	{
		List<PlayerAvatar> list = null;
		foreach (KeyValuePair<PlayerAvatar, BotInfo> bot in Bots)
		{
			if ((Object)(object)bot.Key == (Object)null)
			{
				(list ?? (list = new List<PlayerAvatar>())).Add(bot.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (PlayerAvatar item in list)
		{
			Bots.Remove(item);
		}
		List<int> list2 = new List<int>();
		foreach (KeyValuePair<int, BotInfo> item2 in ByView)
		{
			if ((Object)(object)item2.Value.Avatar == (Object)null)
			{
				list2.Add(item2.Key);
			}
		}
		foreach (int item3 in list2)
		{
			ByView.Remove(item3);
		}
	}
}
