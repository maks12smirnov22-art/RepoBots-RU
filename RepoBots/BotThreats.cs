using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotThreats
{
	private class TypeInfo
	{
		public FieldInfo State;

		public FieldInfo[] Targets;
	}

	private static readonly string[] AngryWords = new string[40]
	{
		"Attack", "GoTo", "Chase", "Notice", "Seek", "TargetPlayer", "Aggro", "Yell", "Aim", "Shoot",
		"Dive", "Wrestle", "Lure", "Spotted", "Tell", "Throw", "Slap", "Follow", "MoveToPlayer", "PlayerGoTo",
		"PlayerPickup", "PlayerMove", "Bite", "Tumble", "Melee", "Charge", "Chomp", "Puke", "Attached", "BellRing",
		"Roulette", "Sneak", "LookUnder", "CreepyStare", "Angry", "HasTarget", "OnScreen", "Fuse", "Transform", "Damage"
	};

	private static readonly string[] CalmWords = new string[12]
	{
		"Idle", "Roam", "Spawn", "Leave", "Despawn", "Stun", "Dead", "Death", "Small", "Cool",
		"Lost", "Disabled"
	};

	private static readonly string[] TargetFields = new string[7] { "playerTarget", "targetPlayer", "actualPlayerTarget", "currentTarget", "attachTarget", "grabbedPlayer", "playerAvatar" };

	private static readonly Dictionary<Type, TypeInfo> Cache = new Dictionary<Type, TypeInfo>();

	private static readonly Dictionary<EnemyParent, MonoBehaviour> Brains = new Dictionary<EnemyParent, MonoBehaviour>();

	private static readonly List<Threat> Current = new List<Threat>();

	private static int _frame = -1;

	public static List<Threat> All()
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Invalid comparison between Unknown and I4
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Invalid comparison between Unknown and I4
		if (_frame == Time.frameCount)
		{
			return Current;
		}
		_frame = Time.frameCount;
		Current.Clear();
		if ((Object)(object)EnemyDirector.instance == (Object)null || EnemyDirector.instance.enemiesSpawned == null)
		{
			return Current;
		}
		foreach (EnemyParent item in EnemyDirector.instance.enemiesSpawned)
		{
			if ((Object)(object)item == (Object)null || !item.Spawned || (Object)(object)item.Enemy == (Object)null || !((Component)item.Enemy).gameObject.activeInHierarchy)
			{
				continue;
			}
			Threat threat = Read(item);
			if (threat != null)
			{
				Current.Add(threat);
			}
			if (threat != null && threat.Kind == "EnemyBombThrower")
			{
				EnemyBombThrowerHead componentInChildren = ((Component)item).GetComponentInChildren<EnemyBombThrowerHead>();
				if ((Object)(object)componentInChildren != (Object)null && ((int)componentInChildren.currentState == 2 || (int)componentInChildren.currentState == 3))
				{
					Current.Add(new Threat
					{
						Parent = item,
						Kind = "EnemyBombThrowerHead",
						State = ((object)componentInChildren.currentState/*cast due to constrained. prefix*/).ToString(),
						Position = ((Component)componentInChildren).transform.position,
						Rule = ThreatRule.Explosive,
						Aggressive = true
					});
				}
			}
		}
		return Current;
	}

	private static Threat Read(EnemyParent ep)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		Enemy enemy = ep.Enemy;
		Threat threat = new Threat
		{
			Parent = ep,
			Position = (((Object)(object)enemy.CenterTransform != (Object)null) ? enemy.CenterTransform.position : ((Component)enemy).transform.position),
			Kind = "Enemy",
			State = ((object)enemy.CurrentState/*cast due to constrained. prefix*/).ToString(),
			Target = enemy.TargetPlayerAvatar
		};
		MonoBehaviour val = Brain(ep);
		if ((Object)(object)val != (Object)null)
		{
			threat.Kind = ((object)val).GetType().Name;
			if (threat.Kind.EndsWith("Controller"))
			{
				threat.Kind = threat.Kind.Substring(0, threat.Kind.Length - "Controller".Length);
			}
			TypeInfo typeInfo = Info(((object)val).GetType());
			if (typeInfo.State != null)
			{
				object value = typeInfo.State.GetValue(val);
				if (value != null)
				{
					threat.State = value.ToString();
				}
			}
			FieldInfo[] targets = typeInfo.Targets;
			for (int i = 0; i < targets.Length; i++)
			{
				object? value2 = targets[i].GetValue(val);
				PlayerAvatar val2 = (PlayerAvatar)((value2 is PlayerAvatar) ? value2 : null);
				if (val2 != null && (Object)(object)val2 != (Object)null)
				{
					threat.Target = val2;
					break;
				}
			}
		}
		threat.Rule = RuleFor(threat.Kind);
		threat.Calm = Has(threat.State, CalmWords) && !Has(threat.State, AngryWords);
		threat.Aggressive = !threat.Calm && Has(threat.State, AngryWords);
		if (threat.State.StartsWith("Roam") || threat.State.StartsWith("Idle") || threat.State.StartsWith("Investigate"))
		{
			threat.Aggressive = false;
			threat.Calm = true;
		}
		if (threat.Kind == "EnemyDuck")
		{
			threat.Aggressive = threat.State == "AttackStart" || threat.State == "Transform" || threat.State.StartsWith("Chase");
			threat.Calm = !threat.Aggressive;
		}
		if (threat.Kind == "EnemyBang")
		{
			threat.Aggressive = threat.State.Contains("Fuse") || threat.State.Contains("Move");
		}
		return threat;
	}

	private static ThreatRule RuleFor(string kind)
	{
		switch (kind)
		{
		case "EnemyHunter":
			return ThreatRule.Freeze;
		case "EnemyCeilingEye":
		case "EnemyThinMan":
			return ThreatRule.DontLook;
		case "EnemyDuck":
		case "EnemyElsa":
		case "EnemyTick":
		case "EnemyBirthdayBoy":
			return ThreatRule.Harmless;
		case "EnemyBang":
			return ThreatRule.Explosive;
		default:
			return ThreatRule.Generic;
		}
	}

	private static MonoBehaviour Brain(EnemyParent ep)
	{
		if (Brains.TryGetValue(ep, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		value = null;
		MonoBehaviour[] componentsInChildren = ((Component)ep).GetComponentsInChildren<MonoBehaviour>(true);
		foreach (MonoBehaviour val in componentsInChildren)
		{
			if (!((Object)(object)val == (Object)null))
			{
				Type type = ((object)val).GetType();
				if (type.Name.StartsWith("Enemy") && !(type == typeof(Enemy)) && !(type == typeof(EnemyParent)) && Info(type).State != null && ((Object)(object)value == (Object)null || type.Name.Length < ((object)value).GetType().Name.Length))
				{
					value = val;
				}
			}
		}
		if ((Object)(object)value == (Object)null)
		{
			componentsInChildren = ((Component)ep).GetComponentsInChildren<MonoBehaviour>(true);
			foreach (MonoBehaviour val2 in componentsInChildren)
			{
				if (!((Object)(object)val2 == (Object)null))
				{
					string name = ((object)val2).GetType().Name;
					if (name.StartsWith("Enemy") && name.EndsWith("Controller") && name != "EnemyController")
					{
						value = val2;
						break;
					}
				}
			}
		}
		Brains[ep] = value;
		return value;
	}

	private static TypeInfo Info(Type ty)
	{
		if (Cache.TryGetValue(ty, out var value))
		{
			return value;
		}
		value = new TypeInfo();
		FieldInfo field = ty.GetField("currentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field != null && field.FieldType.IsEnum)
		{
			value.State = field;
		}
		List<FieldInfo> list = new List<FieldInfo>();
		string[] targetFields = TargetFields;
		foreach (string name in targetFields)
		{
			FieldInfo field2 = ty.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field2 != null && field2.FieldType == typeof(PlayerAvatar))
			{
				list.Add(field2);
			}
		}
		value.Targets = list.ToArray();
		Cache[ty] = value;
		return value;
	}

	private static bool Has(string s, string[] words)
	{
		foreach (string value in words)
		{
			if (s.IndexOf(value, StringComparison.Ordinal) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	public static void ResetLevel()
	{
		Brains.Clear();
	}
}
