using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotTruck
{
	private static readonly Dictionary<PlayerAvatar, bool> WasInTruck = new Dictionary<PlayerAvatar, bool>();

	private static PlayerAvatar _lastIn;

	private static float _allInSince = -1f;

	private static bool _sent;

	private static float _sentAt;

	private static float _checkAt;

	private static readonly Dictionary<PlayerAvatar, float> Doomed = new Dictionary<PlayerAvatar, float>();

	private static readonly string[] Countdown = new string[10] { "10...", "9...", "8...", "7...", "6...", "5...", "4...", "3...", "2...", "1..." };

	private static readonly Dictionary<PlayerAvatar, int> Said = new Dictionary<PlayerAvatar, int>();

	public static void ResetLevel()
	{
		WasInTruck.Clear();
		_lastIn = null;
		_allInSince = -1f;
		_sent = false;
		Doomed.Clear();
	}

	public static void Tick()
	{
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		KillSlackers();
		if (Time.time < _checkAt)
		{
			return;
		}
		_checkAt = Time.time + 0.25f;
		if ((Object)(object)TruckScreenText.instance == (Object)null || (Object)(object)GameDirector.instance == (Object)null)
		{
			return;
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && !player.isDisabled && !((Object)(object)player.RoomVolumeCheck == (Object)null))
			{
				bool inTruck = player.RoomVolumeCheck.inTruck;
				WasInTruck.TryGetValue(player, out var value);
				if (inTruck && !value)
				{
					_lastIn = player;
				}
				WasInTruck[player] = inTruck;
			}
		}
		bool num = BotBrain.TruckTime() && !SemiFunc.RunIsShop();
		if (_sent && Time.time - _sentAt > 35f && (int)TruckScreenText.instance.playerChatBoxState == 0)
		{
			_sent = false;
		}
		if (!num || _sent || (int)TruckScreenText.instance.playerChatBoxState != 0 || !SemiFunc.PlayersAllInTruck())
		{
			_allInSince = -1f;
			return;
		}
		if (_allInSince < 0f)
		{
			_allInSince = Time.time;
		}
		if (!((Object)(object)_lastIn == (Object)null) && BotRegistry.IsBot(_lastIn) && !(Time.time - _allInSince < 2.5f))
		{
			BotBrain botBrain = BotManager.BrainOf(_lastIn);
			if (botBrain != null)
			{
				_sent = true;
				_sentAt = Time.time;
				Plugin.Log.LogInfo((object)(_lastIn.playerName + " was last in the truck and is going to the screen"));
				botBrain.StartScreen();
			}
		}
	}

	public static void StartSlackers()
	{
		foreach (BotInfo value in BotRegistry.Bots.Values)
		{
			PlayerAvatar avatar = value.Avatar;
			if (!((Object)(object)avatar == (Object)null) && !avatar.isDisabled && !((Object)(object)avatar.RoomVolumeCheck == (Object)null) && !avatar.RoomVolumeCheck.inTruck)
			{
				Plugin.Log.LogInfo((object)(value.Name + " was left behind"));
				Doomed[avatar] = Time.time + 11f;
				BotManager.BrainOf(avatar)?.ForceTruck();
				avatar.ChatMessageSend("I need to get to the truck in...");
			}
		}
	}

	private static void KillSlackers()
	{
		if (Doomed.Count == 0)
		{
			return;
		}
		List<PlayerAvatar> list = new List<PlayerAvatar>();
		foreach (KeyValuePair<PlayerAvatar, float> item in Doomed)
		{
			PlayerAvatar key = item.Key;
			if ((Object)(object)key == (Object)null || key.isDisabled || ((Object)(object)key.RoomVolumeCheck != (Object)null && key.RoomVolumeCheck.inTruck))
			{
				list.Add(key);
				continue;
			}
			int num = Mathf.Clamp((int)(10f - (item.Value - 1f - Time.time)), 0, 10);
			Said.TryGetValue(key, out var value);
			if (num > value && num <= Countdown.Length)
			{
				key.ChatMessageSend(Countdown[num - 1]);
				Said[key] = num;
			}
			if (Time.time >= item.Value)
			{
				Plugin.Log.LogInfo((object)(key.playerName + " didn't make it to the truck"));
				key.ChatMessageSend("betrayal detected");
				key.playerHealth.health = 0;
				key.playerHealth.photonView.RPC("UpdateHealthRPC", (RpcTarget)1, new object[4]
				{
					0,
					key.playerHealth.maxHealth,
					true,
					false
				});
				key.PlayerDeath(-1);
				list.Add(key);
			}
		}
		foreach (PlayerAvatar item2 in list)
		{
			Doomed.Remove(item2);
			Said.Remove(item2);
		}
	}
}
