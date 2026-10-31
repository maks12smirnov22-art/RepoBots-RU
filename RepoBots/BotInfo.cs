using Photon.Pun;
using UnityEngine;

namespace RepoBots;

internal class BotInfo
{
	public PlayerAvatar Avatar;

	public int Index;

	public string Name;

	public string SteamId;

	public int Color;

	public PhotonView[] Views;

	public Vector3 Position;

	public float Yaw;

	public float Pitch;

	public Vector3 Velocity;

	public bool Moving;

	public bool Sprinting;

	public bool Crouching;

	public bool Crawling;

	public bool Grounded = true;

	public bool HasState;

	public int HeldView;

	public Vector3 Puller;

	public bool HoldingStatic;

	public GameObject Body;

	public float LastHurtTime = -100f;

	public void OnHurt(int damage)
	{
		LastHurtTime = Time.time;
	}
}
