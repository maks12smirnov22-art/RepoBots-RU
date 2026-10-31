using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RepoBots;

internal class BotBody : MonoBehaviour
{
	public BotInfo Bot;

	public Rigidbody Rb;

	public CapsuleCollider Col;

	public Vector3 WantVelocity;

	public bool WantSprint;

	public int Posture;

	public bool WantJump;

	public bool Grounded;

	public Collider GroundCollider;

	public bool Sprinting;

	public float Energy;

	public float EnergyMax;

	public bool Winded;

	private float _windedAt;

	private float _moveSpeed = 3f;

	private float _sprintSpeed = 5f;

	private float _crouchSpeed = 1.5f;

	private float _friction = 5f;

	private float _sprintAccel = 1f;

	private float _jumpForce = 20f;

	private float _gravity = 20f;

	private float _drain = 1f;

	private float _speedUpgrades;

	private PhysicsMaterial _matMove;

	private PhysicsMaterial _matIdle;

	private float _radius = 0.3f;

	private Vector3 _velRelNew;

	private Vector3 _velRel;

	private float _sprintLerp;

	private float _rechargeTimer;

	private float _jumpCooldown;

	private float _peakY;

	private bool _wasGrounded = true;

	private int _appliedPosture = -1;

	private readonly HashSet<Collider> _ignored = new HashSet<Collider>();

	private static readonly int GroundMask = LayerMask.GetMask(new string[6] { "Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject", "PlayerOnlyCollision" });

	private static readonly RaycastHit[] Hits = new RaycastHit[12];

	private static readonly string[] MustHit = new string[5] { "Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject" };

	private static int _layer = -1;

