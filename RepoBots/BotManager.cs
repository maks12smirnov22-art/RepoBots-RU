using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal class BotManager : MonoBehaviour
{
	internal static readonly string[] Names = new string[10] { "Bolt", "Rusty", "Sprocket", "Gizmo", "Widget", "Cog", "Pip", "Noodle", "Tater", "Dingus" };

	private readonly Dictionary<PlayerAvatar, BotBrain> _brains = new Dictionary<PlayerAvatar, BotBrain>();

	private static BotManager _instance;

	private LevelGenerator _spawnedFor;

	private bool _spawning;

	private bool _failedLevel;

	private float _peopleDeadSince = -1f;

	public static IEnumerable<BotBrain> Brains
	{
		get
		{
			if (!((Object)(object)_instance != (Object)null))
			{
				return Array.Empty<BotBrain>();
			}
			return _instance._brains.Values;
		}
	}

	public static MonoBehaviour Runner => (MonoBehaviour)(object)_instance;

	public static BotBrain BrainOf(PlayerAvatar a)
	{
		if (!((Object)(object)_instance != (Object)null) || !((Object)(object)a != (Object)null) || !_instance._brains.TryGetValue(a, out var value))
		{
			return null;
		}
		return value;
	}

	private void Awake()
	{
		_instance = this;
	}

	private void Update()
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		if (!SemiFunc.IsMultiplayer() || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
		{
			return;
		}
		BotSave.Tick();
		if (!LevelReady())
		{
			return;
		}
		if ((Object)(object)LevelGenerator.Instance != (Object)(object)_spawnedFor)
		{
			_spawnedFor = LevelGenerator.Instance;
			_brains.Clear();
			BotMood.Reset();
			BotBrain.ResetLevel();
			BotTruck.ResetLevel();
			_failedLevel = false;
			_peopleDeadSince = -1f;
			((MonoBehaviour)this).StartCoroutine(SpawnUpTo(Mathf.Clamp(Plugin.BotCount.Value, 0, 2)));
		}
		if (Input.GetKeyDown(Plugin.AddKey.Value) && !_spawning)
		{
			Plugin.BotCount.Value = Mathf.Min(Plugin.BotCount.Value + 1, 2);
			((MonoBehaviour)this).StartCoroutine(SpawnUpTo(Mathf.Clamp(Plugin.BotCount.Value, 0, 2)));
		}
		if (Input.GetKeyDown(Plugin.RemoveKey.Value))
		{
			Plugin.BotCount.Value = 0;
			RemoveAll();
		}
		float deltaTime = Time.deltaTime;
		foreach (BotInfo value2 in BotRegistry.Bots.Values)
		{
			if (!((Object)(object)value2.Avatar == (Object)null))
			{
				if (!_brains.TryGetValue(value2.Avatar, out var value))
				{
					value = new BotBrain(value2);
					_brains[value2.Avatar] = value;
				}
				value.Tick(deltaTime);
				BotNet.Apply(value2);
			}
		}
		CheckPeopleDead();
		BotTruck.Tick();
		BotPits.Tick();
		BotOnScreen.Tick();
	}

	private static bool LevelReady()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Invalid comparison between Unknown and I4
		if ((Object)(object)LevelGenerator.Instance == (Object)null || !LevelGenerator.Instance.Generated)
		{
			return false;
		}
		if ((Object)(object)GameDirector.instance == (Object)null || (int)GameDirector.instance.currentState != 2)
		{
			return false;
		}
		if (SemiFunc.MenuLevel() || SemiFunc.RunIsLobbyMenu() || SemiFunc.RunIsArena())
		{
			return false;
		}
		if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby())
		{
			return false;
		}
		if ((Object)(object)PlayerAvatar.instance != (Object)null)
		{
			return PlayerAvatar.instance.spawned;
		}
		return false;
	}

	private static bool PaintColor(PlayerCosmetics pc, int color)
	{
		if ((Object)(object)pc == (Object)null || (Object)(object)pc.photonView == (Object)null || pc.colorsEquipped == null)
		{
			return false;
		}
		bool flag = pc.colorsEquipped.Length != 0;
		int[] colorsEquipped = pc.colorsEquipped;
		for (int i = 0; i < colorsEquipped.Length; i++)
		{
			if (colorsEquipped[i] != color)
			{
				flag = false;
			}
		}
		if (flag)
		{
			return true;
		}
		PhotonNetwork.RemoveBufferedRPCs(pc.photonView.ViewID, "SetupColorsAllRPC", (int[])null);
		pc.photonView.RPC("SetupColorsAllRPC", (RpcTarget)3, new object[1] { color });
		return false;
	}

	private IEnumerator SpawnUpTo(int count)
	{
		_spawning = true;
		yield return (object)new WaitForSeconds(0.25f);
		List<Vector3> used = new List<Vector3>();
		for (int index = 0; index < count && index < 2; index++)
		{
			if (!LevelReady())
			{
				break;
			}
			bool flag = false;
			foreach (BotInfo value in BotRegistry.Bots.Values)
			{
				if (value.Index == index && (Object)(object)value.Avatar != (Object)null)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				SpawnBot(index, used);
				yield return null;
			}
		}
		_spawning = false;
	}

	private static void PickSpawn(List<Vector3> used, out Vector3 pos, out Quaternion rot)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar instance = PlayerAvatar.instance;
		List<SpawnPoint> list = new List<SpawnPoint>(Object.FindObjectsOfType<SpawnPoint>());
		if (list.Exists((SpawnPoint p) => (Object)(object)p != (Object)null && p.debug))
		{
			list.RemoveAll((SpawnPoint p) => (Object)(object)p == (Object)null || !p.debug);
		}
		list.RemoveAll((SpawnPoint p) => (Object)(object)p == (Object)null);
		ListExtension.Shuffle<SpawnPoint>((IList<SpawnPoint>)list);
		foreach (SpawnPoint item in list)
		{
			if (!Taken(((Component)item).transform.position))
			{
				pos = ((Component)item).transform.position;
				rot = Quaternion.Euler(0f, ((Component)item).transform.eulerAngles.y, 0f);
				used.Add(pos);
				return;
			}
		}
		if (list.Count > 0)
		{
			SpawnPoint val = list[used.Count % list.Count];
			pos = BotBrain.FindSpotNear(((Component)val).transform.position, 0.9f, 2.5f);
			rot = Quaternion.Euler(0f, ((Component)val).transform.eulerAngles.y, 0f);
			used.Add(pos);
		}
		else
		{
			pos = BotBrain.FindSpotNear(((Component)instance).transform.position, 1.5f, 3f);
			rot = Quaternion.Euler(0f, ((Component)instance).transform.eulerAngles.y, 0f);
			used.Add(pos);
		}
		bool Taken(Vector3 at)
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			foreach (Vector3 item2 in used)
			{
				if (Vector3.Distance(item2, at) < 0.9f)
				{
					return true;
				}
			}
			foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
			{
				if ((Object)(object)player != (Object)null && Vector3.Distance(((Component)player).transform.position, at) < 0.9f)
				{
					return true;
				}
			}
			return false;
		}
	}

	private void SpawnBot(int index, List<Vector3> used)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		PickSpawn(used, out var pos, out var rot);
		string text = "7656119000000" + (1000 + index);
		int num = BotColors.For(text);
		object[] array = new object[5]
		{
			"RepoBot",
			index,
			Names[index],
			text,
			num
		};
		GameObject val = PhotonNetwork.Instantiate(((Object)NetworkManager.instance.playerAvatarPrefab).name, pos, rot, (byte)0, array);
		PlayerAvatar val2 = (((Object)(object)val != (Object)null) ? val.GetComponentInChildren<PlayerAvatar>(true) : null);
		BotInfo botInfo = BotRegistry.Get(val2);
		if (botInfo == null)
		{
			Plugin.Log.LogError((object)"Bot spawn failed: the avatar didn't register as a bot");
			if ((Object)(object)val != (Object)null)
			{
				PhotonNetwork.Destroy(val);
			}
		}
		else
		{
			botInfo.Position = pos;
			botInfo.Yaw = rot.eulerAngles.y;
			botInfo.HasState = true;
			((MonoBehaviour)this).StartCoroutine(Dress(val2, botInfo, pos, rot, num));
		}
	}

	private IEnumerator Dress(PlayerAvatar avatar, BotInfo info, Vector3 pos, Quaternion rot, int color)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		yield return null;
		if ((Object)(object)avatar == (Object)null)
		{
			yield break;
		}
		avatar.Spawn(pos, rot);
		BotHealth.Init(avatar);
		if ((Object)(object)avatar.playerDeathHead == (Object)null && (Object)(object)LevelGenerator.Instance.PlayerDeathHeadPrefab != (Object)null)
		{
			PlayerDeathHead component = PhotonNetwork.Instantiate(((Object)LevelGenerator.Instance.PlayerDeathHeadPrefab).name, AssetManager.instance.physDisabledPosition, Quaternion.identity, (byte)0, (object[])null).GetComponent<PlayerDeathHead>();
			component.playerAvatar = avatar;
			avatar.playerDeathHead = component;
		}
		if ((Object)(object)avatar.tumble == (Object)null && (Object)(object)LevelGenerator.Instance.PlayerTumblePrefab != (Object)null)
		{
			PlayerTumble component2 = PhotonNetwork.Instantiate(((Object)LevelGenerator.Instance.PlayerTumblePrefab).name, AssetManager.instance.physDisabledPosition, Quaternion.identity, (byte)0, (object[])null).GetComponent<PlayerTumble>();
			component2.playerAvatar = avatar;
			avatar.tumble = component2;
		}
		Plugin.Log.LogInfo((object)("Spawned bot " + info.Name));
		bool bodyDone = false;
		bool headDone = false;
		for (int tries = 0; tries < 18; tries++)
		{
			if (!((Object)(object)avatar != (Object)null))
			{
				break;
			}
			if (bodyDone & headDone)
			{
				break;
			}
			yield return (object)new WaitForSeconds(0.5f);
			if ((Object)(object)avatar == (Object)null)
			{
				break;
			}
			if (!bodyDone)
			{
				bodyDone = PaintColor(avatar.playerCosmetics, color);
			}
			if (!headDone)
			{
				PlayerDeathHead playerDeathHead = avatar.playerDeathHead;
				headDone = (Object)(object)playerDeathHead != (Object)null && playerDeathHead.setup && PaintColor(playerDeathHead.playerCosmetics, color);
			}
		}
		if (!headDone)
		{
			Plugin.Log.LogInfo((object)(info.Name + "'s head didn't take its color yet"));
		}
		if (!((Object)(object)avatar != (Object)null))
		{
			yield break;
		}
		int[] array = BotLooks.CosmeticsFor(info).ToArray();
		PlayerCosmetics[] array2 = new PlayerCosmetics[2]
		{
			avatar.playerCosmetics,
			((Object)(object)avatar.playerDeathHead != (Object)null) ? avatar.playerDeathHead.playerCosmetics : null
		};
		foreach (PlayerCosmetics val in array2)
		{
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val.photonView == (Object)null))
			{
				PhotonNetwork.RemoveBufferedRPCs(val.photonView.ViewID, "SetupCosmeticsRPC", (int[])null);
				val.photonView.RPC("SetupCosmeticsRPC", (RpcTarget)3, new object[2] { array, false });
			}
		}
	}

	private void RemoveAll()
	{
		foreach (BotInfo item in new List<BotInfo>(BotRegistry.Bots.Values))
		{
			PlayerAvatar avatar = item.Avatar;
			if (!((Object)(object)avatar == (Object)null))
			{
				if ((Object)(object)avatar.playerDeathHead != (Object)null)
				{
					PhotonNetwork.Destroy(((Component)avatar.playerDeathHead).gameObject);
				}
				if ((Object)(object)avatar.tumble != (Object)null)
				{
					PhotonNetwork.Destroy(((Component)avatar.tumble).gameObject);
				}
				if (_brains.TryGetValue(avatar, out var value))
				{
					value.Forget();
				}
				PhotonNetwork.Destroy(avatar.photonView);
			}
		}
		_brains.Clear();
		Plugin.Log.LogInfo((object)"Removed all bots");
	}

	private void CheckPeopleDead()
	{
		if (_failedLevel || !SemiFunc.RunIsLevel())
		{
			return;
		}
		RunManager instance = RunManager.instance;
		if ((Object)(object)instance == (Object)null || instance.restarting || instance.allPlayersDeadCheckDisabled || !instance.runStarted)
		{
			return;
		}
		bool flag = false;
		bool flag2 = false;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player == (Object)null)
			{
				continue;
			}
			if (BotRegistry.IsBot(player))
			{
				flag2 |= !player.isDisabled;
				continue;
			}
			flag = true;
			if (player.isDisabled)
			{
				continue;
			}
			_peopleDeadSince = -1f;
			return;
		}
		if (!flag || !flag2)
		{
			_peopleDeadSince = -1f;
			return;
		}
		if (_peopleDeadSince < 0f)
		{
			_peopleDeadSince = Time.time;
			Plugin.Log.LogInfo((object)"Every real player is dead, the bots are on their own");
		}
		if (Plugin.FailWhenPeopleDie.Value || !(Time.time - _peopleDeadSince < Plugin.ReviveGiveUpSeconds.Value))
		{
			_failedLevel = true;
			Plugin.Log.LogInfo((object)"Failing the level: nobody got revived");
			instance.AllPlayersDeadSet(true);
			instance.ChangeLevel(false, true, (RunManager.ChangeLevelType)0);
		}
	}
}
