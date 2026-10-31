using UnityEngine;

namespace RepoBots;

internal class Threat
{
	public EnemyParent Parent;

	public string Kind;

	public string State;

	public PlayerAvatar Target;

	public Vector3 Position;

	public ThreatRule Rule;

	public bool Aggressive;

	public bool Calm;
}
