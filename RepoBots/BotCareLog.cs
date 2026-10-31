using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotCareLog
{
	private class Letgo
	{
		public string Bot;

		public float At;

		public float Height;

		public string Why;
	}

	[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "Break")]
	private static class BreakPatch
	{
		private static void Prefix(PhysGrabObjectImpactDetector __instance, float valueLost, Vector3 _contactPoint, int breakLevel)
		{
			//IL_0064: Unknown result type (might be due to invalid IL or missing references)
			//IL_0069: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
			if (!SemiFunc.IsMasterClientOrSingleplayer() || BotRegistry.Bots.Count == 0 || (Object)(object)__instance.physGrabObject == (Object)null)
			{
				return;
			}
			PhysGrabObject physGrabObject = __instance.physGrabObject;
			string text = Blame(physGrabObject, out var held, out var dropped);
			if (held)
			{
				LostHeld += valueLost;
			}
			else
			{
				if (!dropped)
				{
					return;
				}
				LostDropped += valueLost;
			}
			Breaks++;
			Vector3 previousVelocityRaw = __instance.previousVelocityRaw;
			Plugin.Log.LogInfo((object)string.Format("[care] {0} lost ${1:0} (frag {2:0}, level {3}) going {4:0.00} m/s, hit {5}; {6}", ((Object)physGrabObject).name.Replace("(Clone)", ""), valueLost, __instance.fragility * __instance.fragilityMultiplier, breakLevel, previousVelocityRaw.magnitude, HitWhat(__instance, _contactPoint), text));
		}
	}

	[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), "DestroyObject")]
	private static class DestroyPatch
	{
		private static void Prefix(PhysGrabObjectImpactDetector __instance)
		{
			if (SemiFunc.IsMasterClientOrSingleplayer() && BotRegistry.Bots.Count != 0 && !((Object)(object)__instance.physGrabObject == (Object)null) && !__instance.physGrabObject.dead && !((Object)(object)((Component)__instance).GetComponent<ValuableObject>() == (Object)null))
			{
				string text = Blame(__instance.physGrabObject, out var held, out var dropped);
				if (held || dropped)
				{
					Destroyed++;
					Plugin.Log.LogInfo((object)("[care] " + ((Object)__instance.physGrabObject).name.Replace("(Clone)", "") + " DESTROYED; " + text));
				}
			}
		}
	}

	private static readonly Dictionary<PhysGrabObject, Letgo> LetGo = new Dictionary<PhysGrabObject, Letgo>();

	public static float LostHeld;

	public static float LostDropped;

	public static int Breaks;

	public static int Destroyed;

	public static void Reset()
	{
		LostHeld = (LostDropped = 0f);
		Breaks = (Destroyed = 0);
		LetGo.Clear();
	}

	public static void Released(BotInfo bot, PhysGrabObject obj, string why)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)obj == (Object)null) && !obj.dead && bot != null)
		{
			RaycastHit val = default;
			float height = (Physics.Raycast(obj.centerPoint, Vector3.down, out val, 5f, LayerMask.GetMask(new string[1] { "Default" }), (QueryTriggerInteraction)1) ? val.distance : 5f);
			LetGo[obj] = new Letgo
			{
				Bot = bot.Name,
				At = Time.time,
				Height = height,
				Why = why
			};
		}
	}

	private static string Blame(PhysGrabObject o, out bool held, out bool dropped)
	{
		held = (dropped = false);
		foreach (PlayerAvatar item in o.playerGrabbing.ConvertAll((PhysGrabber g) => (!((Object)(object)g != (Object)null)) ? null : g.playerAvatar))
		{
			if ((Object)(object)item != (Object)null && BotRegistry.IsBot(item))
			{
				held = true;
				BotBrain botBrain = BotManager.BrainOf(item);
				return "held by " + item.playerName + ((botBrain != null) ? (" (" + botBrain.CareInfo(o) + ")") : "");
			}
		}
		if (o.playerGrabbing.Count > 0)
		{
			return "held by a person";
		}
		if (LetGo.TryGetValue(o, out var value) && Time.time - value.At < 3f)
		{
			dropped = true;
			return $"{value.Bot} let go {Time.time - value.At:0.0}s ago from {value.Height:0.00}m up ({value.Why})";
		}
		return "nobody";
	}

	private static string HitWhat(PhysGrabObjectImpactDetector d, Vector3 contact)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		string result = "?";
		float num = 0.4f;
		Collider[] array = Physics.OverlapSphere(contact, 0.25f, -1, (QueryTriggerInteraction)1);
		foreach (Collider val in array)
		{
			if (!((Object)(object)val == (Object)null) && !((Component)val).transform.IsChildOf(((Component)d).transform))
			{
				float num2 = Vector3.Distance(val.ClosestPoint(contact), contact);
				if (num2 < num)
				{
					num = num2;
					PlayerAvatar componentInParent = ((Component)val).GetComponentInParent<PlayerAvatar>();
					result = (((Object)(object)componentInParent != (Object)null) ? ("player " + componentInParent.playerName) : (((Object)val).name + " (" + LayerMask.LayerToName(((Component)val).gameObject.layer) + ")"));
				}
			}
		}
		return result;
	}
}