	public Vector3 Feet
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return ((Component)this).transform.position;
		}
	}

	public static BotBody Create(BotInfo bot, Vector3 feet)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected Obj, but got Unknown
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		PlayerController instance = PlayerController.instance;
		GameObject val = new GameObject("RepoBots Body " + bot.Name);
		val.layer = PickLayer(instance);
		val.transform.position = feet;
		BotBody botBody = val.AddComponent<BotBody>();
		botBody.Bot = bot;
		botBody.Rb = val.AddComponent<Rigidbody>();
		botBody.Col = val.AddComponent<CapsuleCollider>();
		botBody.CopyFromPlayer(instance);
		botBody._peakY = feet.y;
		return botBody;
	}

	private static int PickLayer(PlayerController pc)
	{
		if (_layer >= 0)
		{
			return _layer;
		}
		int num = (((Object)(object)pc != (Object)null && (Object)(object)pc.col != (Object)null) ? ((Component)pc.col).gameObject.layer : 0);
		int num2 = num;
		int num3 = Score(num);
		StringBuilder stringBuilder = new StringBuilder();
		string[] mustHit = MustHit;
		foreach (string text in mustHit)
		{
			int num4 = LayerMask.NameToLayer(text);
			stringBuilder.Append(text + "=" + ((num4 >= 0 && !Physics.GetIgnoreLayerCollision(num, num4)) ? "yes" : "NO") + " ");
		}
		Plugin.Log.LogInfo((object)$"Player collider layer {LayerMask.LayerToName(num)} ({num}) collides with: {stringBuilder}");
		if (num3 < MustHit.Length)
		{
			mustHit = new string[3] { "Player", "PlayerOnlyCollision", "Default" };
			for (int i = 0; i < mustHit.Length; i++)
			{
				int num5 = LayerMask.NameToLayer(mustHit[i]);
				if (num5 >= 0 && Score(num5) > num3)
				{
					num2 = num5;
					num3 = Score(num5);
				}
			}
			Plugin.Log.LogInfo((object)$"Bot bodies use layer {LayerMask.LayerToName(num2)} ({num2}) instead, hits {num3}/{MustHit.Length}");
		}
		_layer = num2;
		return num2;
		static int Score(int layer)
		{
			int num6 = 0;
			string[] mustHit2 = MustHit;
			for (int j = 0; j < mustHit2.Length; j++)
			{
				int num7 = LayerMask.NameToLayer(mustHit2[j]);
				if (num7 >= 0 && !Physics.GetIgnoreLayerCollision(layer, num7))
				{
					num6++;
				}
			}
			return num6;
		}
	}

	private void CopyFromPlayer(PlayerController pc)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		Rb.constraints = (RigidbodyConstraints)112;
		Rb.interpolation = (RigidbodyInterpolation)1;
		Rb.collisionDetectionMode = (CollisionDetectionMode)2;
		if ((Object)(object)pc != (Object)null)
		{
			Rigidbody rb = pc.rb;
			Rb.mass = rb.mass;
			Rb.drag = rb.drag;
			Rb.angularDrag = rb.angularDrag;
			Rb.useGravity = true;
			Collider col = pc.col;
			CapsuleCollider val = (CapsuleCollider)(object)((col is CapsuleCollider) ? col : null);
			if (val != null)
			{
				_radius = val.radius * Mathf.Max(((Component)val).transform.lossyScale.x, ((Component)val).transform.lossyScale.z);
			}
			_matMove = pc.PhysicMaterialMove;
			_matIdle = pc.PhysicMaterialIdle;
			_friction = pc.MoveFriction;
			_sprintAccel = pc.SprintAcceleration;
			_jumpForce = pc.JumpForce;
			_gravity = ((pc.playerOriginalCustomGravity > 0f) ? pc.playerOriginalCustomGravity : pc.CustomGravity);
			_drain = pc.EnergySprintDrain;
			string key = SemiFunc.PlayerGetSteamID(pc.playerAvatarScript);
			float num = StatsManager.instance.playerUpgradeSpeed.GetValueOrDefault(key, 0);
			float num2 = (float)StatsManager.instance.playerUpgradeStamina.GetValueOrDefault(key, 0) * 10f;
			_moveSpeed = ((pc.playerOriginalMoveSpeed > 0f) ? pc.playerOriginalMoveSpeed : pc.MoveSpeed);
			_crouchSpeed = ((pc.playerOriginalCrouchSpeed > 0f) ? pc.playerOriginalCrouchSpeed : pc.CrouchSpeed);
			float num3 = ((pc.playerOriginalSprintSpeed > 0f) ? pc.playerOriginalSprintSpeed : pc.SprintSpeed) - num;
			float num4 = pc.EnergyStart - num2;
			string steamId = Bot.SteamId;
			_speedUpgrades = StatsManager.instance.playerUpgradeSpeed.GetValueOrDefault(steamId, 0);
			_sprintSpeed = num3 + _speedUpgrades;
			EnergyMax = num4 + (float)StatsManager.instance.playerUpgradeStamina.GetValueOrDefault(steamId, 0) * 10f;
		}
		else
		{
			Rb.mass = 1f;
			EnergyMax = 40f;
		}
		Energy = EnergyMax;
		ApplyPosture(0);
	}

	private void ApplyPosture(int posture)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if (_appliedPosture != posture)
		{
			_appliedPosture = posture;
			float num = posture switch
			{
				1 => 1.15f, 
				2 => 0.62f, 
				_ => 1.7f, 
			};
			Col.radius = ((posture == 2) ? Mathf.Min(_radius, 0.26f) : _radius);
			Col.height = Mathf.Max(num, Col.radius * 2f);
			Col.center = new Vector3(0f, Col.height * 0.5f, 0f);
		}
	}

	public void IgnoreWith(GameObject root, bool ignore = true)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		Collider[] componentsInChildren = root.GetComponentsInChildren<Collider>(true);
		foreach (Collider val in componentsInChildren)
		{
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val == (Object)(object)Col))
			{
				Physics.IgnoreCollision((Collider)(object)Col, val, ignore);
				if (ignore)
				{
					_ignored.Add(val);
				}
				else
				{
					_ignored.Remove(val);
				}
			}
		}
	}

	public void Teleport(Vector3 feet)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		Rb.velocity = Vector3.zero;
		Rb.position = feet;
		((Component)this).transform.position = feet;
		_velRel = (_velRelNew = Vector3.zero);
		_peakY = feet.y;
	}

	public void SetActive(bool active)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (Rb.isKinematic != !active || ((Collider)Col).enabled != active)
		{
			Rb.isKinematic = !active;
			((Collider)Col).enabled = active;
			if (!active)
			{
				Rb.velocity = Vector3.zero;
			}
		}
	}

	private void FixedUpdate()
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0203: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_038d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bc: Unknown result type (might be due to invalid IL or missing references)
		if (Bot == null || (Object)(object)Bot.Avatar == (Object)null)
		{
			Object.Destroy((Object)(object)((Component)this).gameObject);
		}
		else
		{
			if (Rb.isKinematic)
			{
				return;
			}
			float fixedDeltaTime = Time.fixedDeltaTime;
			ApplyPosture(Posture);
			((Component)this).transform.rotation = Quaternion.Euler(0f, Bot.Yaw, 0f);
			CheckGround();
			Vector3 val = new Vector3(WantVelocity.x, 0f, WantVelocity.z);
			if (Energy <= 0.5f && !Winded)
			{
				Winded = true;
				_windedAt = Time.time;
				Plugin.Log.LogInfo((object)(Bot.Name + " is out of breath"));
			}
			else if (Winded && Energy >= EnergyMax * 0.6f)
			{
				Winded = false;
				Plugin.Log.LogInfo((object)$"{Bot.Name} got their breath back ({Time.time - _windedAt:0}s)");
			}
			bool flag = WantSprint && !Winded && Posture == 0 && Energy > 0f && val.sqrMagnitude > 0.01f;
			float num;
			if (flag)
			{
				_sprintLerp = Mathf.Clamp01(_sprintLerp + _sprintAccel * fixedDeltaTime);
				num = Mathf.Lerp(_moveSpeed, _sprintSpeed, _sprintLerp);
				float num2 = (_drain + _speedUpgrades) * _sprintLerp * fixedDeltaTime;
				Energy = Mathf.Max(0f, Energy - num2);
				_rechargeTimer = 1f;
			}
			else
			{
				_sprintLerp = 0f;
				num = ((Posture > 0) ? _crouchSpeed : _moveSpeed);
			}
			Sprinting = flag;
			Vector3 val2 = Vector3.zero;
			if (val.sqrMagnitude > 0.0001f)
			{
				float num3 = Mathf.Clamp01(val.magnitude / Mathf.Max(num, 0.1f));
				val2 = val.normalized * num3;
			}
			Vector3 val3 = val2 * num;
			_velRelNew = Vector3.Lerp(_velRelNew, val3, _friction * fixedDeltaTime);
			Vector3 velocity = Rb.velocity;
			if (_velRelNew.magnitude > 0.1f)
			{
				if ((Object)(object)_matMove != (Object)null)
				{
					((Collider)Col).sharedMaterial = _matMove;
				}
				_velRel = Vector3.Lerp(_velRel, _velRelNew, _friction * fixedDeltaTime);
				_velRel.y = velocity.y;
				Rb.AddForce(_velRel - velocity, (ForceMode)1);
			}
			else
			{
				if ((Object)(object)_matIdle != (Object)null)
				{
					((Collider)Col).sharedMaterial = _matIdle;
				}
				_velRel = Vector3.zero;
			}
			if (!Grounded)
			{
				Rb.AddForce(new Vector3(0f, (0f - _gravity) * fixedDeltaTime, 0f), (ForceMode)1);
			}
			if (_jumpCooldown > 0f)
			{
				_jumpCooldown -= fixedDeltaTime;
			}
			if (WantJump && Grounded && _jumpCooldown <= 0f && Posture == 0)
			{
				Rb.AddForce(Vector3.up * _jumpForce - new Vector3(0f, Rb.velocity.y, 0f), (ForceMode)1);
				_jumpCooldown = 0.4f;
				Grounded = false;
			}
			WantJump = false;
			StepUp(((Component)this).transform.InverseTransformDirection(val2));
			if (!flag)
			{
				if (_rechargeTimer > 0f)
				{
					_rechargeTimer -= fixedDeltaTime;
				}
				else if (Energy < EnergyMax)
				{
					Energy = Mathf.Min(EnergyMax, Energy + 2f * fixedDeltaTime);
				}
			}
		}
	}

	private void CheckGround()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		int num = Physics.SphereCastNonAlloc(((Component)this).transform.position + Vector3.up * (Col.radius + 0.05f), Col.radius * 0.9f, Vector3.down, Hits, 0.2f, GroundMask, (QueryTriggerInteraction)1);
		bool flag = false;
		GroundCollider = null;
		for (int i = 0; i < num; i++)
		{
			if (!((Object)(object)Hits[i].collider == (Object)(object)Col) && !_ignored.Contains(Hits[i].collider) && Hits[i].normal.y > 0.5f)
			{
				flag = true;
				GroundCollider = Hits[i].collider;
				BotPits.LearnFloor(GroundCollider);
				break;
			}
		}
		if (!flag)
		{
			_peakY = Mathf.Max(_peakY, ((Component)this).transform.position.y);
		}
		else
		{
			if (!_wasGrounded)
			{
				float num2 = _peakY - ((Component)this).transform.position.y;
				if (num2 >= 8f)
				{
					FallDamage(num2);
				}
			}
			_peakY = ((Component)this).transform.position.y;
		}
		_wasGrounded = flag;
		Grounded = flag;
	}

	private void FallDamage(float fall)
	{
		PlayerAvatar avatar = Bot.Avatar;
		int damage = 5;
		if (fall > 21f)
		{
			damage = 100;
		}
		else if (fall > 17f)
		{
			damage = 50;
		}
		else if (fall > 13f)
		{
			damage = 25;
		}
		else if (fall > 9f)
		{
			damage = 15;
		}
		Plugin.Log.LogInfo((object)$"{Bot.Name} fell {fall:0.0}m");
		if ((Object)(object)avatar.tumble != (Object)null)
		{
			avatar.tumble.TumbleRequest(true, false);
		}
		BotHealth.Hurt(avatar.playerHealth, damage, savingGrace: true, -1, hurtByHeal: false);
	}

	private void StepUp(Vector3 input)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		if (!Grounded || input.sqrMagnitude < 0.01f)
		{
			return;
		}
		Vector3 val = ((Component)this).transform.TransformDirection(input);
		Vector3 normalized = val.normalized;
		Vector3 position = ((Component)this).transform.position;
		float num = Col.radius + 0.25f;
		RaycastHit val2 = default;
		if (Physics.Raycast(position + Vector3.up * 0.08f, normalized, out val2, num, GroundMask, (QueryTriggerInteraction)1) && !_ignored.Contains(val2.collider) && val2.normal.y < 0.5f && !Physics.Raycast(position + Vector3.up * 0.42f, normalized, num + 0.1f, GroundMask, (QueryTriggerInteraction)1))
		{
			Vector3 velocity = Rb.velocity;
			if (velocity.y < 2.5f)
			{
				Rb.velocity = new Vector3(velocity.x, 2.5f, velocity.z);
			}
		}
	}
}
