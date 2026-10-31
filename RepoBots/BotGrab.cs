using System;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotGrab
{
	private static readonly int BlockMask = LayerMask.GetMask(new string[7] { "Default", "Player", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject", "Enemy" });

	public static PhysGrabObject Held(BotInfo info)
	{
		if (info.HeldView == 0)
		{
			return null;
		}
		PhotonView val = PhotonView.Find(info.HeldView);
		if (!((Object)(object)val != (Object)null))
		{
			return null;
		}
		return ((Component)val).GetComponent<PhysGrabObject>();
	}

	public static bool Grab(BotInfo info, PhysGrabObject obj, Vector3 eye, Collider area)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)info.Avatar.physGrabber == (Object)null || (Object)(object)obj == (Object)null || (Object)(object)obj.photonView == (Object)null || obj.photonView.ViewID == 0)
		{
			return false;
		}
		if (info.HeldView != 0)
		{
			Release(info);
		}
		if (!PickPoint(obj, eye, area, out var point, out var best))
		{
			return false;
		}
		if (!GrabLine(info, eye, point, ((Component)obj).transform))
		{
			return false;
		}
		return GrabAt(info, obj, point, best);
	}

	public static bool GrabAt(BotInfo info, PhysGrabObject obj, Vector3 point, PhysGrabObjectCollider best)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = info.Avatar;
		PhysGrabber physGrabber = avatar.physGrabber;
		if ((Object)(object)physGrabber == (Object)null || (Object)(object)obj == (Object)null || (Object)(object)best == (Object)null || (Object)(object)obj.photonView == (Object)null || obj.photonView.ViewID == 0)
		{
			return false;
		}
		if (info.HeldView != 0)
		{
			Release(info);
		}
		Transform transform = ((Component)avatar.localCamera).transform;
		if ((Object)(object)obj.rb != (Object)null && obj.rb.IsSleeping())
		{
			obj.OverrideIndestructible(0.5f);
			obj.OverrideBreakEffects(0.5f);
		}
		Vector3 val = transform.InverseTransformDirection(((Component)obj).transform.forward);
		Vector3 val2 = transform.InverseTransformDirection(((Component)obj).transform.up);
		int viewID = physGrabber.photonView.ViewID;
		info.Puller = point;
		physGrabber.physGrabPointPullerPosition = point;
		physGrabber.grabRevision = (physGrabber.grabRevision == int.MaxValue ? 1 : physGrabber.grabRevision + 1);
		obj.GrabLink(viewID, best.colliderID, point, val, val2, physGrabber.grabRevision);
		obj.photonView.RPC("GrabStartedRPC", (RpcTarget)2, new object[1] { viewID });
		physGrabber.photonView.RPC("PhysGrabBeamActivateRPC", (RpcTarget)0, Array.Empty<object>());
		info.HeldView = obj.photonView.ViewID;
		return true;
	}

	public static bool PickPoint(PhysGrabObject obj, Vector3 eye, Collider area, out Vector3 point, out PhysGrabObjectCollider best)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val;
		Bounds bounds;
		if (!((Object)(object)area != (Object)null))
		{
			val = eye;
		}
		else
		{
			bounds = area.bounds;
			val = bounds.center;
		}
		Vector3 val2 = val;
		best = null;
		point = obj.centerPoint;
		float num = float.MaxValue;
		PhysGrabObjectCollider[] componentsInChildren = ((Component)obj).GetComponentsInChildren<PhysGrabObjectCollider>();
		foreach (PhysGrabObjectCollider val3 in componentsInChildren)
		{
			Collider component = ((Component)val3).GetComponent<Collider>();
			if (!((Object)(object)component == (Object)null) && component.enabled && !component.isTrigger)
			{
				Vector3 val4 = component.ClosestPoint(val2);
				Vector3 val5 = val4 - val2;
				float sqrMagnitude = val5.sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					best = val3;
					point = val4;
				}
			}
		}
		if ((Object)(object)best == (Object)null)
		{
			return false;
		}
		if ((Object)(object)area != (Object)null)
		{
			bounds = area.bounds;
			point = bounds.center;
		}
		else if ((Object)(object)obj.roomVolumeCheck != (Object)null && obj.roomVolumeCheck.currentSize.magnitude < 0.5f)
		{
			bounds = ((Component)best).GetComponent<Collider>().bounds;
			point = bounds.center;
		}
		return true;
	}

	public static bool GrabLine(BotInfo info, Vector3 eye, Vector3 point, Transform target)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = point - eye;
		float magnitude = val.magnitude;
		if (magnitude < 0.01f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(eye, val / magnitude, magnitude + 0.05f, BlockMask, (QueryTriggerInteraction)1);
		Array.Sort(array, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
		Transform root = ((Component)info.Avatar).transform.root;
		RaycastHit[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			RaycastHit val2 = array2[num];
			Transform transform = ((Component)val2.collider).transform;
			if (!((Object)(object)transform.root == (Object)(object)root) && (!((Object)(object)info.Body != (Object)null) || !transform.IsChildOf(info.Body.transform)))
			{
				if ((Object)(object)target != (Object)null && transform.IsChildOf(target))
				{
					return true;
				}
				return val2.distance >= magnitude - 0.02f;
			}
		}
		return true;
	}

	public static string WhyNot(BotInfo info, PhysGrabObject obj, Vector3 eye, float reach)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)obj == (Object)null || !PickPoint(obj, eye, null, out var point, out var _))
		{
			return "no solid collider";
		}
		Vector3 val = point - eye;
		float magnitude = val.magnitude;
		if (magnitude > reach)
		{
			return $"{magnitude:0.0}m away, eye y {eye.y:0.0}, point y {point.y:0.0}";
		}
		RaycastHit[] array = Physics.RaycastAll(eye, (point - eye) / magnitude, magnitude + 0.05f, BlockMask, (QueryTriggerInteraction)1);
		Array.Sort(array, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
		Transform root = ((Component)info.Avatar).transform.root;
		RaycastHit[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			RaycastHit val2 = array2[num];
			Transform transform = ((Component)val2.collider).transform;
			if (!((Object)(object)transform.root == (Object)(object)root) && (!((Object)(object)info.Body != (Object)null) || !transform.IsChildOf(info.Body.transform)) && !transform.IsChildOf(((Component)obj).transform))
			{
				return $"blocked by {((Object)transform).name} under {((Object)transform.root).name} (layer {LayerMask.LayerToName(((Component)val2.collider).gameObject.layer)}) at {val2.distance:0.00} of {magnitude:0.00}m";
			}
		}
		return "clear now";
	}

	public static bool CanGrab(BotInfo info, PhysGrabObject obj, Vector3 eye, Collider area, float reach)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)obj == (Object)null || !PickPoint(obj, eye, area, out var point, out var _))
		{
			return false;
		}
		Vector3 val = point - eye;
		if (val.magnitude <= reach)
		{
			return GrabLine(info, eye, point, ((Component)obj).transform);
		}
		return false;
	}

	public static void Release(BotInfo info)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = info.Avatar;
		PhysGrabber val = (((Object)(object)avatar != (Object)null) ? avatar.physGrabber : null);
		PhysGrabObject val2 = Held(info);
		info.HeldView = 0;
		if (!((Object)(object)val == (Object)null))
		{
			if ((Object)(object)val.physGrabPoint != (Object)null)
			{
				val.physGrabPointPullerPosition = val.physGrabPoint.position;
			}
			if ((Object)(object)val2 != (Object)null && (Object)(object)val2.photonView != (Object)null)
			{
				val2.photonView.RPC("GrabEndedRPC", (RpcTarget)2, new object[1] { val.photonView.ViewID });
			}
			val.photonView.RPC("PhysGrabBeamDeactivateRPC", (RpcTarget)0, Array.Empty<object>());
			ClearLocal(val);
		}
	}

	public static void ClearLocal(PhysGrabber g)
	{
		g.grabbed = false;
		g.grabbedObject = null;
		g.grabbedObjectTransform = null;
		g.grabbedPhysGrabObject = null;
	}

	public static void GrabStatic(BotInfo info, StaticGrabObject sgo, Vector3 point)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabber physGrabber = info.Avatar.physGrabber;
		if (!((Object)(object)physGrabber == (Object)null) && !((Object)(object)sgo == (Object)null))
		{
			int viewID = physGrabber.photonView.ViewID;
			info.Puller = point;
			physGrabber.physGrabPointPullerPosition = point;
			physGrabber.grabRevision = (physGrabber.grabRevision == int.MaxValue ? 1 : physGrabber.grabRevision + 1);
			sgo.GrabLink(viewID, point, physGrabber.grabRevision);
			sgo.photonView.RPC("GrabStartedRPC", (RpcTarget)2, new object[1] { viewID });
			physGrabber.photonView.RPC("PhysGrabBeamActivateRPC", (RpcTarget)0, Array.Empty<object>());
			info.HoldingStatic = true;
		}
	}

	public static void ReleaseStatic(BotInfo info, StaticGrabObject sgo)
	{
		PhysGrabber val = (((Object)(object)info.Avatar != (Object)null) ? info.Avatar.physGrabber : null);
		info.HoldingStatic = false;
		if (!((Object)(object)val == (Object)null))
		{
			if ((Object)(object)sgo != (Object)null)
			{
				sgo.photonView.RPC("GrabEndedRPC", (RpcTarget)2, new object[1] { val.photonView.ViewID });
			}
			val.photonView.RPC("PhysGrabBeamDeactivateRPC", (RpcTarget)0, Array.Empty<object>());
			ClearLocal(val);
		}
	}

	public static void Apply(BotInfo info)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabber val = (((Object)(object)info.Avatar != (Object)null) ? info.Avatar.physGrabber : null);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		if (info.HeldView != 0 || info.HoldingStatic)
		{
			val.physGrabPointPullerPosition = info.Puller;
			if ((Object)(object)val.physGrabPointPuller != (Object)null)
			{
				val.physGrabPointPuller.position = info.Puller;
			}
		}
		else if (val.grabbed && !PhotonNetwork.IsMasterClient)
		{
			ClearLocal(val);
		}
	}
}
