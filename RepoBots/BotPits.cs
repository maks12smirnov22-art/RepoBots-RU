using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotPits
{
	private static readonly List<Bounds> Pits = new List<Bounds>();

	private static float _scanAt = -99f;

	private static int _solidMask = LayerMask.GetMask(new string[2] { "Default", "PlayerOnlyCollision" });

	private static readonly int TriggerMask = LayerMask.GetMask(new string[1] { "Triggers" });

	private static readonly RaycastHit[] Hits = new RaycastHit[16];

	private static readonly Dictionary<long, KeyValuePair<bool, float>> Cache = new Dictionary<long, KeyValuePair<bool, float>>();

	private static bool _logged;

	public static string LastWhy = "";

	private const float ProbeUp = 1f;

	public static int Count => Pits.Count;

	public static void Tick()
	{
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _scanAt)
		{
			return;
		}
		_scanAt = Time.time + 8f;
		Pits.Clear();
		Cache.Clear();
		HurtCollider[] array = Object.FindObjectsOfType<HurtCollider>();
		foreach (HurtCollider val in array)
		{
			if ((Object)(object)val == (Object)null || !val.deathPit || !((Behaviour)val).isActiveAndEnabled)
			{
				continue;
			}
			Collider[] componentsInChildren = ((Component)val).GetComponentsInChildren<Collider>();
			foreach (Collider val2 in componentsInChildren)
			{
				if ((Object)(object)val2 != (Object)null && val2.enabled)
				{
					Pits.Add(val2.bounds);
				}
			}
		}
		if (Pits.Count > 0 && !_logged)
		{
			_logged = true;
			List<string> list = new List<string>();
			foreach (Bounds pit in Pits)
			{
				Bounds current = pit;
				list.Add($"{current.size.x:F0}x{current.size.y:F0}x{current.size.z:F0} at y {current.min.y:F0}..{current.max.y:F0}");
			}
			Plugin.Log.LogInfo((object)($"Found {Pits.Count} death pit zone(s) in this level: " + string.Join(", ", list)));
		}
		else if (Pits.Count == 0)
		{
			_logged = false;
		}
	}

	public static void LearnFloor(Collider ground)
	{
		if (!((Object)(object)ground == (Object)null) && !ground.isTrigger && !((Object)(object)((Component)ground).GetComponentInParent<PhysGrabObject>() != (Object)null))
		{
			int num = 1 << ((Component)ground).gameObject.layer;
			if ((_solidMask & num) == 0 && LayerMask.LayerToName(((Component)ground).gameObject.layer) != "NavmeshOnly")
			{
				_solidMask |= num;
				Cache.Clear();
				Plugin.Log.LogInfo((object)("Floor layer learned: " + LayerMask.LayerToName(((Component)ground).gameObject.layer)));
			}
		}
	}

	private static bool NearPitZone(Vector3 p, float margin)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		foreach (Bounds pit in Pits)
		{
			Bounds current = pit;
			if (!(p.x < current.min.x - margin) && !(p.x > current.max.x + margin) && !(p.z < current.min.z - margin) && !(p.z > current.max.z + margin) && p.y > current.min.y - 1f && p.y < current.max.y + 25f)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsPit(Vector3 feet)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		long key = (long)(((ulong)Mathf.RoundToInt(feet.x * 2f) & 0xFFFFFuL) | (((ulong)Mathf.RoundToInt(feet.z * 2f) & 0xFFFFFuL) << 20) | (((ulong)Mathf.RoundToInt(feet.y) & 0xFFFFFuL) << 40));
		if (Cache.TryGetValue(key, out var value) && Time.time - value.Value < 3f)
		{
			return value.Key;
		}
		bool flag = Probe(feet);
		if (Cache.Count > 4000)
		{
			Cache.Clear();
		}
		Cache[key] = new KeyValuePair<bool, float>(flag, Time.time);
		return flag;
	}

	private static bool Probe(Vector3 feet)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		int num = Physics.RaycastNonAlloc(feet + Vector3.up * 1f, Vector3.down, Hits, 8f, _solidMask | TriggerMask, (QueryTriggerInteraction)2);
		float num2 = float.MaxValue;
		float num3 = float.MaxValue;
		string arg = null;
		for (int i = 0; i < num; i++)
		{
			Collider collider = Hits[i].collider;
			if ((Object)(object)collider == (Object)null)
			{
				continue;
			}
			if (collider.isTrigger)
			{
				HurtCollider componentInParent = ((Component)collider).GetComponentInParent<HurtCollider>();
				if ((Object)(object)componentInParent != (Object)null && componentInParent.deathPit && Hits[i].distance < num3)
				{
					num3 = Hits[i].distance;
					arg = ((Object)collider).name;
				}
			}
			else if (((1 << ((Component)collider).gameObject.layer) & _solidMask) != 0 && !((Object)(object)((Component)collider).GetComponentInParent<PhysGrabObject>() != (Object)null))
			{
				num2 = Mathf.Min(num2, Hits[i].distance);
			}
		}
		if (num2 - 1f < 1.5f)
		{
			return false;
		}
		if (num3 < num2)
		{
			LastWhy = $"death pit '{arg}' {num3 - 1f:F1}m down, floor " + ((num2 == float.MaxValue) ? "none" : $"{num2 - 1f:F1}m down");
			return true;
		}
		if (num2 == float.MaxValue)
		{
			LastWhy = "no floor within 7m";
			return true;
		}
		return false;
	}

	public static bool FloorAlong(Vector3 from, Vector3 to)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = to - from;
		val.y = 0f;
		float magnitude = val.magnitude;
		if (magnitude < 0.3f)
		{
			return true;
		}
		if (IsPit(from))
		{
			return true;
		}
		int num = Mathf.CeilToInt(magnitude / 0.5f);
		for (int i = 1; i <= num; i++)
		{
			if (IsPit(Vector3.Lerp(from, to, (float)i / (float)num)))
			{
				return false;
			}
		}
		return true;
	}

	public static bool RouteOverPit(List<Vector3> corners, int start)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		if (Pits.Count == 0)
		{
			return false;
		}
		for (int i = Mathf.Max(start, 1); i < corners.Count; i++)
		{
			Vector3 val = corners[i - 1];
			Vector3 val2 = corners[i];
			float num = Vector3.Distance(val, val2);
			int num2 = Mathf.Max(1, Mathf.CeilToInt(num / 0.5f));
			for (int j = 0; j <= num2; j++)
			{
				Vector3 val3 = Vector3.Lerp(val, val2, (float)j / (float)num2);
				if (NearPitZone(val3, 0.3f) && IsPit(val3))
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool SpotOk(Vector3 p)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (NearPitZone(p, 0.5f))
		{
			return !IsPit(p);
		}
		return true;
	}
}
