using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal class BotNet : MonoBehaviour
{
	public const byte StateEvent = 147;

	private const int Stride = 14;

	private const float SendInterval = 0.05f;

	private float _nextSend;

	private bool _subscribed;

	private void Update()
	{
		if (!_subscribed && PhotonNetwork.NetworkingClient != null)
		{
			PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
			_subscribed = true;
		}
		BotRegistry.Prune();
		BotVoice.Prune();
		bool flag = true;
		foreach (BotInfo value in BotRegistry.Bots.Values)
		{
			BotRegistry.ClaimViews(value);
			if ((Object)(object)value.Avatar != (Object)null)
			{
				StandInForOwner(value.Avatar);
				BotVoice.Ensure(value);
				if (flag)
				{
					RegisterWithEnemies(value.Avatar);
				}
			}
			if (value.HasState && !PhotonNetwork.IsMasterClient)
			{
				Apply(value);
			}
		}
		if (PhotonNetwork.IsMasterClient && BotRegistry.Bots.Count > 0 && Time.unscaledTime >= _nextSend)
		{
			_nextSend = Time.unscaledTime + 0.05f;
			Send();
		}
	}

	private static void StandInForOwner(PlayerAvatar a)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		a.levelAnimationCompleted = true;
		GameDirector instance = GameDirector.instance;
		if ((Object)(object)instance != (Object)null && (int)instance.currentState >= 3 && (int)instance.currentState != 7)
		{
			a.outroDone = true;
		}
	}

	internal static void RegisterWithEnemies(PlayerAvatar a)
	{
		if ((Object)(object)EnemyDirector.instance == (Object)null || EnemyDirector.instance.enemiesSpawned == null || (Object)(object)a.photonView == (Object)null)
		{
			return;
		}
		int viewID = a.photonView.ViewID;
		foreach (EnemyParent item in EnemyDirector.instance.enemiesSpawned)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.Enemy != (Object)null)
			{
				item.Enemy.PlayerAdded(viewID);
			}
		}
	}

	private void LateUpdate()
	{
		foreach (BotInfo value in BotRegistry.Bots.Values)
		{
			BotRegistry.ClaimViews(value);
		}
	}

	private void OnDestroy()
	{
		if (_subscribed && PhotonNetwork.NetworkingClient != null)
		{
			PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
		}
	}

	private static void Send()
	{
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Expected Obj, but got Unknown
		int num = 0;
		foreach (BotInfo value in BotRegistry.Bots.Values)
		{
			if (value.HasState && (Object)(object)value.Avatar != (Object)null)
			{
				num++;
			}
		}
		if (num == 0)
		{
			return;
		}
		int[] array = new int[num];
		float[] array2 = new float[num * 14];
		int num2 = 0;
		foreach (BotInfo value2 in BotRegistry.Bots.Values)
		{
			if (value2.HasState && !((Object)(object)value2.Avatar == (Object)null))
			{
				array[num2] = value2.Avatar.photonView.ViewID;
				int num3 = num2 * 14;
				array2[num3] = value2.Position.x;
				array2[num3 + 1] = value2.Position.y;
				array2[num3 + 2] = value2.Position.z;
				array2[num3 + 3] = value2.Yaw;
				array2[num3 + 4] = value2.Pitch;
				array2[num3 + 5] = value2.Velocity.x;
				array2[num3 + 6] = value2.Velocity.y;
				array2[num3 + 7] = value2.Velocity.z;
				int num4 = (int)((value2.Moving ? 1u : 0u) | (uint)(value2.Sprinting ? 2 : 0) | (uint)(value2.Crouching ? 4 : 0) | (uint)(value2.Crawling ? 8 : 0)) | (value2.Grounded ? 16 : 0);
				array2[num3 + 8] = num4;
				array2[num3 + 9] = value2.HeldView;
				array2[num3 + 10] = value2.Puller.x;
				array2[num3 + 11] = value2.Puller.y;
				array2[num3 + 12] = value2.Puller.z;
				array2[num3 + 13] = (value2.HoldingStatic ? 1f : 0f);
				num2++;
			}
		}
		PhotonNetwork.RaiseEvent((byte)147, (object)new object[2] { array, array2 }, new RaiseEventOptions
		{
			Receivers = (ReceiverGroup)0
		}, SendOptions.SendUnreliable);
	}

	private static void OnEvent(EventData ev)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		if (ev.Code != 147 || !(ev.CustomData is object[] array) || array.Length < 2 || PhotonNetwork.IsMasterClient || !(array[0] is int[] array2) || !(array[1] is float[] array3) || array3.Length < array2.Length * 14)
		{
			return;
		}
		for (int i = 0; i < array2.Length; i++)
		{
			BotInfo byView = BotRegistry.GetByView(array2[i]);
			if (byView != null)
			{
				int num = i * 14;
				byView.Position = new Vector3(array3[num], array3[num + 1], array3[num + 2]);
				byView.Yaw = array3[num + 3];
				byView.Pitch = array3[num + 4];
				byView.Velocity = new Vector3(array3[num + 5], array3[num + 6], array3[num + 7]);
				int num2 = (int)array3[num + 8];
				byView.Moving = (num2 & 1) != 0;
				byView.Sprinting = (num2 & 2) != 0;
				byView.Crouching = (num2 & 4) != 0;
				byView.Crawling = (num2 & 8) != 0;
				byView.Grounded = (num2 & 0x10) != 0;
				byView.HeldView = (int)array3[num + 9];
				byView.Puller = new Vector3(array3[num + 10], array3[num + 11], array3[num + 12]);
				byView.HoldingStatic = array3[num + 13] > 0.5f;
				byView.HasState = true;
			}
		}
	}

	public static void Apply(BotInfo info)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = info.Avatar;
		if (!((Object)(object)avatar == (Object)null))
		{
			Quaternion val = Quaternion.Euler(0f, info.Yaw, 0f);
			avatar.isCrouching = info.Crouching;
			avatar.isSprinting = info.Sprinting;
			avatar.isCrawling = info.Crawling;
			avatar.isSliding = false;
			avatar.isMoving = info.Moving;
			avatar.isGrounded = info.Grounded;
			Vector3 val2 = Quaternion.Inverse(val) * info.Velocity;
			Vector3 val3 = new Vector3(val2.x, 0f, val2.z);
			if (val3.sqrMagnitude > 1f)
			{
				val3.Normalize();
			}
			avatar.InputDirectionRaw = val3;
			avatar.rbVelocity = val2;
			avatar.rbVelocityRaw = info.Velocity;
			avatar.clientPosition = info.Position;
			avatar.clientRotation = val;
			avatar.rotationDisabled = false;
			avatar.rotationOverrideActive = false;
			avatar.clientPhysRiding = false;
			BotGrab.Apply(info);
		}
	}
}
