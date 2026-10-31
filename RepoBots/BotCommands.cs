using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotCommands
{
	private static readonly RaycastHit[] Hits = new RaycastHit[32];

	private static bool Gaze(PlayerAvatar who, out int view, out Vector3 point, out string label)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_022f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		view = 0;
		point = Vector3.zero;
		label = null;
		Transform val = (((Object)(object)who.localCamera != (Object)null) ? ((Component)who.localCamera).transform : null);
		if ((Object)(object)who == (Object)(object)PlayerAvatar.instance && (Object)(object)CameraUtils.Instance != (Object)null && (Object)(object)CameraUtils.Instance.MainCamera != (Object)null)
		{
			val = ((Component)CameraUtils.Instance.MainCamera).transform;
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		int num = Physics.RaycastNonAlloc(val.position, val.forward, Hits, 40f, -1, (QueryTriggerInteraction)1);
		Array.Sort(Hits, 0, num, Comparer<RaycastHit>.Create((RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance)));
		Transform root = ((Component)who).transform.root;
		for (int num2 = 0; num2 < num; num2++)
		{
			Collider collider = Hits[num2].collider;
			if ((Object)(object)collider == (Object)null || (Object)(object)((Component)collider).transform.root == (Object)(object)root || ((Object)(object)PlayerController.instance != (Object)null && (Object)(object)who == (Object)(object)PlayerAvatar.instance && (Object)(object)((Component)collider).transform.root == (Object)(object)((Component)PlayerController.instance).transform.root))
			{
				continue;
			}
			PlayerAvatar val2 = ((Component)collider).GetComponentInParent<PlayerAvatar>();
			PlayerTumble componentInParent = ((Component)collider).GetComponentInParent<PlayerTumble>();
			if ((Object)(object)val2 == (Object)null && (Object)(object)componentInParent != (Object)null)
			{
				val2 = componentInParent.playerAvatar;
			}
			if ((Object)(object)val2 != (Object)null)
			{
				if (!((Object)(object)val2 == (Object)(object)who) && BotRegistry.IsBot(val2))
				{
					view = val2.photonView.ViewID;
					label = val2.playerName + ": follow me";
					return true;
				}
				continue;
			}
			PhysGrabObject componentInParent2 = ((Component)collider).GetComponentInParent<PhysGrabObject>();
			if ((Object)(object)componentInParent2 != (Object)null && (Object)(object)componentInParent2.photonView != (Object)null)
			{
				view = componentInParent2.photonView.ViewID;
				ref string reference = ref label;
				string text;
				if ((Object)(object)((Component)componentInParent2).GetComponent<PhysGrabCart>() != (Object)null)
				{
					text = "take that cart";
				}
				else
				{
					text = (((Object)(object)((Component)componentInParent2).GetComponent<PlayerDeathHead>() != (Object)null) ? "bring that head back" : "grab that");
				}
				reference = text;
				point = Hits[num2].point;
				return true;
			}
			ExtractionPoint componentInParent3 = ((Component)collider).GetComponentInParent<ExtractionPoint>();
			if ((Object)(object)componentInParent3 != (Object)null && (Object)(object)componentInParent3.photonView != (Object)null)
			{
				view = componentInParent3.photonView.ViewID;
				label = "turn on that extraction";
				return true;
			}
			if (!collider.isTrigger)
			{
				point = Hits[num2].point;
				label = "go there";
				return true;
			}
		}
		return false;
	}

	public static Vector3? GazePoint(PlayerAvatar who)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)who == (Object)null || !Gaze(who, out var _, out var point, out var _) || point == Vector3.zero)
		{
			return null;
		}
		return point;
	}

	public static PhysGrabObject GazeObject(PlayerAvatar who)
	{
		if ((Object)(object)who == (Object)null || !Gaze(who, out var view, out var _, out var _) || view == 0)
		{
			return null;
		}
		PhotonView val = PhotonView.Find(view);
		if (!((Object)(object)val != (Object)null))
		{
			return null;
		}
		return ((Component)val).GetComponent<PhysGrabObject>();
	}

	public static ExtractionPoint NearestIdleExtraction(Vector3 from)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		RoundDirector instance = RoundDirector.instance;
		if ((Object)(object)instance == (Object)null || instance.extractionPointActive)
		{
			return null;
		}
		ExtractionPoint result = null;
		float num = float.MaxValue;
		foreach (GameObject extractionPoint in instance.extractionPointList)
		{
			ExtractionPoint val = (((Object)(object)extractionPoint != (Object)null) ? extractionPoint.GetComponent<ExtractionPoint>() : null);
			if (!((Object)(object)val == (Object)null) && !val.isLocked && val.StateIs((ExtractionPoint.State)1))
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, from);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	public static PhysGrabCart NearestCart(Vector3 from)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabCart result = null;
		float num = float.MaxValue;
		PhysGrabCart[] array = Object.FindObjectsOfType<PhysGrabCart>();
		foreach (PhysGrabCart val in array)
		{
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val.physGrabObject == (Object)null) && !((Object)(object)val.inCart == (Object)null))
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, from);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	public static PlayerAvatar NearestDead(Vector3 from)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar result = null;
		float num = float.MaxValue;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && player.isDisabled && !((Object)(object)player.playerDeathHead == (Object)null) && player.playerDeathHead.triggered && !((Object)(object)player.playerDeathHead.physGrabObject == (Object)null))
			{
				float num2 = Vector3.Distance(player.playerDeathHead.physGrabObject.centerPoint, from) + (BotRegistry.IsBot(player) ? 40f : 0f);
				if (num2 < num)
				{
					num = num2;
					result = player;
				}
			}
		}
		return result;
	}
}
