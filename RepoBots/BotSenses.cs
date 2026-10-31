using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal static class BotSenses
{
	public static readonly List<HeardSound> Recent = new List<HeardSound>();

	private static readonly Dictionary<Sound, float> LoopThrottle = new Dictionary<Sound, float>();

	public static bool Listening
	{
		get
		{
			if (PhotonNetwork.IsMasterClient)
			{
				return BotRegistry.Bots.Count > 0;
			}
			return false;
		}
	}

	public static void Report(Vector3 pos, float range)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		if (range <= 0.5f)
		{
			return;
		}
		EnemyParent val = null;
		float num = 3f;
		foreach (Threat item in BotThreats.All())
		{
			float num2 = Vector3.Distance(item.Position, pos);
			if (num2 < num)
			{
				num = num2;
				val = item.Parent;
			}
		}
		if (!((Object)(object)val == (Object)null))
		{
			float time = Time.time;
			Recent.Add(new HeardSound
			{
				Position = pos,
				Range = range,
				Time = time,
				Enemy = val
			});
			int i;
			for (i = 0; i < Recent.Count && time - Recent[i].Time > 2f; i++)
			{
			}
			if (i > 0)
			{
				Recent.RemoveRange(0, i);
			}
		}
	}

	public static void ReportLoop(Sound s)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)s.Source == (Object)null) && s.Source.isPlaying && !(s.Source.volume < 0.05f))
		{
			float time = Time.time;
			if (!LoopThrottle.TryGetValue(s, out var value) || !(time < value))
			{
				LoopThrottle[s] = time + 0.5f;
				Report(((Component)s.Source).transform.position, s.Source.maxDistance);
			}
		}
	}

	public static void ResetLevel()
	{
		Recent.Clear();
		LoopThrottle.Clear();
	}
}
