using System;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotOnScreen
{
	private static float _nextAt;

	private static readonly RaycastHit[] Hits = new RaycastHit[16];

	public static void Tick()
	{
		if (!PhotonNetwork.IsMasterClient || Time.time < _nextAt)
		{
			return;
		}
		_nextAt = Time.time + 0.2f;
		if ((Object)(object)EnemyDirector.instance == (Object)null || EnemyDirector.instance.enemiesSpawned == null || BotRegistry.Bots.Count == 0)
		{
			return;
		}
		Camera val = (((Object)(object)CameraUtils.Instance != (Object)null) ? CameraUtils.Instance.MainCamera : null);
		float num = (((Object)(object)val != (Object)null) ? val.fieldOfView : 70f);
		float num2 = (((Object)(object)val != (Object)null && val.aspect > 0.1f) ? val.aspect : 1.7777778f);
		float num3 = Mathf.Tan(num * 0.5f * (MathF.PI / 180f));
		float tanH = num3 * num2;
		foreach (EnemyParent item in EnemyDirector.instance.enemiesSpawned)
		{
			Enemy val2 = (((Object)(object)item != (Object)null) ? item.Enemy : null);
			if ((Object)(object)val2 == (Object)null || !val2.HasOnScreen || (Object)(object)val2.OnScreen == (Object)null || !((Component)val2).gameObject.activeInHierarchy)
			{
				continue;
			}
			EnemyOnScreen onScreen = val2.OnScreen;
			foreach (BotInfo value in BotRegistry.Bots.Values)
			{
				PlayerAvatar avatar = value.Avatar;
				if ((Object)(object)avatar == (Object)null || (Object)(object)avatar.photonView == (Object)null)
				{
					continue;
				}
				int viewID = avatar.photonView.ViewID;
				if (onScreen.OnScreenPlayer.ContainsKey(viewID))
				{
					bool on = false;
					bool culled = true;
					if (!avatar.isDisabled && !avatar.isTumbling)
					{
						Check(onScreen, val2, value, tanH, num3, out on, out culled);
					}
					onScreen.OnScreenPlayer[viewID] = on;
					onScreen.CulledPlayer[viewID] = culled;
				}
			}
		}
	}

	private static void Check(EnemyOnScreen os, Enemy e, BotInfo info, float tanH, float tanV, out bool on, out bool culled)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		on = false;
		culled = true;
		Vector3 position = info.Position;
		Vector3 up = Vector3.up;
		float num;
		if (info.Crawling)
		{
			num = 0.45f;
		}
		else
		{
			num = (info.Crouching ? 1f : 1.5f);
		}
		Vector3 val = position + up * num;
		Quaternion val2 = Quaternion.Inverse(Quaternion.Euler(info.Pitch, info.Yaw, 0f));
		Transform[] points = os.points;
		if (points == null)
		{
			return;
		}
		Transform[] array = points;
		foreach (Transform val3 in array)
		{
			if ((Object)(object)val3 == (Object)null)
			{
				continue;
			}
			Vector3 position2 = val3.position;
			if (Vector3.Distance(position2, val) > os.maxDistance)
			{
				continue;
			}
			Vector3 val4 = val2 * (position2 - val);
			if (val4.z <= 0.05f)
			{
				continue;
			}
			float num2 = val4.x / (val4.z * tanH);
			float num3 = val4.y / (val4.z * tanV);
			if (!(num2 < -1f - 2f * os.paddingWidth) && !(num2 > 1f + 2f * os.paddingWidth) && !(num3 < -1f - 2f * os.paddingHeight) && !(num3 > 1f + 2f * os.paddingHeight))
			{
				culled = false;
				if (ClearToward(position2, val, e, info))
				{
					on = true;
					break;
				}
			}
		}
	}

	private static bool ClearToward(Vector3 from, Vector3 eye, Enemy e, BotInfo info)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = eye - from;
		float num = Mathf.Min(val.magnitude, 12f);
		int num2 = Physics.RaycastNonAlloc(from, val.normalized, Hits, num, (int)e.VisionMask, (QueryTriggerInteraction)1);
		Transform val2 = (((Object)(object)info.Avatar != (Object)null) ? ((Component)info.Avatar).transform.root : null);
		for (int i = 0; i < num2; i++)
		{
			Transform transform = Hits[i].transform;
			if (!((Object)(object)transform == (Object)null) && !((Object)(object)transform.root == (Object)(object)((Component)e).transform.root) && !((Object)(object)((Component)transform).GetComponentInParent<EnemyRigidbody>() != (Object)null) && !((Object)(object)transform.root == (Object)(object)val2) && (!((Object)(object)info.Body != (Object)null) || !((Object)(object)transform.root == (Object)(object)info.Body.transform.root)) && !((Component)transform).CompareTag("Player") && !((Object)(object)((Component)transform).GetComponentInParent<PlayerTumble>() != (Object)null))
			{
				return false;
			}
		}
		return true;
	}
}
