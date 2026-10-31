using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using UnityEngine.AI;

namespace RepoBots;

internal class BotBrain
{
	private enum OpenStep
	{
		Approach,
		Pull,
		Peek
	}

	internal class Box
	{
		public PhysGrabHinge Hinge;

		public PhysGrabObject Door;

		public string Kind;

		public Bounds Furniture;
	}

	private enum Job
	{
		Follow,
		Flee,
		Freeze,
		Hide,
		Revive,
		Haul,
		Cart,
		Activate,
		Explore,
		Truck,
		Assist,
		Escort,
		Guard,
		Order,
		Heal,
		Open,
		HangOut,
		Gnome,
		Use
	}

	private enum Carry
	{
		Approach,
		Hold,
		Deliver
	}

	private class Percept
	{
		public Vector3 Pos;

		public float SeenAt = -100f;

		public float HeardAt = -100f;
	}

	private enum CartGoal
	{
		Extraction,
		LootZone,
		OutOfExtraction
	}

	private class PersonMemory
	{
		public Vector3 Pos;

		public float Time = -100f;

		public bool SeenNow;
	}

	internal enum OrderKind
	{
		None,
		Follow,
		Come,
		Hold,
		GoTo,
		Truck
	}

	private class Pending
	{
		public float At;

		public string Text;

		public int Prio;

		public bool CanReply;

		public Action After;
	}

	private class Want
	{
		public string Item;

		public string Owner;
	}

	public static bool MuteReplies;

	private static readonly Regex BoxName = new Regex("chest|wardrobe|bookshel|cabinet|fridge|locker|safe|garbage bin|bin\\b|bajamaja|toilet", RegexOptions.IgnoreCase);

	private static readonly Regex NotBox = new Regex("door|stall|vent|hatch|hinges|garage car|helicopter|mount|painting|shelves \\(", RegexOptions.IgnoreCase);

	internal static readonly List<Box> Boxes = new List<Box>();

	private static readonly Dictionary<PhysGrabHinge, Box> BoxOf = new Dictionary<PhysGrabHinge, Box>();

	private static readonly Dictionary<Box, BotBrain> OpenClaims = new Dictionary<Box, BotBrain>();

	private static float _boxesAt;

	private static bool _boxesFound;

	private readonly HashSet<Box> _boxSeen = new HashSet<Box>();

	private readonly HashSet<Box> _boxDone = new HashSet<Box>();

	private readonly HashSet<Box> _boxSkip = new HashSet<Box>();

	private float _boxLookAt;

	private float _openCheckAt;

	private Box _openBox;

	private OpenStep _openStep;

	private bool _openAsked;

	private Vector3? _openSpot;

	private float _openSpotAt;

	private int _openSpotTries;

	private Vector3 _openLocal;

	private float _openSign = 1f;

	private float _openBest;

	private float _openProgressAt;

	private int _openFlips;

	private int _openRegrabs;

	private float _peekUntil;

	private int _peekBefore;

	private int _openSweep;

	private Vector3 _openSweepAt;

	private const float WalkSpeed = 3.2f;

	private const float SprintSpeed = 5.6f;

	private const float CarrySpeed = 2.2f;

	private const float CrouchSpeed = 1.6f;

	private const float CrawlSpeed = 0.9f;

	private const float Accel = 18f;

	private const float EyeHeight = 1.5f;

	private const float ReachDistance = 3.5f;

	private const float Radius = 0.32f;

	private const float BodyHeight = 1.7f;

	private const float StepHeight = 0.4f;

	private const float Skin = 0.03f;

	private const int Stand = 0;

	private const int Crouch = 1;

	private const int Crawl = 2;

	private static readonly HashSet<string> LooksUnder = new HashSet<string> { "EnemyRobe", "EnemyRunner", "EnemySlowWalker", "EnemyHead" };

	private static readonly HashSet<string> Deadly = new HashSet<string> { "EnemyRobe", "EnemyRunner", "EnemyHead", "EnemySlowWalker", "EnemyBeamer", "EnemyTumbler", "EnemyFloater" };

	private static readonly int GroundMask = LayerMask.GetMask(new string[3] { "Default", "NavmeshOnly", "PlayerOnlyCollision" });

	private static readonly int MoveMask = LayerMask.GetMask(new string[5] { "Default", "PhysGrabObject", "PhysGrabObjectCart", "PhysGrabObjectHinge", "StaticGrabObject" });

	private static readonly RaycastHit[] Hits = new RaycastHit[24];

	private static readonly Dictionary<PhysGrabObject, BotBrain> Claims = new Dictionary<PhysGrabObject, BotBrain>();

	private static readonly Dictionary<PhysGrabObject, float> Blacklist = new Dictionary<PhysGrabObject, float>();

	private static BotBrain _activator;

	private readonly BotInfo _bot;

	private readonly NavMeshPath _path = new NavMeshPath();

	private readonly List<Vector3> _corners = new List<Vector3>();

	private int _corner;

	private float _repathAt;

	private Vector3 _pathGoal;

	private Job _job;

	private Job _jobBeforeFreeze;

	private float _thinkAt;

	private float _jobStarted;

	private PlayerAvatar _leader;

	private bool _walking;

	private bool _sprinting;

	private readonly float _followDist;

	private readonly float _sideBias;

	private Threat _danger;

	private Vector3 _fleeTo;

	private float _fleeUntil;

	private bool _fleeRun;

	private float _freezeUntil;

	private bool _quiet;

	private readonly List<Vector3> _dontLook = new List<Vector3>();

	private Vector3 _hideSpot;

	private bool _hideUnder;

	private float _hideStarted;

	private float _hideCalmSince = -1f;

	private int _posture;

	private readonly Dictionary<EnemyParent, Percept> _known = new Dictionary<EnemyParent, Percept>();

	private readonly List<Threat> _knownList = new List<Threat>();

	private readonly HashSet<EnemyParent> _seenNow = new HashSet<EnemyParent>();

	private int _knownFrame = -1;

	private float _heardUpTo;

	private Vector3 _attention;

	private float _attentionNext;

	private float _listenUntil;

	private float _attentionUntil;

	private LevelPoint _explore;

	private float _exploreStarted;

	private float _scanAt;

	private float _scanYaw;

	private bool _wasTumbling;

	private float _tumbleStarted;

	private bool _tumbleOnPurpose;

	private float _nextDiveAt;

	private PhysGrabObject _target;

	private PlayerAvatar _reviveOf;

	private PhysGrabCart _cart;

	private PhysGrabCart _pushing;

	private Carry _carry;

	private float _carryProblemSince = -1f;

	private float _deliverStarted;

	private float _grabbedAt;

	private float _itemBlocked;

	private float _crampedCheckAt;

	private bool _itemCramped;

	private int _wedgedCount;

	private Vector3? _approachFrom;

	private ExtractionPoint _activateTarget;

	private Vector3 _progressPos;

	private float _progressAt;

	private float _blockedSince = -1f;

	private Vector3 _sidestep;

	private float _sidestepUntil;

	private bool _wasDisabled;

	private BotBody _body;

	private PhysGrabObject _bodyIgnoresHeld;

	private float _glanceAt;

	private float _glanceYaw;

	private float _seeAt;

	private float _tagAlongUntil;

	private float _tagAlongRollAt;

	private static readonly Dictionary<PhysGrabObject, int> Fails = new Dictionary<PhysGrabObject, int>();

	private static readonly Dictionary<PhysGrabObject, float> HumanHeldAt = new Dictionary<PhysGrabObject, float>();

	private ExtractionPoint _reviveEP;

	private float _headCartCheckAt;

	private static readonly Collider[] Overlaps = new Collider[16];

	private static readonly Collider[] Nearby = new Collider[64];

	private const float SightRange = 22f;

	private const float SightHalfAngle = 60f;

	private const float FeelRange = 3f;

	private const float RememberSeen = 5f;

	private const float RememberHeard = 4f;

	private float _escapeLogAt;

	private float _liftUntil;

	private float _snagSince = -1f;

	private float _snagGraceUntil;

	private PhysGrabObject _holdSizeOf;

	private float _holdHalfH;

	private Vector3 _avoidDir;

	private float _avoidUntil;

	private bool _pathFailed;

	private bool _pathPartial;

	private float _yawVel;

	private float _pitchVel;

	private float _tapPhase;

	private bool _jobWantsToMove;

	private float _cartApproachAt = -1f;

	private float _grabCrouchUntil;

	private float _carryLogAt;

	private float _helpAskedAt;

	private float _haulCheckAt;

	private Vector3 _haulCheckPos;

	private bool _haulMoved = true;

	private static BotBrain _driver;

	private static BotBrain _escort;

	private static readonly Dictionary<PhysGrabCart, float> HumanTouchedAt = new Dictionary<PhysGrabCart, float>();

	private CartGoal _cartGoal;

	private Vector3 _cartDest;

	private int _cartFan = -1;

	private float _cartDecideAt;

	private float _cartParkedAt = -100f;

	private bool _cartForPerson;

	private PhysGrabCart _escortCart;

	private bool _escortHaul;

	private float _cartStillSince = -1f;

	private float _escortLostAt = -1f;

	private bool _guarding;

	private float _cartThreatAt = -100f;

	private Vector3 _cartThreatPos;

	private Vector3? _guardSpot;

	private Vector3? _detour;

	private float _routeWaitSince = -1f;

	private bool _routeLogged;

	private static readonly int CartWallMask = LayerMask.GetMask(new string[2] { "Default", "StaticGrabObject" });

	private PhysGrabCart _cartMeasured;

	private Vector3 _cartHalf;

	private Vector3 _cartCenterLocal;

	private float _cartBackUntil;

	private Vector3 _cartBackTurn;

	private float _cartBlockedSince = -1f;

	private float _cartModeBadSince = -1f;

	private float _cartAimBadSince = -1f;

	private bool _cartAimLogged;

	private static readonly int HingeMask = LayerMask.GetMask(new string[1] { "PhysGrabObjectHinge" });

	private static readonly RaycastHit[] DoorHits = new RaycastHit[8];

	private Box _door;

	private bool _doorHolding;

	private Vector3 _doorLocal;

	private float _doorSign = 1f;

	private float _doorStarted;

	private float _doorBest;

	private float _doorProgressAt;

	private int _doorFlips;

	private float _doorCheckAt;

	private readonly Dictionary<PhysGrabHinge, float> _doorRest = new Dictionary<PhysGrabHinge, float>();

	private PhysGrabHinge _backDoor;

	private float _backUntil;

	private float _backPushAt;

	private float _backClearSince = -1f;

	private static readonly Dictionary<PhysGrabHinge, BotBrain> DoorClaims = new Dictionary<PhysGrabHinge, BotBrain>();

	private float _doorWaitUntil;

	private Vector3 _doorWaitFrom;

	private EnemyParent _gnome;

	private float _gnomeSlamAt;

	private bool _gnomeUp;

	private float _gnomeCheckAt;

	private Vector3? _hangSpot;

	private Vector3 _hangAnchor;

	private bool _hangAnchored;

	private float _hangMoveAt;

	private bool _hangMoving;

	private float _hangLookAt;

	private Vector3? _hangLook;

	private bool _hangInShop;

	private ItemAttributes _browsing;

	private PlayerAvatar _hangWho;

	private static readonly Dictionary<PlayerAvatar, PlayerHealthGrab> NeckOf = new Dictionary<PlayerAvatar, PlayerHealthGrab>();

	private static float _necksAt;

	private readonly Dictionary<PlayerAvatar, float> _knowHurt = new Dictionary<PlayerAvatar, float>();

	private PlayerAvatar _healing;

	private int _healStartHp;

	private int _healTargetHp;

	private float _healProgressAt;

	private bool _healAsked;

	private float _healCheckAt;

	private Vector3? _sneakTo;

	private bool _sneakUnder;

	private Vector3 _hunterLastPos;

	private float _hunterLastAt;

	private Vector3 _hunterVel;

	private float _frozeAt;

	private Vector3 _zoneCenter;

	private float _zoneRadius;

	private float _zoneUntil;

	private float _escapeUntil;

	private Vector3 _escapeTo;

	private static readonly int RoomLayer = LayerMask.GetMask(new string[1] { "RoomVolume" });

	private static readonly Collider[] MapHits = new Collider[8];

	private readonly Dictionary<RoomVolume, float> _rooms = new Dictionary<RoomVolume, float>();

	private float _mapLearnAt;

	private Vector3 _routeCheckedGoal = new Vector3(9999f, 9999f, 9999f);

	private bool _mapOut;

	private float _mapUntil;

	private float _mapNextAllowed;

	private float _mapIdleAt = -1f;

	private Action _mapAfter;

	private const float PeopleSightRange = 30f;

	private const float ShareRange = 15f;

	private const float ForgetPerson = 90f;

	private readonly Dictionary<PlayerAvatar, PersonMemory> _people = new Dictionary<PlayerAvatar, PersonMemory>();

	private readonly HashSet<ValuableObject> Seen = new HashSet<ValuableObject>();

	private readonly Dictionary<PhysGrabObject, Vector3> _thingPos = new Dictionary<PhysGrabObject, Vector3>();

	private readonly List<PhysGrabCart> Carts = new List<PhysGrabCart>();

	private readonly HashSet<LevelPoint> Visited = new HashSet<LevelPoint>();

	private float _memoryAt;

	private float _shareAt;

	private static readonly List<PhysGrabCart> AllCarts = new List<PhysGrabCart>();

	private static float _allCartsAt;

	private OrderKind _order;

	private PlayerAvatar _orderBy;

	private Vector3 _orderPos;

	private float _orderUntil;

	private bool _orderArrived;

	private PlayerAvatar _bringTo;

	private float _orderLostSince = -1f;

	private bool _orderMoving;

	private int _orderStuck;

	private static readonly string[] Okays = new string[6] { "On it.", "Okay.", "Got it.", "Sure thing.", "You got it.", "Will do." };

	private float _pitLogAt = -99f;

	private float _pitBlockedSince = -1f;

	private PhysGrabObject _setDown;

	private float _setDownStarted;

	private float _setDownRestingSince = -1f;

	private string _setDownWhy;

	private bool _dropNow;

	private bool _savingStamina;

	private ItemAttributes _shopItem;

	private bool _shopPutBack;

	private float _shopCheckAt;

	private float _shopNothingLogAt;

	private static Object _shopVisit;

	private static int _shopVisitNo;

	private static float _shopSince;

	private static readonly List<ItemAttributes> ShopStock = new List<ItemAttributes>();

	private static float _stockAt;

	private static readonly Dictionary<ItemAttributes, BotBrain> ShopBought = new Dictionary<ItemAttributes, BotBrain>();

	private static readonly Dictionary<ItemAttributes, Vector3> ShelfSpot = new Dictionary<ItemAttributes, Vector3>();

	private static readonly List<Vector3> BadShelves = new List<Vector3>();

	private static readonly HashSet<PlayerAvatar> WentShopping = new HashSet<PlayerAvatar>();

	private static bool _budgetSaid;

	private static bool _doneSaid;

	private static bool _buttonSaid;

	private static float _overSaidAt;

	private static int _stopVisit = -1;

	private static int _wantVisit = -1;

	private static string _shopWant;

	private BotPersona _persona;

	private List<string> _voices;

	private SlangStyle _slang;

	private float _talkAt;

	private bool _greeted;

	private float _greetAt = -1f;

	private float _banterAt;

	private bool _deadLineSaid;

	private float _diedAt = -1f;

	private PhysGrabObject _heldTalk;

	private string _heldName;

	private float _heldValue;

	private readonly List<Pending> _pending = new List<Pending>();

	private static readonly Dictionary<string, float> TeamSaid = new Dictionary<string, float>();

	private static float _anyTalkAt;

	internal static readonly List<string> Conversation = new List<string>();

	private const int Chatter = 0;

	private const int Info = 1;

	private const int Urgent = 2;

	private const int Reply = 3;

	private string _typing;

	private string _typingPlain;

	private string _typingFix;

	private int _typingPrio;

	private float _typingDone;

	private bool _typingReply;

	private string _typingTopic;

	private Action _typingAfter;

	private string _deferred;

	private int _deferredPrio;

	private float _deferredAt;

	private bool _deferredReply;

	private string _deferredTopic;

	private Action _deferredAfter;

	private float _cps = -1f;

	private static readonly Dictionary<PlayerAvatar, bool> WasDead = new Dictionary<PlayerAvatar, bool>();

	private static readonly Dictionary<PlayerAvatar, int> LastHp = new Dictionary<PlayerAvatar, int>();

	private static int _extractionsSeen = -1;

	private static int _watchFrame = -1;

	private static readonly int RoomMask = LayerMask.GetMask(new string[1] { "RoomVolume" });

	private static readonly Collider[] RoomHits = new Collider[8];

	private static readonly Dictionary<string, string> RoomNames = new Dictionary<string, string>();

	private static readonly HashSet<string> Generic = new HashSet<string>
	{
		"n", "s", "e", "w", "start", "room", "module", "level", "normal", "passage",
		"dead end", "deadend", "extraction", "truck"
	};

	private static readonly Dictionary<PhysGrabObject, List<BotBrain>> Helpers = new Dictionary<PhysGrabObject, List<BotBrain>>();

	private PhysGrabObject _assist;

	private bool _slackerRun;

	private bool _screenMode;

	private float _screenStarted;

	private float _screenGrabbedAt;

	private float _screenNudgeAt;

	private bool _finalSeen;

	private static readonly List<Want> Wants = new List<Want>();

	private ItemAttributes _useItem;

	private float _useStarted;

	private float _usePressedAt;

	private float _useCheckAt;

	private string _useName;

	private string _useKey;

	private float BoxRange => Mathf.Lerp(8f, 16f, P.Greed);

	private bool NoWayThere
	{
		get
		{
			//IL_0076: Unknown result type (might be due to invalid IL or missing references)
			//IL_007c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_0051: Unknown result type (might be due to invalid IL or missing references)
			//IL_0056: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			if (!_pathFailed)
			{
				if (_pathPartial && _corner >= _corners.Count - 1 && _corners.Count > 0)
				{
					Vector3 val = Flat(_corners[_corners.Count - 1] - _bot.Position);
					if (val.magnitude < 1.2f)
					{
						goto IL_006f;
					}
				}
				return false;
			}
			goto IL_006f;
			IL_006f:
			return !StraightOk(_bot.Position, _pathGoal);
		}
	}

	private bool OpeningDoor => _door != null;

	private bool HangingOutHere
	{
		get
		{
			if (!SemiFunc.RunIsLobby())
			{
				if (SemiFunc.RunIsShop())
				{
					return !TruckTime();
				}
				return false;
			}
			return true;
		}
	}

	private float Generosity => Mathf.Clamp01(0.65f * P.Helpful + 0.35f * P.Brave);

	private int HealFloor => Mathf.RoundToInt(Mathf.Lerp(70f, 12f, Generosity));

	private int HealBudget => Mathf.RoundToInt(Mathf.Lerp(10f, 90f, Generosity) / 10f) * 10;

	private int MyHealth
	{
		get
		{
			if (!((Object)(object)_bot.Avatar != (Object)null) || !((Object)(object)_bot.Avatar.playerHealth != (Object)null))
			{
				return 0;
			}
			return _bot.Avatar.playerHealth.health;
		}
	}

	private bool CanSpare => MyHealth - 10 >= HealFloor;

	internal Collider BodyCollider
	{
		get
		{
			if (!((Object)(object)_body != (Object)null))
			{
				return null;
			}
			return (Collider)(object)_body.Col;
		}
	}

	private int HealthNow
	{
		get
		{
			if (!((Object)(object)_bot.Avatar != (Object)null) || !((Object)(object)_bot.Avatar.playerHealth != (Object)null))
			{
				return 100;
			}
			return _bot.Avatar.playerHealth.health;
		}
	}

	private float Nerve
	{
		get
		{
			if (HealthNow >= 30)
			{
				if (HealthNow >= 50)
				{
					return P.Brave;
				}
				return Mathf.Min(P.Brave, 0.4f);
			}
			return Mathf.Min(P.Brave, 0.05f);
		}
	}

	private float MemorySpan => Mathf.Lerp(150f, 420f, P.Careful);

	public bool Alive
	{
		get
		{
			if ((Object)(object)_bot.Avatar != (Object)null)
			{
				return !_bot.Avatar.isDisabled;
			}
			return false;
		}
	}

	public Vector3 Position
	{
		get
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			return _bot.Position;
		}
	}

	public string Name => _bot.Name;

	public PlayerAvatar Self => _bot.Avatar;

	public OrderKind CurrentOrder => _order;

	public PlayerAvatar OrderedBy => _orderBy;

	private static bool ShopStopped => _stopVisit == _shopVisitNo;

	private static string ShopWant
	{
		get
		{
			if (_wantVisit != _shopVisitNo)
			{
				return null;
			}
			return _shopWant;
		}
	}

	internal static bool AnyBotPurchases => ShopBought.Count > 0;

	private static int Currency => SemiFunc.StatGetRunCurrency();

	private static int ZoneCost => SemiFunc.ShopGetTotalCost();

	private BotPersona P => _persona ?? (_persona = BotPersona.For(_bot.SteamId));

	private List<string> Voices => _voices ?? (_voices = MakeVoices());

	private SlangStyle Slang => _slang ?? (_slang = SlangStyle.For(_bot.SteamId, P));

	private bool Typing => _typing != null;

	private float CharsPerSecond
	{
		get
		{
			if (_cps < 0f)
			{
				uint num = 2166136261u;
				string text = _bot.Name + _bot.SteamId;
				foreach (char c in text)
				{
					num = (num ^ c) * 16777619;
				}
				num ^= num >> 15;
				num *= 739982445;
				num ^= num >> 12;
				float num2 = 3.5f + (float)(num % 1000) / 1000f * 7.5f;
				_cps = num2 * Mathf.Lerp(0.85f, 1.2f, P.Chatty) * Mathf.Lerp(1.1f, 0.9f, P.Careful);
				Plugin.Log.LogInfo((object)$"{_bot.Name} types {_cps:0.0} letters a second");
			}
			return _cps;
		}
	}

	public bool OnScreen => _screenMode;

	private float HeavyLimit => Plugin.MaxCarryMass.Value;

	public void SayAck(string text)
	{
		Say(text, 3);
	}

	public void AnswerLater(float delay, BotChat.Ask ask, PlayerAvatar asker, string subject)
	{
		HeardFrom(asker);
		string text = AnswerText(ask, asker, subject);
		if (text != null)
		{
			if (delay <= 0.05f)
			{
				Say(text, 3);
			}
			else
			{
				SayLater(delay, text, 3);
			}
		}
	}

	internal string AIBriefing(PlayerAvatar asker)
	{
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("You are " + _bot.Name + ", a crew member in the co-op horror game R.E.P.O.: little robots sent into creepy places to grab valuables and haul them to an extraction point while avoiding monsters. You're one of the crew, talking out loud with a text-to-speech voice to your teammate " + asker.playerName + ".");
		stringBuilder.AppendLine("Your personality: " + P.Describe() + ". Talk like that.");
		stringBuilder.AppendLine("Reply with ONE short spoken line, under 15 words. Casual, a bit funny, no emojis, no stage directions, no quotes, spell numbers as words.");
		stringBuilder.AppendLine("Only use the facts below. If you don't know something, say so; never make up where things are.");
		stringBuilder.AppendLine("If they're asking you to do something, end your reply with one tag: [FOLLOW] (follow them), [COME] (go to them), [WAIT] (stay where you are), [FREE] (go back to looting on your own), [TRUCK] (go to the truck), [GRAB] (pick up what they're looking at), [BRING] (bring them what they're looking at), [CART] (take charge of the cart). Otherwise no tag.");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("What you know right now:");
		stringBuilder.AppendLine("- You are: " + DoingNow() + ".");
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar != (Object)null && (Object)(object)avatar.playerHealth != (Object)null)
		{
			stringBuilder.AppendLine($"- Your health: {avatar.playerHealth.health} of {avatar.playerHealth.maxHealth}.");
		}
		stringBuilder.AppendLine($"- You remember {_rooms.Count} rooms of this level.");
		stringBuilder.AppendLine("- This save so far: " + HistoryText() + ". Level " + Words((((Object)(object)RunManager.instance != (Object)null) ? RunManager.instance.levelsCompleted : 0) + 1) + " of the run.");
		string text = RoomName(_bot.Position);
		if (text != null)
		{
			stringBuilder.AppendLine("- You're in " + text + ".");
		}
		if ((Object)(object)asker != (Object)null && !asker.isDisabled)
		{
			stringBuilder.AppendLine("- " + asker.playerName + " is " + Where(((Component)asker).transform.position, forListener: false) + " (from you).");
		}
		List<Threat> list = KnownThreats();
		if (list.Count == 0)
		{
			stringBuilder.AppendLine("- No monsters seen or heard lately.");
		}
		foreach (Threat item in list)
		{
			stringBuilder.AppendLine("- A " + MonsterName(item.Kind) + " " + Where(item.Position, forListener: true, asker) + " (from " + asker.playerName + "'s view)" + (item.Aggressive ? ", and it's angry" : "") + ".");
		}
		string text2 = AnswerText(BotChat.Ask.Loot, asker, null);
		if (text2 != null)
		{
			stringBuilder.AppendLine("- Loot: " + text2);
		}
		string text3 = AnswerText(BotChat.Ask.Money, asker, null);
		if (!string.IsNullOrEmpty(text3))
		{
			stringBuilder.AppendLine("- Money: " + text3);
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && (Object)(object)player != (Object)(object)avatar && player.isDisabled)
			{
				stringBuilder.AppendLine("- " + player.playerName + " is dead.");
			}
		}
		if (Conversation.Count > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("Recent chat:");
			foreach (string item2 in Conversation)
			{
				stringBuilder.AppendLine(item2);
			}
		}
		return stringBuilder.ToString();
	}

	internal void FromAI(string text, PlayerAvatar from)
	{
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		string text2 = BotAI.TakeAction(ref text);
		text = text.Trim('"', ' ');
		if (text.Length > 140)
		{
			text = text.Substring(0, 140);
		}
		text = Regex.Replace(text, "\\d+", (Match m) => (!int.TryParse(m.Value, out var result)) ? m.Value : Words(result));
		Plugin.Log.LogInfo((object)(_bot.Name + " (Claude) -> " + text + ((text2 != null) ? (" [" + text2 + "]") : "")));
		MuteReplies = true;
		try
		{
			if (text2 != null)
			{
				switch (text2.Length)
				{
				default:
					goto end_IL_00a7;
				case 4:
					switch (text2[2])
					{
					default:
						goto end_IL_00a7;
					case 'M':
						if (text2 == "COME")
						{
							OrderCome(from);
						}
						goto end_IL_00a7;
					case 'I':
						if (text2 == "WAIT")
						{
							OrderHold(from);
						}
						goto end_IL_00a7;
					case 'E':
						if (text2 == "FREE")
						{
							ClearOrder(say: false);
						}
						goto end_IL_00a7;
					case 'R':
						if (text2 == "CART")
						{
							PhysGrabCart val = BotCommands.NearestCart(_bot.Position);
							if ((Object)(object)val != (Object)null)
							{
								OrderCart(from, val, toPerson: false);
							}
						}
						goto end_IL_00a7;
					case 'A':
						if (text2 == "GRAB")
						{
							break;
						}
						goto end_IL_00a7;
					}
					break;
				case 5:
					switch (text2[0])
					{
					default:
						goto end_IL_00a7;
					case 'T':
						if (text2 == "TRUCK")
						{
							OrderTruck(from);
						}
						goto end_IL_00a7;
					case 'B':
						if (text2 == "BRING")
						{
							break;
						}
						goto end_IL_00a7;
					}
					break;
				case 6:
					if (text2 == "FOLLOW" && _order != OrderKind.Follow)
					{
						OrderFollow(from);
					}
					goto end_IL_00a7;
				}
				PhysGrabObject val2 = BotCommands.GazeObject(from);
				if ((Object)(object)val2 != (Object)null)
				{
					OrderGrab(from, val2, text2 == "BRING");
				}
			}
			end_IL_00a7:;
		}
		finally
		{
			MuteReplies = false;
		}
		Say(text, 3);
	}

	private static void ResetBoxStatics()
	{
		Boxes.Clear();
		BoxOf.Clear();
		OpenClaims.Clear();
		_boxesAt = 0f;
		_boxesFound = false;
	}

	internal static void FindBoxes()
	{
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		if (_boxesFound || Time.time < _boxesAt || !SemiFunc.RunIsLevel())
		{
			return;
		}
		_boxesAt = Time.time + 5f;
		if ((Object)(object)LevelGenerator.Instance == (Object)null || !LevelGenerator.Instance.Generated)
		{
			return;
		}
		PhysGrabHinge[] array = Object.FindObjectsOfType<PhysGrabHinge>();
		foreach (PhysGrabHinge val in array)
		{
			if ((Object)(object)val == (Object)null || BoxOf.ContainsKey(val) || (Object)(object)val.physGrabObject == (Object)null)
			{
				continue;
			}
			Transform parent = ((Component)val).transform.parent;
			if ((Object)(object)parent == (Object)null)
			{
				continue;
			}
			string name = ((Object)parent).name;
			if (NotBox.IsMatch(name) || !BoxName.IsMatch(name))
			{
				continue;
			}
			Box box = new Box
			{
				Hinge = val,
				Door = val.physGrabObject,
				Kind = KindOf(name)
			};
			bool flag = false;
			Renderer[] componentsInChildren = ((Component)parent).GetComponentsInChildren<Renderer>();
			foreach (Renderer val2 in componentsInChildren)
			{
				if (!((Object)(object)val2 == (Object)null) && (val2 is MeshRenderer || val2 is SkinnedMeshRenderer) && val2.enabled)
				{
					if (!flag)
					{
						box.Furniture = val2.bounds;
					}
					else
					{
						box.Furniture.Encapsulate(val2.bounds);
					}
					flag = true;
				}
			}
			if (!flag)
			{
				box.Furniture = new Bounds(val.physGrabObject.centerPoint, Vector3.one);
			}
			Boxes.Add(box);
			BoxOf[val] = box;
		}
		_boxesFound = true;
		Plugin.Log.LogInfo((object)$"Found {Boxes.Count} things that open (chests, cabinets, wardrobes...)");
	}

	private static string KindOf(string name)
	{
		string text = name.ToLowerInvariant();
		if (text.Contains("chest"))
		{
			return "chest";
		}
		if (text.Contains("wardrobe"))
		{
			return "wardrobe";
		}
		if (text.Contains("bookshel"))
		{
			return "bookshelf";
		}
		if (text.Contains("fridge"))
		{
			return "fridge";
		}
		if (text.Contains("locker"))
		{
			return "locker";
		}
		if (text.Contains("safe"))
		{
			return "safe";
		}
		if (text.Contains("cabinet"))
		{
			return "cabinet";
		}
		if (text.Contains("garbage") || Regex.IsMatch(text, "(^| )bin( |$)"))
		{
			return "trash can";
		}
		if (text.Contains("bajamaja"))
		{
			return "porta potty";
		}
		if (text.Contains("toilet"))
		{
			return "toilet";
		}
		return "cabinet";
	}

	private static bool BoxGone(Box b)
	{
		if (b != null && !((Object)(object)b.Hinge == (Object)null) && !((Object)(object)b.Door == (Object)null) && !b.Hinge.broken && !b.Hinge.dead)
		{
			return (Object)(object)b.Hinge.joint == (Object)null;
		}
		return true;
	}

	internal static float OpenAngle(Box b)
	{
		if (!((Object)(object)b.Hinge.joint != (Object)null))
		{
			return 0f;
		}
		return Mathf.Abs(b.Hinge.joint.angle);
	}

	internal static float OpenTarget(Box b)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		JointLimits limits = b.Hinge.joint.limits;
		float num = (b.Hinge.joint.useLimits ? Mathf.Max(Mathf.Abs(limits.min), Mathf.Abs(limits.max)) : 180f);
		HingeAnchor(b, out var axis);
		if (Mathf.Abs(axis.y) < 0.5f)
		{
			return Mathf.Max(num - 8f, Mathf.Min(num, 95f));
		}
		return Mathf.Min(75f, num * 0.7f);
	}

	internal static bool IsLid(Box b)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		HingeAnchor(b, out var axis);
		return Mathf.Abs(axis.y) < 0.5f;
	}

	private static bool LooksOpen(Box b)
	{
		return OpenAngle(b) > 30f;
	}

	private void LookForBoxes()
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _boxLookAt)
		{
			return;
		}
		_boxLookAt = Time.time + 0.8f;
		FindBoxes();
		if (Boxes.Count == 0)
		{
			return;
		}
		Vector3 val = Eye();
		foreach (Box box in Boxes)
		{
			if (BoxGone(box) || _boxDone.Contains(box))
			{
				continue;
			}
			Vector3 centerPoint = box.Door.centerPoint;
			Vector3 val2 = centerPoint - val;
			if (!(val2.sqrMagnitude > 196f) && InView(centerPoint, 65f, 2.5f) && ClearLine(val, centerPoint, box.Door, ((Component)box.Hinge).transform.parent))
			{
				_boxSeen.Add(box);
				if (LooksOpen(box) && (!OpenClaims.TryGetValue(box, out var value) || value == this))
				{
					_boxDone.Add(box);
				}
			}
		}
	}

	private bool WantsToOpen(Box b)
	{
		float num = (float)((Name.GetHashCode() * 31 + ((Object)b.Hinge).GetInstanceID()) & 0xFFFF) / 65535f;
		float num2 = 0.45f + 0.45f * P.Greed + 0.2f * P.Careful - 0.15f * P.Social;
		if (b.Kind == "toilet" || b.Kind == "porta potty")
		{
			num2 -= 0.35f;
		}
		return num < num2;
	}

	private bool TryStartOpen()
	{
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _openCheckAt || !SemiFunc.RunIsLevel() || Boxes.Count == 0)
		{
			return false;
		}
		_openCheckAt = Time.time + 1f;
		if (_bot.HeldView != 0 || _bot.HoldingStatic || InDanger() || TruckTime() || _slackerRun)
		{
			return false;
		}
		Box box = null;
		float num = float.MaxValue;
		float boxRange = BoxRange;
		foreach (Box item in _boxSeen)
		{
			if (BoxGone(item) || _boxDone.Contains(item) || _boxSkip.Contains(item) || (OpenClaims.TryGetValue(item, out var value) && value != this && value != null && value._openBox == item))
			{
				continue;
			}
			if (LooksOpen(item))
			{
				_boxDone.Add(item);
				continue;
			}
			Vector3 centerPoint = item.Door.centerPoint;
			float num2 = Vector3.Distance(centerPoint, _bot.Position);
			if (!(num2 > boxRange) && SafeSpot(centerPoint))
			{
				if (!WantsToOpen(item))
				{
					_boxSkip.Add(item);
				}
				else if (num2 < num)
				{
					num = num2;
					box = item;
				}
			}
		}
		if (box == null)
		{
			return false;
		}
		StartOpen(box, asked: false);
		if (box.Kind == "toilet" || box.Kind == "porta potty")
		{
			Say(Line("opentoilet", null, null, box.Kind), 0, "opentoilet:" + _bot.Name, 120f);
		}
		else if (Random.value < 0.3f)
		{
			Say(Line("openbox", null, null, box.Kind), 0, "openbox:" + _bot.Name, 40f);
		}
		return true;
	}

	private void StartOpen(Box b, bool asked)
	{
		DropJob("StartOpen", 235);
		_job = Job.Open;
		_openBox = b;
		_openStep = OpenStep.Approach;
		_openAsked = asked;
		_openSpot = null;
		_openSpotAt = 0f;
		_openSpotTries = 0;
		_openFlips = 0;
		_openRegrabs = 0;
		_jobStarted = Time.time;
		OpenClaims[b] = this;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " is going to open a " + b.Kind + " (" + ((Object)((Component)b.Hinge).transform.parent).name + ")"));
	}

	private void StopOpen()
	{
		Box openBox = _openBox;
		_openBox = null;
		if (openBox != null)
		{
			if (OpenClaims.TryGetValue(openBox, out var value) && value == this)
			{
				OpenClaims.Remove(openBox);
			}
			if (_bot.HeldView != 0 && (Object)(object)openBox.Door != (Object)null && (Object)(object)openBox.Door.photonView != (Object)null && _bot.HeldView == openBox.Door.photonView.ViewID)
			{
				BotGrab.Release(_bot);
			}
		}
	}

	private bool OpenStillGoing()
	{
		if (_openBox != null && !BoxGone(_openBox))
		{
			return Time.time - _jobStarted < 30f;
		}
		return false;
	}

	private void FinishOpen(bool looked)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		Box openBox = _openBox;
		if (openBox != null)
		{
			_boxDone.Add(openBox);
			if (looked)
			{
				foreach (BotBrain brain in BotManager.Brains)
				{
					if (brain != null && brain != this && brain.Alive && Vector3.Distance(brain.Position, openBox.Door.centerPoint) < 10f)
					{
						brain._boxDone.Add(openBox);
					}
				}
			}
		}
		StopOpen();
		DropJob("FinishOpen", 280);
		_thinkAt = 0f;
		if ((_openSweep > 0) & looked)
		{
			_openSweep--;
			Box box = NextInSweep();
			if (box != null)
			{
				int openSweep = _openSweep;
				StartOpen(box, asked: true);
				_openSweep = openSweep;
			}
			else
			{
				_openSweep = 0;
				Say(Line("opensweepdone"), 1, "opensweepdone:" + _bot.Name, 20f);
			}
		}
	}

	private Box NextInSweep()
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		Box result = null;
		float num = float.MaxValue;
		foreach (Box box in Boxes)
		{
			if (!BoxGone(box) && !_boxDone.Contains(box) && !LooksOpen(box) && (!OpenClaims.TryGetValue(box, out var value) || value == this || value == null || value._openBox != box))
			{
				float num2 = Vector3.Distance(box.Door.centerPoint, _openSweepAt);
				if (num2 < 10f && num2 < num)
				{
					num = num2;
					result = box;
				}
			}
		}
		return result;
	}

	internal static Vector3 HingeAnchor(Box b, out Vector3 axis)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		HingeJoint joint = b.Hinge.joint;
		Vector3 val = ((Component)joint).transform.TransformDirection(((Joint)joint).axis);
		axis = val.normalized;
		return ((Component)joint).transform.TransformPoint(((Joint)joint).anchor);
	}

	private static float FromAxis(Vector3 p, Vector3 anchor, Vector3 axis)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = p - anchor;
		Vector3 val2 = val - axis * Vector3.Dot(val, axis);
		return val2.magnitude;
	}

	private Vector3? FindOpenSpot(Box b)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		Vector3 centerPoint = b.Door.centerPoint;
		Vector3 anchor = HingeAnchor(b, out var axis);
		float num = FromAxis(centerPoint, anchor, axis) * 2f + 0.35f;
		Vector3? result = null;
		float num2 = float.MaxValue;
		NavMeshHit val2 = default;
		for (int i = 0; i < 16; i++)
		{
			Vector3 val = Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward;
			float[] array = new float[3] { 1.3f, 1.9f, 2.6f };
			foreach (float num3 in array)
			{
				if (!NavMesh.SamplePosition(centerPoint + val * num3, out val2, 1.2f, -1))
				{
					continue;
				}
				Vector3 position = val2.position;
				if ((Mathf.Abs(position.y - _bot.Position.y) > 3f && Mathf.Abs(position.y - centerPoint.y) > 2.5f) || FromAxis(new Vector3(position.x, centerPoint.y, position.z), anchor, axis) < num)
				{
					continue;
				}
				Vector3 val3 = position + Vector3.up * 1.5f;
				Vector3 val4 = centerPoint - val3;
				if (!(val4.magnitude > 3.1f) && BotGrab.GrabLine(_bot, val3, centerPoint, ((Component)b.Door).transform))
				{
					Vector3 val5 = Flat(centerPoint - b.Furniture.center);
					float num4;
					if (!(val5.sqrMagnitude > 0.01f))
					{
						num4 = 0f;
					}
					else
					{
						val4 = Flat(position - centerPoint);
						num4 = Vector3.Dot(val4.normalized, val5.normalized);
					}
					float num5 = num4;
					float num6 = Vector3.Distance(position, _bot.Position) - num5 * 1.5f;
					if (num6 < num2)
					{
						num2 = num6;
						result = position;
					}
				}
			}
		}
		return result;
	}

	private bool PickDoorPoint(Box b, Vector3 eye, out Vector3 point, out PhysGrabObjectCollider which)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		point = Vector3.zero;
		which = null;
		Vector3 val = HingeAnchor(b, out var axis);
		float num = float.MinValue;
		PhysGrabObjectCollider[] componentsInChildren = ((Component)b.Door).GetComponentsInChildren<PhysGrabObjectCollider>();
		foreach (PhysGrabObjectCollider val2 in componentsInChildren)
		{
			Collider component = ((Component)val2).GetComponent<Collider>();
			if ((Object)(object)component == (Object)null || !component.enabled || component.isTrigger)
			{
				continue;
			}
			Bounds bounds = component.bounds;
			Vector3 center = bounds.center;
			Vector3 val3 = center - val;
			Vector3 val4 = val3 - axis * Vector3.Dot(val3, axis);
			Vector3 val5 = component.ClosestPoint(center + val4 * 0.7f);
			Vector3[] array = new Vector3[4]
			{
				val5,
				Vector3.Lerp(center, val5, 0.5f),
				component.ClosestPoint(eye),
				center
			};
			foreach (Vector3 val6 in array)
			{
				Vector3 val7 = val6 - eye;
				if (!(val7.magnitude > 3.5f) && BotGrab.GrabLine(_bot, eye, val6, ((Component)b.Door).transform))
				{
					float num2 = FromAxis(val6, val, axis);
					val7 = val6 - eye;
					float num3 = num2 - val7.magnitude * 0.2f;
					if (num3 > num)
					{
						num = num3;
						point = val6;
						which = val2;
					}
				}
			}
		}
		return (Object)(object)which != (Object)null;
	}

	internal static float OpenDirection(Box b, Vector3 grab, Vector3 eye)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		HingeJoint joint = b.Hinge.joint;
		JointLimits limits = joint.limits;
		if (joint.useLimits && limits.max <= 1f && limits.min < -1f)
		{
			return -1f;
		}
		if (joint.useLimits && limits.min >= -1f && limits.max > 1f)
		{
			return 1f;
		}
		Vector3 val = HingeAnchor(b, out var axis);
		Vector3 val2 = grab - val;
		Vector3 val3 = val + Quaternion.AngleAxis(30f, axis) * val2;
		Vector3 val4 = val + Quaternion.AngleAxis(-30f, axis) * val2;
		Vector3 val5 = val3 - eye;
		float sqrMagnitude = val5.sqrMagnitude;
		val5 = val4 - eye;
		if (!(sqrMagnitude <= val5.sqrMagnitude))
		{
			return -1f;
		}
		return 1f;
	}

	private int SeenInside(Box b)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		Bounds furniture = b.Furniture;
		furniture.Expand(0.6f);
		int num = 0;
		foreach (ValuableObject item in Seen)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.physGrabObject != (Object)null && furniture.Contains(item.physGrabObject.centerPoint))
			{
				num++;
			}
		}
		return num;
	}

	private ValuableObject FirstInside(Box b)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		Bounds furniture = b.Furniture;
		furniture.Expand(0.6f);
		foreach (ValuableObject item in Seen)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.physGrabObject != (Object)null && furniture.Contains(item.physGrabObject.centerPoint))
			{
				return item;
			}
		}
		return null;
	}

	private Vector3 DoOpen(out Vector3? lookAt)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0461: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0509: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_051b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0520: Unknown result type (might be due to invalid IL or missing references)
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_0525: Unknown result type (might be due to invalid IL or missing references)
		//IL_054a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0560: Unknown result type (might be due to invalid IL or missing references)
		//IL_0565: Unknown result type (might be due to invalid IL or missing references)
		//IL_0567: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0704: Unknown result type (might be due to invalid IL or missing references)
		//IL_058a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0721: Unknown result type (might be due to invalid IL or missing references)
		//IL_072c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0731: Unknown result type (might be due to invalid IL or missing references)
		//IL_073b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0714: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_06de: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05af: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_045a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0403: Unknown result type (might be due to invalid IL or missing references)
		//IL_0408: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_044a: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0432: Unknown result type (might be due to invalid IL or missing references)
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0441: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		Box openBox = _openBox;
		if (BoxGone(openBox))
		{
			FinishOpen(looked: false);
			return Vector3.zero;
		}
		Vector3 val = Eye();
		Vector3 centerPoint = openBox.Door.centerPoint;
		float num = OpenAngle(openBox);
		float time = Time.time;
		if (_openStep == OpenStep.Peek)
		{
			lookAt = openBox.Furniture.center;
			if (time < _peekUntil)
			{
				return Vector3.zero;
			}
			LookForValuablesNow();
			int num2 = SeenInside(openBox) - _peekBefore;
			Plugin.Log.LogInfo((object)(_bot.Name + " looked in the " + openBox.Kind + ": " + ((num2 > 0) ? (num2 + " things") : "empty")));
			BotSave.Add(_bot.SteamId, "opened");
			if (num2 > 0)
			{
				BotSave.Add(_bot.SteamId, "found");
			}
			if (num2 > 0)
			{
				ValuableObject val2 = FirstInside(openBox);
				Say(Line("openfound", null, null, openBox.Kind), 1, "openfound:" + _bot.Name, 15f);
				if ((Object)(object)val2 != (Object)null)
				{
					_thinkAt = 0f;
				}
			}
			else if (_openAsked || Random.value < 0.35f)
			{
				Say(Line("openempty", null, null, openBox.Kind), _openAsked ? 1 : 0, "openempty:" + _bot.Name, _openAsked ? 5f : 45f);
			}
			FinishOpen(looked: true);
			return Vector3.zero;
		}
		if (_openStep == OpenStep.Pull)
		{
			int num3 = (((Object)(object)openBox.Door.photonView != (Object)null) ? openBox.Door.photonView.ViewID : 0);
			if (_bot.HeldView != num3 || num3 == 0)
			{
				_openStep = OpenStep.Approach;
				_openRegrabs++;
				return Vector3.zero;
			}
			lookAt = ((Component)openBox.Door).transform.TransformPoint(_openLocal);
			if (num >= OpenTarget(openBox))
			{
				BeginPeek(openBox);
				return Vector3.zero;
			}
			if (num > _openBest + 3f)
			{
				_openBest = num;
				_openProgressAt = time;
			}
			else
			{
				if (time - _openProgressAt > 0.9f && num >= 45f && !IsLid(openBox))
				{
					BeginPeek(openBox);
					return Vector3.zero;
				}
				if (time - _openProgressAt > 0.9f)
				{
					_openSign = 0f - _openSign;
					_openFlips++;
					_openProgressAt = time;
					if (_openFlips > 3)
					{
						Plugin.Log.LogInfo((object)(_bot.Name + " couldn't get the " + openBox.Kind + " open"));
						BotGrab.Release(_bot);
						Say(Line("openstuck", null, null, openBox.Kind), _openAsked ? 1 : 0, "openstuck:" + _bot.Name);
						FinishOpen(looked: false);
						return Vector3.zero;
					}
				}
			}
			Vector3 anchor = HingeAnchor(openBox, out var axis);
			float num4 = FromAxis(centerPoint, anchor, axis) * 2f + 0.2f;
			if (FromAxis(new Vector3(_bot.Position.x, centerPoint.y, _bot.Position.z), anchor, axis) < num4)
			{
				Vector3 val3 = Flat(_bot.Position - centerPoint);
				return ((val3.sqrMagnitude > 0.01f) ? val3.normalized : (-Flat(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward))) * 1.2f;
			}
			return Vector3.zero;
		}
		lookAt = centerPoint;
		if (LooksOpen(openBox) && (!OpenClaims.TryGetValue(openBox, out var value) || value == this))
		{
			BeginPeek(openBox);
			return Vector3.zero;
		}
		if (time - _jobStarted > 20f || _openRegrabs > 3)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " gave up on the " + openBox.Kind));
			_boxSkip.Add(openBox);
			FinishOpen(looked: false);
			return Vector3.zero;
		}
		Vector3 val4 = centerPoint - val;
		if (val4.magnitude < 3.2f && PickDoorPoint(openBox, val, out var point, out var which))
		{
			Vector3 anchor2 = HingeAnchor(openBox, out var axis2);
			float num5 = FromAxis(centerPoint, anchor2, axis2) * 2f + 0.1f;
			if ((FromAxis(new Vector3(_bot.Position.x, centerPoint.y, _bot.Position.z), anchor2, axis2) >= num5 || _openSpotTries >= 2) && BotGrab.GrabAt(_bot, openBox.Door, point, which))
			{
				_openLocal = ((Component)openBox.Door).transform.InverseTransformPoint(point);
				_openSign = OpenDirection(openBox, point, val);
				_openBest = num;
				_openProgressAt = time;
				_openStep = OpenStep.Pull;
				Plugin.Log.LogInfo((object)(_bot.Name + " grabbed the " + openBox.Kind + " door"));
				return Vector3.zero;
			}
		}
		if (!_openSpot.HasValue || time - _openSpotAt > 4f)
		{
			_openSpot = FindOpenSpot(openBox);
			_openSpotAt = time;
			_openSpotTries++;
			if (!_openSpot.HasValue)
			{
				if (_openSpotTries > 2)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " can't get to the " + openBox.Kind));
					_boxSkip.Add(openBox);
					if (_openAsked)
					{
						Say(Line("cantreach"), 1);
					}
					FinishOpen(looked: false);
					return Vector3.zero;
				}
				return Steer(_bot.Position, centerPoint) * 3.2f;
			}
		}
		val4 = Flat(_openSpot.Value - _bot.Position);
		if (val4.magnitude < 0.35f)
		{
			return Vector3.zero;
		}
		return Steer(_bot.Position, _openSpot.Value) * 3.2f;
	}

	private void LookForValuablesNow()
	{
		_seeAt = 0f;
		LookForValuables();
	}

	private void BeginPeek(Box b)
	{
		_openStep = OpenStep.Peek;
		_peekUntil = Time.time + Mathf.Lerp(0.7f, 1.6f, P.Careful) * Random.Range(0.85f, 1.2f);
		_peekBefore = SeenInside(b);
	}

	private void OpenPuller()
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		Box openBox = _openBox;
		if (!BoxGone(openBox))
		{
			if (_openStep == OpenStep.Peek)
			{
				_bot.Puller = ((Component)openBox.Door).transform.TransformPoint(_openLocal);
			}
			else if (_openStep == OpenStep.Pull)
			{
				Vector3 val = HingeAnchor(openBox, out var axis);
				Vector3 val2 = ((Component)openBox.Door).transform.TransformPoint(_openLocal);
				Vector3 val3 = val + Quaternion.AngleAxis(_openSign * 35f, axis) * (val2 - val);
				Vector3 val4 = Eye();
				_bot.Puller = val4 + Vector3.ClampMagnitude(val3 - val4, 4f);
			}
		}
	}

	public void OrderOpen(PlayerAvatar by, PhysGrabObject looked, bool all)
	{
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		if (!Alive)
		{
			return;
		}
		FindBoxes();
		HeardFrom(by);
		Box value = null;
		if ((Object)(object)looked != (Object)null)
		{
			PhysGrabHinge component = ((Component)looked).GetComponent<PhysGrabHinge>();
			if ((Object)(object)component != (Object)null && !BoxOf.TryGetValue(component, out value))
			{
				Say(Line("opendoor"), 3);
				return;
			}
		}
		Vector3 val = (((Object)(object)by != (Object)null) ? ((Component)by).transform.position : _bot.Position);
		if (value == null)
		{
			float num = float.MaxValue;
			Vector3 val2 = (((Object)(object)by != (Object)null) ? Flat(((Component)by).transform.forward) : Vector3.zero);
			foreach (Box box in Boxes)
			{
				if (BoxGone(box) || LooksOpen(box))
				{
					continue;
				}
				Vector3 centerPoint = box.Door.centerPoint;
				float num2 = Vector3.Distance(centerPoint, val);
				if (!(num2 > 10f))
				{
					Vector3 val3 = Flat(centerPoint - val);
					float num3 = num2 - Vector3.Dot(val3.normalized, val2) * 2f;
					if (num3 < num)
					{
						num = num3;
						value = box;
					}
				}
			}
		}
		if (value == null)
		{
			Say(Line("opennone"), 3);
			return;
		}
		ClearOrder(say: false);
		if (OpenClaims.TryGetValue(value, out var value2) && value2 != this && value2 != null && value2._openBox == value)
		{
			value2.DropJob("OrderOpen", 685);
		}
		_boxSeen.Add(value);
		_boxDone.Remove(value);
		_boxSkip.Remove(value);
		StartOpen(value, asked: true);
		_openSweep = (all ? 6 : 0);
		_openSweepAt = val;
		Say(Ack(Line(all ? "ackopenall" : "ackopen", null, null, value.Kind)), 3);
	}

	public BotBrain(BotInfo bot)
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		_bot = bot;
		_followDist = 2.2f + (float)(bot.Index % 3) * 0.6f;
		_sideBias = ((bot.Index % 2 == 0) ? 1f : (-1f)) * (0.8f + 0.3f * (float)(bot.Index / 2));
		_progressPos = bot.Position;
		_progressAt = Time.time;
		_thinkAt = Time.time + 0.2f * (float)bot.Index;
		if (SemiFunc.RunIsLevel())
		{
			BotSave.Add(bot.SteamId, "levels");
		}
	}

	public static void ResetLevel()
	{
		Claims.Clear();
		Blacklist.Clear();
		HumanHeldAt.Clear();
		ResetMemoryStatics();
		Fails.Clear();
		ResetTeamStatics();
		ResetCartStatics();
		ResetTalkStatics();
		ResetBoxStatics();
		DoorClaims.Clear();
		_activator = null;
		BotThreats.ResetLevel();
		BotSenses.ResetLevel();
	}

	public void Forget()
	{
		DropJob("Forget", 173);
	}

	public void Shove(Vector3 force)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_body != (Object)null && (Object)(object)_body.Rb != (Object)null && !_body.Rb.isKinematic)
		{
			_body.Rb.AddForce(force, (ForceMode)2);
			return;
		}
		BotInfo bot = _bot;
		bot.Velocity += force;
	}

	public void Tick(float dt)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0390: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_037e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_040a: Unknown result type (might be due to invalid IL or missing references)
		//IL_040f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		//IL_041b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0460: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_0422: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0433: Unknown result type (might be due to invalid IL or missing references)
		//IL_043a: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_0457: Unknown result type (might be due to invalid IL or missing references)
		//IL_0494: Unknown result type (might be due to invalid IL or missing references)
		//IL_0496: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_0481: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_04de: Unknown result type (might be due to invalid IL or missing references)
		//IL_04eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0518: Unknown result type (might be due to invalid IL or missing references)
		//IL_051d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0522: Unknown result type (might be due to invalid IL or missing references)
		//IL_052c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0531: Unknown result type (might be due to invalid IL or missing references)
		//IL_0560: Unknown result type (might be due to invalid IL or missing references)
		//IL_0569: Unknown result type (might be due to invalid IL or missing references)
		//IL_056e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0573: Unknown result type (might be due to invalid IL or missing references)
		//IL_0578: Unknown result type (might be due to invalid IL or missing references)
		//IL_0552: Unknown result type (might be due to invalid IL or missing references)
		//IL_0559: Unknown result type (might be due to invalid IL or missing references)
		//IL_055e: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_059c: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0613: Unknown result type (might be due to invalid IL or missing references)
		//IL_0615: Unknown result type (might be due to invalid IL or missing references)
		//IL_0618: Unknown result type (might be due to invalid IL or missing references)
		//IL_0605: Unknown result type (might be due to invalid IL or missing references)
		//IL_060c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0611: Unknown result type (might be due to invalid IL or missing references)
		//IL_0625: Unknown result type (might be due to invalid IL or missing references)
		//IL_0627: Unknown result type (might be due to invalid IL or missing references)
		//IL_0639: Unknown result type (might be due to invalid IL or missing references)
		//IL_063d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0642: Unknown result type (might be due to invalid IL or missing references)
		//IL_0806: Unknown result type (might be due to invalid IL or missing references)
		//IL_080b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0810: Unknown result type (might be due to invalid IL or missing references)
		//IL_0812: Unknown result type (might be due to invalid IL or missing references)
		//IL_0814: Unknown result type (might be due to invalid IL or missing references)
		//IL_081d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0822: Unknown result type (might be due to invalid IL or missing references)
		//IL_082a: Unknown result type (might be due to invalid IL or missing references)
		//IL_082f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0832: Unknown result type (might be due to invalid IL or missing references)
		//IL_0834: Unknown result type (might be due to invalid IL or missing references)
		//IL_0837: Unknown result type (might be due to invalid IL or missing references)
		//IL_083c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0841: Unknown result type (might be due to invalid IL or missing references)
		//IL_0844: Unknown result type (might be due to invalid IL or missing references)
		//IL_0846: Unknown result type (might be due to invalid IL or missing references)
		//IL_0848: Unknown result type (might be due to invalid IL or missing references)
		//IL_084d: Unknown result type (might be due to invalid IL or missing references)
		//IL_084f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0851: Unknown result type (might be due to invalid IL or missing references)
		//IL_0853: Unknown result type (might be due to invalid IL or missing references)
		//IL_0858: Unknown result type (might be due to invalid IL or missing references)
		//IL_0868: Unknown result type (might be due to invalid IL or missing references)
		//IL_086d: Unknown result type (might be due to invalid IL or missing references)
		//IL_086f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0876: Unknown result type (might be due to invalid IL or missing references)
		//IL_087b: Unknown result type (might be due to invalid IL or missing references)
		//IL_087e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0880: Unknown result type (might be due to invalid IL or missing references)
		//IL_088d: Unknown result type (might be due to invalid IL or missing references)
		//IL_088f: Unknown result type (might be due to invalid IL or missing references)
		//IL_089a: Unknown result type (might be due to invalid IL or missing references)
		//IL_089c: Unknown result type (might be due to invalid IL or missing references)
		//IL_066d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0672: Unknown result type (might be due to invalid IL or missing references)
		//IL_0674: Unknown result type (might be due to invalid IL or missing references)
		//IL_0676: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0682: Unknown result type (might be due to invalid IL or missing references)
		//IL_0689: Unknown result type (might be due to invalid IL or missing references)
		//IL_068e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0735: Unknown result type (might be due to invalid IL or missing references)
		//IL_0756: Unknown result type (might be due to invalid IL or missing references)
		//IL_075b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0768: Unknown result type (might be due to invalid IL or missing references)
		//IL_076d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0772: Unknown result type (might be due to invalid IL or missing references)
		//IL_0775: Unknown result type (might be due to invalid IL or missing references)
		//IL_0777: Unknown result type (might be due to invalid IL or missing references)
		//IL_078f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0794: Unknown result type (might be due to invalid IL or missing references)
		//IL_079f: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ad2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ace: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a05: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a07: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a23: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a2a: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null)
		{
			return;
		}
		TalkTick();
		if (avatar.isDisabled || avatar.deadSet)
		{
			if (!_wasDisabled)
			{
				_dropNow = true;
				_setDown = null;
				BotSave.Add(_bot.SteamId, "deaths");
				ClearOrder(say: false);
				PutMapAway(finished: false);
				_body?.SetActive(active: false);
				DropJob("Tick", 200);
				if (_bot.HeldView != 0)
				{
					BotGrab.Release(_bot);
				}
				_dropNow = false;
				_bot.Velocity = Vector3.zero;
				_bot.Moving = false;
				_bot.Sprinting = false;
				_bot.Crouching = false;
			}
			_wasDisabled = true;
			return;
		}
		if (_wasDisabled)
		{
			_wasDisabled = false;
			BotHealth.Heal(avatar.playerHealth, 1, effect: true);
			BotSave.Add(_bot.SteamId, "revived");
			Plugin.Log.LogInfo((object)(_bot.Name + " is back on their feet"));
			OnRevived();
			_bot.Position = ((Component)avatar).transform.position;
			_bot.Velocity = Vector3.zero;
			if ((Object)(object)_body != (Object)null)
			{
				_body.Teleport(_bot.Position);
				_body.SetActive(active: true);
			}
			ResetProgress();
			_corners.Clear();
		}
		PlayerTumble tumble = avatar.tumble;
		if ((Object)(object)tumble != (Object)null && tumble.isTumbling)
		{
			WhileTumbling(tumble);
			return;
		}
		if (_wasTumbling)
		{
			_wasTumbling = false;
			Vector3 val = (((Object)(object)tumble != (Object)null) ? (((Component)tumble).transform.position - Vector3.up * 0.3f) : ((Component)avatar).transform.position);
			_bot.Position = SnapToGround(val, val);
			_bot.Velocity = Vector3.zero;
			if ((Object)(object)_body != (Object)null)
			{
				_body.Teleport(_bot.Position);
				_body.SetActive(active: true);
			}
			_corners.Clear();
			ResetProgress();
		}
		UpdateMemory();
		LookForValuables();
		LookForBoxes();
		bool flag = SetDownTick(out var lookAt);
		if (Time.time >= _thinkAt && !flag)
		{
			_thinkAt = Time.time + 0.4f;
			if (!OpeningDoor)
			{
				Think();
			}
			else if (Plugin.BotsFlee.Value)
			{
				Job job = _job;
				if (AssessDanger() && _job != job && OpeningDoor)
				{
					EndDoor(opened: true);
				}
			}
		}
		Vector3? lookAt2 = null;
		bool crawl = false;
		bool sprint = false;
		bool crouch = false;
		Vector3 desired;
		if (flag)
		{
			desired = Vector3.zero;
			lookAt2 = lookAt;
		}
		else
		{
			switch (_job)
			{
			case Job.Freeze:
				desired = DoFreeze(out crawl);
				crouch = true;
				break;
			case Job.Flee:
				desired = DoFlee(out sprint);
				break;
			case Job.Hide:
				desired = DoHide(out crouch, out lookAt2);
				break;
			case Job.Revive:
			case Job.Haul:
				desired = DoCarry(out lookAt2);
				break;
			case Job.Cart:
				desired = DoCart(out lookAt2);
				break;
			case Job.Activate:
				desired = DoActivate();
				break;
			case Job.Explore:
				desired = DoExplore();
				break;
			case Job.Truck:
				desired = DoTruck(out sprint, out lookAt2);
				break;
			case Job.Assist:
				desired = DoAssist(out lookAt2);
				break;
			case Job.Escort:
				desired = DoEscort(out sprint, out lookAt2);
				break;
			case Job.Guard:
				desired = DoGuard(out lookAt2);
				break;
			case Job.Heal:
				desired = DoHeal(out lookAt2);
				break;
			case Job.Open:
				desired = DoOpen(out lookAt2);
				break;
			case Job.HangOut:
				desired = DoHangOut(out lookAt2);
				break;
			case Job.Gnome:
				desired = DoGnome(out lookAt2);
				break;
			case Job.Use:
				desired = DoUse(out lookAt2);
				break;
			case Job.Order:
				desired = DoOrder(out sprint, out lookAt2);
				_orderMoving = desired.sqrMagnitude > 0.01f;
				break;
			default:
				desired = DoFollow(out sprint);
				break;
			}
		}
		EscapingZone(ref desired, ref sprint);
		Vector3 val2 = Flat(desired);
		_jobWantsToMove = val2.magnitude > 0.3f;
		if (Time.time < _listenUntil)
		{
			desired = Vector3.zero;
		}
		float num = MapTick();
		if (num < 1f)
		{
			desired *= num;
			sprint = false;
		}
		if (Typing)
		{
			desired = Vector3.zero;
			sprint = false;
			lookAt2 = Eye() + Quaternion.Euler(_bot.Pitch, _bot.Yaw, 0f) * Vector3.forward * 5f;
		}
		if (_quiet && _job != Job.Freeze)
		{
			sprint = false;
			crouch = true;
			desired = Vector3.ClampMagnitude(desired, 1.6f);
		}
		desired += Separation(_bot.Position);
		if (Time.time < _grabCrouchUntil)
		{
			crouch = true;
		}
		int num2;
		if (_job == Job.Hide && _hideUnder)
		{
			val2 = Flat(_hideSpot - _bot.Position);
			if (val2.magnitude < 1.6f)
			{
				num2 = 1;
				goto IL_05d5;
			}
		}
		num2 = (((_job == Job.Freeze) & crawl) ? 1 : 0);
		goto IL_05d5;
		IL_05d5:
		bool wantCrawl = (byte)num2 != 0;
		UpdatePosture(desired, crouch, wantCrawl);
		if (_posture == 2)
		{
			desired = Vector3.ClampMagnitude(desired, 0.9f);
		}
		else if (_posture == 1)
		{
			desired = Vector3.ClampMagnitude(desired, 1.6f);
		}
		Vector3 val3 = desired;
		if (DoorTick(val3, ref desired, ref lookAt2))
		{
			val3 = desired;
			sprint = false;
		}
		if (Plugin.KeyboardMouse.Value)
		{
			desired = Keys(desired, sprint);
		}
		PitGuard(ref desired);
		Vector3 val5;
		if (Plugin.RealPhysics.Value && (Object)(object)Body() != (Object)null)
		{
			Vector3 val4 = EscapeFromCart();
			if (val4 != Vector3.zero)
			{
				desired = val4 * 3.2f;
				_body.WantJump = true;
			}
			_body.WantVelocity = desired;
			if (sprint && _job != Job.Flee && !_slackerRun && _body.EnergyMax > 0f)
			{
				float num3 = _body.Energy / _body.EnergyMax;
				if (num3 < 0.3f)
				{
					_savingStamina = true;
				}
				else if (num3 >= 0.75f)
				{
					_savingStamina = false;
				}
				if (_savingStamina)
				{
					sprint = false;
				}
			}
			_body.WantSprint = sprint;
			_body.Posture = _posture;
			if (ShouldJump(desired))
			{
				_body.WantJump = true;
			}
			SyncBodyIgnores();
			Vector3 feet = _body.Feet;
			val5 = Flat(_body.Rb.velocity);
			CheckBlocked(desired, val5);
			_bot.Velocity = _body.Rb.velocity;
			_bot.Position = feet;
			_bot.Moving = val5.sqrMagnitude > 0.2f;
			_bot.Sprinting = _body.Sprinting && _bot.Moving;
			_bot.Grounded = _body.Grounded;
		}
		else
		{
			val5 = Flat(_bot.Velocity);
			val5 = Vector3.MoveTowards(val5, desired, 18f * dt);
			Vector3 position = _bot.Position;
			Vector3 p = MoveWithCollision(position, val5 * dt);
			Vector3 val6 = SnapToGround(p, position);
			val5 = Flat(val6 - position) / Mathf.Max(dt, 0.0001f);
			val5 = Vector3.ClampMagnitude(val5, 5.6f);
			CheckBlocked(desired, val5);
			_bot.Velocity = val5;
			_bot.Position = val6;
			_bot.Moving = val5.sqrMagnitude > 0.2f;
			_bot.Sprinting = sprint && _bot.Moving && _posture == 0;
			_bot.Grounded = true;
		}
		_bot.Crouching = _posture >= 1;
		_bot.Crawling = _posture == 2;
		if (Plugin.CarryLog.Value && _bot.HeldView != 0 && (_job == Job.Haul || _job == Job.Revive) && (Object)(object)_target != (Object)null && Time.time >= _carryLogAt)
		{
			_carryLogAt = Time.time + 3f;
			ManualLogSource log = Plugin.Log;
			object[] array = new object[10]
			{
				_bot.Name,
				((Object)_target).name.Replace("(Clone)", ""),
				_bot.Position.x,
				_bot.Position.z,
				null,
				null,
				null,
				null,
				null,
				null
			};
			val2 = Flat(val5);
			array[4] = val2.magnitude;
			val2 = Flat(val3);
			array[5] = val2.magnitude;
			array[6] = Vector3.Distance(_target.centerPoint, HoldPoint());
			array[7] = _carry;
			ref object reference = ref array[8];
			object obj;
			if (_pathFailed)
			{
				obj = "none";
			}
			else
			{
				obj = (_pathPartial ? "partial" : (_corners.Count - _corner + " corners left"));
			}
			reference = obj;
			array[9] = ((_blockedSince > 0f) ? (Time.time - _blockedSince) : 0f);
			log.LogInfo((object)string.Format("[carry] {0} {1}: at {2:0.0},{3:0.0} speed {4:0.0} want {5:0.0} item {6:0.0}m from hands, {7}, path {8}, blocked {9:0.0}s", array));
		}
		Look(Plugin.KeyboardMouse.Value ? val3 : val5, lookAt2, dt);
		UpdatePuller(dt);
		CheckStuck();
	}

	private void Think()
	{
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		PruneClaims();
		if (Plugin.BotsFlee.Value && AssessDanger())
		{
			return;
		}
		if (_order != OrderKind.None)
		{
			if (SemiFunc.RunIsLevel() && (TruckTime() || _slackerRun))
			{
				ClearOrder(say: false);
			}
			else if (RunOrder())
			{
				return;
			}
		}
		if ((_job != Job.Revive && _job != Job.Truck && _job != Job.Cart && _job != Job.Heal && TryHelpPerson()) || (SemiFunc.RunIsShop() && !_slackerRun && _job != Job.Haul && _job != Job.Heal && TryStartShop()))
		{
			return;
		}
		if ((_job == Job.Haul || _job == Job.Revive) && (Object)(object)_target != (Object)null && !_target.dead)
		{
			if (Time.time - _jobStarted < 75f)
			{
				return;
			}
			if (_bot.HeldView != 0 && Time.time - _jobStarted < 240f)
			{
				if (Time.time >= _haulCheckAt)
				{
					_haulMoved = Vector3.Distance(_target.centerPoint, _haulCheckPos) > 4f;
					_haulCheckPos = _target.centerPoint;
					_haulCheckAt = Time.time + 20f;
				}
				if (_haulMoved)
				{
					return;
				}
			}
			Plugin.Log.LogInfo((object)(_bot.Name + " gave up on " + ((Object)_target).name));
			Failed(_target, 60f);
			DropJob("Think", 504);
		}
		else if (_job == Job.Cart && (Object)(object)_pushing != (Object)null)
		{
			if (Time.time - _jobStarted < 90f && !TruckTime())
			{
				return;
			}
			DropJob("Think", 510);
		}
		else if (_job == Job.Activate)
		{
			if ((Object)(object)_activateTarget != (Object)null && _activateTarget.StateIs((ExtractionPoint.State)1) && !RoundDirector.instance.extractionPointActive)
			{
				return;
			}
			DropJob("Think", 516);
		}
		else
		{
			if ((_job == Job.Truck && (TruckTime() || _slackerRun || _finalSeen)) || (_job == Job.Assist && AssistStillNeeded()) || (_job == Job.Heal && HealStillGoing()) || (_job == Job.Open && OpenStillGoing() && !TruckTime()) || (_job == Job.Gnome && GnomeStillGoing() && !TruckTime()) || (_job == Job.Escort && EscortStillNeeded()) || (_job == Job.Use && UseStillGoing()))
			{
				return;
			}
			if (_job != Job.Follow && _job != Job.Explore && _job != Job.HangOut)
			{
				DropJob("Think", 548);
			}
		}
		if (TryStartRevive())
		{
			return;
		}
		LearnTeamHealthInTruck();
		if (!TryStartHeal() && !TryStartTruck() && !TryStartGnome() && !TryStartAssist() && !TryStartEscort() && !TryStartDrive() && (!Plugin.BotsHaul.Value || !TryStartCart()) && (!Plugin.BotsHaul.Value || !TryStartActivate()) && (!Plugin.BotsHaul.Value || !TryStartHaul()) && (!Plugin.BotsOpen.Value || !TryStartOpen()) && (!SemiFunc.RunIsLobby() || !TryStartUseItem()))
		{
			if (HangingOutHere)
			{
				_job = Job.HangOut;
			}
			else if (TagAlong())
			{
				_job = Job.Follow;
			}
			else if (!Plugin.BotsExplore.Value || !TryStartExplore())
			{
				_job = Job.Follow;
			}
		}
	}

	private bool TagAlong()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (time >= _tagAlongRollAt)
		{
			_tagAlongRollAt = time + 25f;
			float num = Mathf.Clamp01((P.Social - 0.45f) * 1.6f);
			PlayerAvatar val = NearestPerson(_bot.Position);
			Vector3? val2 = (((Object)(object)val != (Object)null) ? PersonPos(val) : ((Vector3?)null));
			if (val2.HasValue && Vector3.Distance(val2.Value, _bot.Position) < 25f && Random.value < num)
			{
				_tagAlongUntil = time + 25f;
				Plugin.Log.LogInfo((object)(_bot.Name + " is tagging along with " + val.playerName + " for a bit"));
			}
		}
		if (time < _tagAlongUntil)
		{
			return (Object)(object)PickLeader() != (Object)null;
		}
		return false;
	}

	private void Failed(PhysGrabObject o, float blacklistFor)
	{
		if (!((Object)(object)o == (Object)null))
		{
			Fails.TryGetValue(o, out var value);
			value = (Fails[o] = value + 1);
			Blacklist[o] = Time.time + ((value >= 3) ? 99999f : blacklistFor);
			if (value >= 3)
			{
				Plugin.Log.LogInfo((object)$"{_bot.Name} is leaving {((Object)o).name} alone (failed {value} times)");
			}
		}
	}

	private void DropJob([CallerMemberName] string who = "", [CallerLineNumber] int line = 0)
	{
		if ((Object)(object)_target != (Object)null && (_job == Job.Haul || _job == Job.Revive))
		{
			Plugin.Log.LogInfo((object)string.Format("{0} stopped {1} of {2} ({3}:{4}, {5})", _bot.Name, _job, ((Object)_target).name, who, line, (_bot.HeldView != 0) ? "holding" : "not holding"));
		}
		if (_bot.HeldView != 0)
		{
			ReleaseHeld("DropJob", 634);
		}
		if ((Object)(object)_target != (Object)null && Claims.TryGetValue(_target, out var value) && value == this)
		{
			Claims.Remove(_target);
		}
		if ((Object)(object)_pushing != (Object)null && Claims.TryGetValue(_pushing.physGrabObject, out var value2) && value2 == this)
		{
			Claims.Remove(_pushing.physGrabObject);
		}
		if (_activator == this)
		{
			_activator = null;
		}
		_target = null;
		_reviveOf = null;
		_reviveEP = null;
		_cart = null;
		_pushing = null;
		_activateTarget = null;
		_carryProblemSince = -1f;
		StopAssist();
		if ((Object)(object)_healing != (Object)null)
		{
			StopHeal(finished: false);
		}
		if (_openBox != null)
		{
			StopOpen();
		}
		_door = null;
		_doorHolding = false;
		_gnome = null;
		_backDoor = null;
		_escortHaul = false;
		_bringTo = null;
		ShopJobEnded(who);
		_shopItem = null;
		_shopPutBack = false;
		if ((Object)(object)_useItem != (Object)null && _job == Job.Use)
		{
			_useItem = null;
		}
		if (_screenMode)
		{
			StopScreen();
		}
		_job = Job.Follow;
	}

	private bool AssessDanger()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0706: Unknown result type (might be due to invalid IL or missing references)
		//IL_0711: Unknown result type (might be due to invalid IL or missing references)
		//IL_0716: Unknown result type (might be due to invalid IL or missing references)
		//IL_071b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0720: Unknown result type (might be due to invalid IL or missing references)
		//IL_068d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_074d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0759: Unknown result type (might be due to invalid IL or missing references)
		//IL_075e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0731: Unknown result type (might be due to invalid IL or missing references)
		//IL_0738: Unknown result type (might be due to invalid IL or missing references)
		//IL_06ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_06fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a62: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a6d: Unknown result type (might be due to invalid IL or missing references)
		//IL_07fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0805: Unknown result type (might be due to invalid IL or missing references)
		//IL_0821: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_08db: Unknown result type (might be due to invalid IL or missing references)
		//IL_09c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_09cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_099f: Unknown result type (might be due to invalid IL or missing references)
		//IL_092e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0933: Unknown result type (might be due to invalid IL or missing references)
		//IL_08ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Unknown result type (might be due to invalid IL or missing references)
		//IL_0443: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f2: Unknown result type (might be due to invalid IL or missing references)
		_quiet = false;
		_dontLook.Clear();
		_ = _bot.Position;
		Vector3 val = Eye();
		bool flag = Time.time - _bot.LastHurtTime < 2f;
		float nerve = Nerve;
		float num = Mathf.Lerp(14f, 7f, nerve);
		float num2 = Mathf.Lerp(7f, 3.5f, nerve);
		float num3 = Mathf.Lerp(9f, 3.5f, nerve);
		Threat threat = null;
		bool fleeRun = true;
		Threat threat2 = null;
		Threat threat3 = null;
		float num4 = float.MaxValue;
		float num5 = float.MaxValue;
		Perceive();
		bool flag2 = _job == Job.Truck || TruckTime() || _slackerRun || _finalSeen;
		bool flag3 = (Object)(object)_bot.Avatar.RoomVolumeCheck != (Object)null && _bot.Avatar.RoomVolumeCheck.inTruck;
		foreach (Threat item in KnownThreats())
		{
			float num6 = Vector3.Distance(item.Position, val);
			if (num6 > 40f || IsGnome(item))
			{
				continue;
			}
			bool flag4 = _seenNow.Contains(item.Parent);
			bool flag5 = (Object)(object)item.Target == (Object)(object)_bot.Avatar;
			bool flag6 = item.State.Contains("Leave") || item.State.Contains("Despawn") || item.State.Contains("Stun") || item.State.Contains("Spawn") || item.State.Contains("Disabled") || item.State.Contains("Dead") || item.State.Contains("Death");
			switch (item.Rule)
			{
			case ThreatRule.Freeze:
				if (!flag6)
				{
					if (num6 < 40f)
					{
						_quiet = true;
					}
					if (num6 < 9f || (_bot.HeldView != 0 && num6 < 16f) || (num6 < 22f && HunterComing(item)))
					{
						threat2 = item;
					}
					continue;
				}
				break;
			case ThreatRule.DontLook:
				if (num6 < 25f)
				{
					_dontLook.Add(item.Position);
				}
				if ((item.Aggressive & flag5) && num6 < 15f && num6 < num4)
				{
					threat = item;
					num4 = num6;
					fleeRun = false;
				}
				continue;
			case ThreatRule.Harmless:
				if (item.Aggressive && !item.State.Contains("Small") && (flag5 || num6 < 6f) && num6 < num4)
				{
					threat = item;
					num4 = num6;
				}
				continue;
			case ThreatRule.Explosive:
				if (item.Aggressive && num6 < 10f && num6 < num4)
				{
					threat = item;
					num4 = num6;
				}
				continue;
			}
			if (!flag6)
			{
				bool flag7 = item.Aggressive & flag5;
				bool flag8 = Deadly.Contains(item.Kind);
				float num7 = (flag8 ? 4.5f : 2.5f);
				bool flag9 = (flag7 && (flag4 || num6 < 6f)) || (flag && num6 < 14f) || num6 < num7;
				if (item.Kind == "EnemySlowWalker" && num6 < 9f && !flag6)
				{
					flag9 = true;
				}
				bool flag10 = !flag4 && item.State == "Unknown" && num6 < num3;
				if (flag8 && !flag4 && item.State == "Unknown" && num6 < 8f && !flag2)
				{
					flag9 = true;
				}
				bool flag11 = item.Aggressive && num6 < (flag8 ? Mathf.Max(num, 12f) : num) && ((((Object)(object)item.Target == (Object)null) | flag5) || Vector3.Distance(((Component)item.Target).transform.position, _bot.Position) < 6f);
				bool flag12 = !flag9 && (((flag7 | flag11) || (flag4 && num6 < (flag8 ? Mathf.Max(num2, 9f) : num2))) | flag10);
				if (flag2)
				{
					flag12 = flag3 && (flag12 | flag9) && num6 < 14f;
					flag9 = flag9 && (flag7 | flag) && num6 < 8f && !flag3;
				}
				if (((item.Kind == "EnemyRobe") & flag5) && num6 < 25f)
				{
					_dontLook.Add(item.Position);
				}
				if (flag9 && num6 < num4)
				{
					threat = item;
					num4 = num6;
					fleeRun = true;
				}
				else if (flag12 && num6 < num5)
				{
					threat3 = item;
					num5 = num6;
				}
			}
		}
		if (threat2 != null && threat == null)
		{
			if (_job != Job.Freeze)
			{
				_jobBeforeFreeze = ((_job != Job.Flee && _job != Job.Hide) ? _job : Job.Follow);
				_frozeAt = Time.time;
				_sneakTo = null;
				Plugin.Log.LogInfo((object)(_bot.Name + " is holding still, " + threat2.Kind + " is nearby"));
				if (_job == Job.Flee)
				{
					_job = Job.Follow;
				}
			}
			if (threat2.Kind == "EnemyHunter")
			{
				HuntsmanSneak(threat2);
			}
			_danger = threat2;
			_freezeUntil = Time.time + 2.5f;
			_job = Job.Freeze;
			return true;
		}
		if (_job == Job.Freeze && Time.time < _freezeUntil)
		{
			return true;
		}
		if (threat != null)
		{
			if (_job != Job.Flee)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " is getting away from " + threat.Kind + " (" + threat.State + ")"));
				OnFleeStart(threat);
				ShoveCartAway(threat.Position);
				_dropNow = num4 < 10f || threat.Rule == ThreatRule.Explosive || Time.time - _bot.LastHurtTime < 2f;
				DropJob("AssessDanger", 822);
				_dropNow = false;
				_job = Job.Flee;
				_fleeTo = FleeSpot(threat.Position, threat.Kind);
			}
			else
			{
				Vector3 val2 = Flat(_fleeTo - _bot.Position);
				if (val2.magnitude < 1.2f || Vector3.Distance(_fleeTo, threat.Position) < 6f)
				{
					_fleeTo = FleeSpot(threat.Position, threat.Kind);
				}
			}
			_danger = threat;
			_fleeRun = fleeRun;
			_fleeUntil = Time.time + 2f;
			return true;
		}
		if (_job == Job.Flee && Time.time < _fleeUntil)
		{
			return true;
		}
		if (threat3 != null)
		{
			bool flag13 = (Object)(object)_bot.Avatar.RoomVolumeCheck != (Object)null && _bot.Avatar.RoomVolumeCheck.inTruck;
			bool flag14 = LooksUnder.Contains(threat3.Kind) && !flag13;
			if (_job != Job.Hide || Vector3.Distance(_hideSpot, threat3.Position) < 3f)
			{
				if (FindHideSpot(threat3.Position, !flag14, out var spot, out var under, flag13))
				{
					if (_job != Job.Hide)
					{
						Plugin.Log.LogInfo((object)(_bot.Name + " is hiding from " + threat3.Kind + " (" + threat3.State + ")" + (under ? " under something" : "")));
						ShoveCartAway(threat3.Position);
						DropJob("AssessDanger", 853);
						_hideStarted = Time.time;
					}
					_job = Job.Hide;
					_hideSpot = spot;
					_hideUnder = under;
					_corners.Clear();
				}
				else if (flag13)
				{
					if (_job != Job.Hide)
					{
						DropJob("AssessDanger", 866);
						_hideStarted = Time.time;
					}
					_job = Job.Hide;
					_hideSpot = _bot.Position;
					_hideUnder = false;
				}
				else
				{
					if (_job != Job.Flee)
					{
						Plugin.Log.LogInfo((object)(_bot.Name + " is backing away from " + threat3.Kind + " (" + threat3.State + ")"));
						ShoveCartAway(threat3.Position);
						DropJob("AssessDanger", 880);
					}
					_job = Job.Flee;
					_fleeTo = FleeSpot(threat3.Position);
					_fleeRun = false;
					_fleeUntil = Time.time + 2f;
				}
			}
			_danger = threat3;
			_hideCalmSince = -1f;
			return true;
		}
		if (_job == Job.Hide)
		{
			if (_danger != null && (Object)(object)_danger.Parent != (Object)null && _known.TryGetValue(_danger.Parent, out var value))
			{
				float num8 = Mathf.Max(value.SeenAt, value.HeardAt);
				float num9 = Vector3.Distance(value.Pos, _bot.Position);
				float num10 = Mathf.Lerp(18f, 7f, P.Brave) + ((num9 < 10f) ? Mathf.Lerp(12f, 4f, P.Brave) : 0f);
				if (!_danger.State.Contains("Leave") && !_danger.State.Contains("Despawn") && !_danger.State.Contains("Dead") && !_danger.State.Contains("Disabled") && Time.time - num8 < num10)
				{
					_hideCalmSince = -1f;
					return true;
				}
			}
			if (_hideCalmSince < 0f)
			{
				_hideCalmSince = Time.time;
			}
			if (Time.time - _hideCalmSince < Mathf.Lerp(8f, 2f, P.Brave))
			{
				return true;
			}
			Plugin.Log.LogInfo((object)(_bot.Name + " came out of hiding"));
			OnCameOutOfHiding();
			_job = Job.Follow;
		}
		if (_job == Job.Flee || _job == Job.Freeze)
		{
			bool flag15 = _job == Job.Flee;
			ref Job job = ref _job;
			Job job2;
			if ((Object)(object)_target != (Object)null)
			{
				job2 = (((Object)(object)_reviveOf != (Object)null) ? Job.Revive : Job.Haul);
			}
			else
			{
				job2 = (((Object)(object)_pushing != (Object)null) ? Job.Cart : Job.Follow);
			}
			job = job2;
			if (!flag15 && _job == Job.Follow && _jobBeforeFreeze != Job.Follow)
			{
				_job = _jobBeforeFreeze;
				_jobStarted = Time.time;
			}
			_jobBeforeFreeze = Job.Follow;
			if (flag15)
			{
				AfterChase();
			}
			if (_job != Job.Follow)
			{
				return true;
			}
		}
		_danger = null;
		return false;
	}

	private static void PruneClaims()
	{
		List<PhysGrabObject> list = null;
		foreach (KeyValuePair<PhysGrabObject, BotBrain> claim in Claims)
		{
			if ((Object)(object)claim.Key == (Object)null || ((Object)(object)claim.Value._target != (Object)(object)claim.Key && ((Object)(object)claim.Value._pushing == (Object)null || (Object)(object)claim.Value._pushing.physGrabObject != (Object)(object)claim.Key)))
			{
				(list ?? (list = new List<PhysGrabObject>())).Add(claim.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (PhysGrabObject item in list)
		{
			Claims.Remove(item);
		}
	}

	private bool Available(PhysGrabObject o)
	{
		if ((Object)(object)o == (Object)null || o.dead || (Object)(object)o.rb == (Object)null || (Object)(object)o.photonView == (Object)null)
		{
			return false;
		}
		if (Claims.TryGetValue(o, out var value) && value != this)
		{
			return false;
		}
		if (Blacklist.TryGetValue(o, out var value2) && Time.time < value2)
		{
			return false;
		}
		foreach (PhysGrabber item in o.playerGrabbing)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.playerAvatar != (Object)(object)_bot.Avatar)
			{
				if (!BotRegistry.IsBot(item.playerAvatar))
				{
					HumanHeldAt[o] = Time.time;
				}
				return false;
			}
		}
		if (HumanHeldAt.TryGetValue(o, out var value3) && Time.time - value3 < 8f)
		{
			return false;
		}
		return true;
	}

	private bool SafeSpot(Vector3 p)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (InHurtZone(p) || !BotPits.SpotOk(p))
		{
			return false;
		}
		foreach (Threat item in KnownThreats())
		{
			if (item.Rule != ThreatRule.Harmless || item.Aggressive)
			{
				float num = Vector3.Distance(p, item.Position);
				if (num < 5f || (item.Aggressive && num < 9f) || (item.Rule == ThreatRule.Freeze && num < 22f))
				{
					return false;
				}
			}
		}
		return true;
	}

	private ExtractionPoint ReviveExtraction(Vector3 head)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		RoundDirector instance = RoundDirector.instance;
		if ((Object)(object)instance == (Object)null || instance.allExtractionPointsCompleted || (Object)(object)TruckSafetySpawnPoint.instance == (Object)null)
		{
			return null;
		}
		Vector3.Distance(head, ((Component)TruckSafetySpawnPoint.instance).transform.position);
		ExtractionPoint result = null;
		float num = float.MaxValue;
		foreach (GameObject extractionPoint in instance.extractionPointList)
		{
			ExtractionPoint val = (((Object)(object)extractionPoint != (Object)null) ? extractionPoint.GetComponent<ExtractionPoint>() : null);
			if (!((Object)(object)val == (Object)null) && ((val.StateIs((ExtractionPoint.State)1) && !val.isLocked) || val.StateIs((ExtractionPoint.State)2) || val.StateIs((ExtractionPoint.State)4)))
			{
				Vector3 val2 = ExtractionSpot(val);
				float num2 = Vector3.Distance(head, val2);
				if (num2 < num && OnNavmesh(val2) && SafeSpot(val2))
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	private bool TryStartRevive()
	{
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		if (!SemiFunc.RunIsLevel() || (Object)(object)TruckSafetySpawnPoint.instance == (Object)null)
		{
			return false;
		}
		if ((Object)(object)RoundDirector.instance != (Object)null && RoundDirector.instance.allExtractionPointsCompleted)
		{
			return false;
		}
		PlayerAvatar val = null;
		float num = float.MaxValue;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player == (Object)null || (Object)(object)player == (Object)(object)_bot.Avatar || !player.isDisabled || (Object)(object)player.playerDeathHead == (Object)null)
			{
				continue;
			}
			PlayerDeathHead playerDeathHead = player.playerDeathHead;
			if (!playerDeathHead.triggered || playerDeathHead.inTruck || playerDeathHead.inExtractionPoint || !Available(playerDeathHead.physGrabObject) || InSomeCart(playerDeathHead.physGrabObject) || !_thingPos.TryGetValue(playerDeathHead.physGrabObject, out var value))
			{
				continue;
			}
			float num2 = Vector3.Distance(value, _bot.Position);
			if (!(num2 > 80f) && OnNavmesh(value) && SafeSpot(value))
			{
				float num3 = num2 + (BotRegistry.IsBot(player) ? 40f : 0f);
				if (num3 < num)
				{
					num = num3;
					val = player;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		Vector3 centerPoint = val.playerDeathHead.physGrabObject.centerPoint;
		ExtractionPoint val2 = ReviveExtraction(centerPoint);
		StartCarry(Job.Revive, val.playerDeathHead.physGrabObject, HeadCart(centerPoint, val2));
		_reviveOf = val;
		_reviveEP = val2;
		Plugin.Log.LogInfo((object)(_bot.Name + " is taking " + val.playerName + "'s head to " + (((Object)(object)_cart != (Object)null) ? "the cart" : (((Object)(object)_reviveEP != (Object)null) ? "an extraction point" : "someone (no extraction point it can use)"))));
		Say(Line("revivestart", null, null, null, null, val.playerName), 1, "revive:" + val.playerName, 60f);
		return true;
	}

	private bool InSomeCart(PhysGrabObject o)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		foreach (PhysGrabCart allCart in AllCarts)
		{
			if ((Object)(object)allCart != (Object)null && (allCart.itemsInCart.Contains(o) || InCartBox(allCart, o.centerPoint) || AboveCart(allCart, o.centerPoint)))
			{
				return true;
			}
		}
		foreach (PhysGrabCart cart in Carts)
		{
			if ((Object)(object)cart != (Object)null && (cart.itemsInCart.Contains(o) || InCartBox(cart, o.centerPoint)))
			{
				return true;
			}
		}
		return false;
	}

	private PhysGrabCart HeadCart(Vector3 head, ExtractionPoint ep)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsUseCarts.Value || (Object)(object)RoundDirector.instance == (Object)null || RoundDirector.instance.allExtractionPointsCompleted)
		{
			return null;
		}
		Vector3 val = (((Object)(object)ep != (Object)null) ? ExtractionSpot(ep) : Vector3.zero);
		float num = (((Object)(object)ep != (Object)null) ? Vector3.Distance(head, val) : float.MaxValue);
		if (num < 25f)
		{
			return null;
		}
		PhysGrabCart result = null;
		float num2 = float.MaxValue;
		foreach (PhysGrabCart cart in Carts)
		{
			if (!((Object)(object)cart == (Object)null) && !((Object)(object)cart.physGrabObject == (Object)null) && !((Object)(object)cart.inCart == (Object)null) && !HumanHolds(cart.physGrabObject) && (!((Object)(object)cart.physGrabObject.roomVolumeCheck != (Object)null) || !cart.physGrabObject.roomVolumeCheck.inExtractionPoint) && _thingPos.TryGetValue(cart.physGrabObject, out var value) && !(IdleFor(cart) < 60f) && (!((Object)(object)ep != (Object)null) || !(Vector3.Distance(value, val) < 15f)))
			{
				float num3 = Vector3.Distance(value, head);
				if (num3 < 12f && num3 + 8f < num && num3 < num2 && OnNavmesh(value) && SafeSpot(value))
				{
					num2 = num3;
					result = cart;
				}
			}
		}
		return result;
	}

	private static string CartHasHead(PhysGrabCart c)
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)c == (Object)null || (Object)(object)GameDirector.instance == (Object)null)
		{
			return null;
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && player.isDisabled && !((Object)(object)player.playerDeathHead == (Object)null) && player.playerDeathHead.triggered)
			{
				PhysGrabObject physGrabObject = player.playerDeathHead.physGrabObject;
				if ((Object)(object)physGrabObject != (Object)null && (c.itemsInCart.Contains(physGrabObject) || InCartBox(c, physGrabObject.centerPoint)))
				{
					return player.playerName;
				}
			}
		}
		return null;
	}

	private static bool AboveCart(PhysGrabCart c, Vector3 p)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)c.inCart == (Object)null)
		{
			return false;
		}
		Vector3 val = c.inCart.InverseTransformPoint(p);
		if (Mathf.Abs(val.x) <= 0.6f && Mathf.Abs(val.z) <= 0.6f && val.y > 0f)
		{
			return val.y < 3f;
		}
		return false;
	}

	private static bool InCartBox(PhysGrabCart c, Vector3 p)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)c.inCart == (Object)null)
		{
			return false;
		}
		Vector3 val = c.inCart.InverseTransformPoint(p);
		if (Mathf.Abs(val.x) <= 0.5f && Mathf.Abs(val.y) <= 0.6f)
		{
			return Mathf.Abs(val.z) <= 0.5f;
		}
		return false;
	}

	private bool HumanHolds(PhysGrabObject o)
	{
		foreach (PhysGrabber item in o.playerGrabbing)
		{
			if ((Object)(object)item != (Object)null && !BotRegistry.IsBot(item.playerAvatar))
			{
				return true;
			}
		}
		return false;
	}

	private PhysGrabCart KnownCart()
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabCart result = null;
		float num = float.MaxValue;
		foreach (PhysGrabCart cart in Carts)
		{
			if (!((Object)(object)cart == (Object)null) && !((Object)(object)cart.physGrabObject == (Object)null) && !((Object)(object)cart.inCart == (Object)null) && (!((Object)(object)cart.physGrabObject.roomVolumeCheck != (Object)null) || !cart.physGrabObject.roomVolumeCheck.inExtractionPoint) && _thingPos.TryGetValue(cart.physGrabObject, out var value))
			{
				float num2 = Vector3.Distance(value, _bot.Position);
				if (num2 < 60f && num2 < num)
				{
					num = num2;
					result = cart;
				}
			}
		}
		return result;
	}

	private Vector3? BaseSpot()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabCart val = KnownCart();
		if ((Object)(object)val != (Object)null && _thingPos.TryGetValue(val.physGrabObject, out var value))
		{
			return value;
		}
		if ((Object)(object)TruckSafetySpawnPoint.instance != (Object)null)
		{
			return ((Component)TruckSafetySpawnPoint.instance).transform.position;
		}
		return null;
	}

	private PhysGrabCart CartFor(Vector3 item, Vector3 extraction)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabCart result = null;
		float num = float.MaxValue;
		foreach (PhysGrabCart cart in Carts)
		{
			if (!((Object)(object)cart == (Object)null) && !((Object)(object)cart.physGrabObject == (Object)null) && !((Object)(object)cart.inCart == (Object)null) && !HumanHolds(cart.physGrabObject) && (!((Object)(object)cart.physGrabObject.roomVolumeCheck != (Object)null) || !cart.physGrabObject.roomVolumeCheck.inExtractionPoint))
			{
				float num2 = Vector3.Distance(cart.inCart.position, item);
				if (num2 < 10f && Vector3.Distance(item, extraction) > 35f && num2 < 0.5f * Vector3.Distance(item, extraction) && num2 < num && OnNavmesh(((Component)cart).transform.position))
				{
					num = num2;
					result = cart;
				}
			}
		}
		return result;
	}

	private bool TryStartHaul()
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		ExtractionPoint val = ActiveExtraction();
		if (!SemiFunc.RunIsLevel() || (Object)(object)ValuableDirector.instance == (Object)null)
		{
			return false;
		}
		PhysGrabCart val2 = (((Object)(object)val == (Object)null) ? KnownCart() : null);
		if ((Object)(object)val == (Object)null && (Object)(object)val2 == (Object)null)
		{
			return false;
		}
		Vector3 val3 = (((Object)(object)val != (Object)null) ? ExtractionSpot(val) : val2.inCart.position);
		bool flag = BotRegistry.Bots.Count >= 2;
		PhysGrabObject val4 = null;
		float num = float.MaxValue;
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if ((Object)(object)valuable == (Object)null || !Seen.Contains(valuable))
			{
				continue;
			}
			PhysGrabObject physGrabObject = valuable.physGrabObject;
			if (!Available(physGrabObject) || physGrabObject.rb.isKinematic)
			{
				continue;
			}
			int num2 = HelpersNeeded(physGrabObject, Plugin.MaxCarryMass.Value);
			if ((num2 > 0 && (!flag || num2 >= BotRegistry.Bots.Count || physGrabObject.rb.mass > Plugin.MaxCarryMass.Value * 2.5f)) || ((Object)(object)physGrabObject.roomVolumeCheck != (Object)null && (physGrabObject.roomVolumeCheck.inExtractionPoint || physGrabObject.roomVolumeCheck.inTruck)) || !Recall(physGrabObject, out var pos))
			{
				continue;
			}
			if (_driver == this)
			{
				PhysGrabCart val5 = NearestKnownCart();
				if ((Object)(object)val5 != (Object)null && _thingPos.TryGetValue(val5.physGrabObject, out var value) && Vector3.Distance(pos, value) > 20f)
				{
					continue;
				}
			}
			float num3 = Vector3.Distance(pos, _bot.Position);
			if (!(num3 > 60f) && OnNavmesh(pos) && SafeSpot(pos) && !InSomeCart(physGrabObject))
			{
				float num4 = num3 + 0.5f * Vector3.Distance(pos, val3) - Mathf.Min(valuable.dollarValueCurrent / 500f * Mathf.Lerp(0.4f, 2.5f, P.Greed), Mathf.Lerp(8f, 30f, P.Greed));
				if (num4 < num)
				{
					num = num4;
					val4 = physGrabObject;
				}
			}
		}
		if ((Object)(object)val4 == (Object)null)
		{
			return false;
		}
		StartCarry(Job.Haul, val4, ((Object)(object)val == (Object)null) ? val2 : (Plugin.BotsUseCarts.Value ? CartFor(val4.centerPoint, val3) : null));
		Plugin.Log.LogInfo((object)(_bot.Name + " is hauling " + ((Object)val4).name + (((Object)(object)_cart != (Object)null) ? " to the cart" : "")));
		OnHaulStart(val4);
		return true;
	}

	private bool TryStartCart()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsUseCarts.Value || !SemiFunc.RunIsLevel())
		{
			return false;
		}
		ExtractionPoint val = ActiveExtraction();
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		Vector3 val2 = ExtractionSpot(val);
		foreach (PhysGrabCart cart in Carts)
		{
			if ((Object)(object)cart == (Object)null || (Object)(object)cart.physGrabObject == (Object)null || (Object)(object)cart.inCart == (Object)null || !Available(cart.physGrabObject) || ((Object)(object)cart.physGrabObject.roomVolumeCheck != (Object)null && cart.physGrabObject.roomVolumeCheck.inExtractionPoint) || PersonNear(((Component)cart).transform.position, 5f) || (cart.haulCurrent <= 0 && cart.itemsInCartCount == 0 && CartHasHead(cart) == null) || Vector3.Distance(((Component)cart).transform.position, _bot.Position) > 40f || Vector3.Distance(((Component)cart).transform.position, val2) < 4f)
			{
				continue;
			}
			bool flag = false;
			foreach (KeyValuePair<PhysGrabObject, BotBrain> claim in Claims)
			{
				if (claim.Value != this && (Object)(object)claim.Value._cart == (Object)(object)cart)
				{
					flag = true;
				}
			}
			if (flag)
			{
				continue;
			}
			bool flag2 = false;
			foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
			{
				if (!((Object)(object)valuable == (Object)null) && Seen.Contains(valuable) && Available(valuable.physGrabObject) && !InSomeCart(valuable.physGrabObject) && valuable.physGrabObject.rb.mass <= Plugin.MaxCarryMass.Value && _thingPos.TryGetValue(valuable.physGrabObject, out var value) && Vector3.Distance(value, ((Component)cart).transform.position) < 12f)
				{
					flag2 = true;
					break;
				}
			}
			string text = CartHasHead(cart);
			if ((!flag2 || cart.itemsInCartCount >= 5 || text != null) && (_driver == null || _driver == this))
			{
				_job = Job.Cart;
				_pushing = cart;
				_cartGoal = CartGoal.Extraction;
				_carry = Carry.Approach;
				_jobStarted = Time.time;
				Claims[cart.physGrabObject] = this;
				_corners.Clear();
				ResetProgress();
				Plugin.Log.LogInfo((object)($"{_bot.Name} is pushing the cart to extraction (${cart.haulCurrent})" + ((text != null) ? (" with " + text + "'s head in it") : "")));
				return true;
			}
		}
		return false;
	}

	private bool TryStartActivate()
	{
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		RoundDirector instance = RoundDirector.instance;
		if (!SemiFunc.RunIsLevel() || (Object)(object)instance == (Object)null || instance.extractionPointActive || instance.allExtractionPointsCompleted)
		{
			return false;
		}
		if (_activator != null && _activator != this)
		{
			return false;
		}
		bool flag = false;
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if ((Object)(object)valuable != (Object)null && Seen.Contains(valuable) && (Object)(object)valuable.physGrabObject != (Object)null && !valuable.physGrabObject.dead)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		ExtractionPoint val = null;
		float num = float.MaxValue;
		foreach (GameObject extractionPoint in instance.extractionPointList)
		{
			ExtractionPoint val2 = (((Object)(object)extractionPoint != (Object)null) ? extractionPoint.GetComponent<ExtractionPoint>() : null);
			if (!((Object)(object)val2 == (Object)null) && !val2.isLocked && val2.StateIs((ExtractionPoint.State)1))
			{
				float num2 = Vector3.Distance(((Component)val2).transform.position, _bot.Position);
				if (num2 < num && OnNavmesh(((Component)val2).transform.position) && SafeSpot(((Component)val2).transform.position))
				{
					num = num2;
					val = val2;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		_job = Job.Activate;
		_activateTarget = val;
		_activator = this;
		_jobStarted = Time.time;
		Plugin.Log.LogInfo((object)(_bot.Name + " is going to turn on an extraction point"));
		Say(Line("activate"), 1, "activate", 60f);
		return true;
	}

	private void StartCarry(Job job, PhysGrabObject target, PhysGrabCart cart)
	{
		_haulCheckAt = 0f;
		_haulMoved = true;
		_helpAskedAt = Time.time;
		_job = job;
		_target = target;
		_cart = cart;
		_carry = Carry.Approach;
		_carryProblemSince = -1f;
		_jobStarted = Time.time;
		_escortHaul = false;
		_wedgedCount = 0;
		_itemBlocked = 0f;
		_approachFrom = null;
		Claims[target] = this;
		_corners.Clear();
		ResetProgress();
	}

	private static ExtractionPoint ActiveExtraction()
	{
		RoundDirector instance = RoundDirector.instance;
		if ((Object)(object)instance == (Object)null || !instance.extractionPointActive || (Object)(object)instance.extractionPointCurrent == (Object)null)
		{
			return null;
		}
		ExtractionPoint extractionPointCurrent = instance.extractionPointCurrent;
		if (!extractionPointCurrent.StateIs((ExtractionPoint.State)2) && !extractionPointCurrent.StateIs((ExtractionPoint.State)4))
		{
			return null;
		}
		return extractionPointCurrent;
	}

	private static Vector3 ExtractionSpot(ExtractionPoint ep)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)ep.extractionArea != (Object)null))
		{
			return ((Component)ep).transform.position;
		}
		return ep.extractionArea.transform.position;
	}

	private void LookForValuables()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _seeAt || (Object)(object)ValuableDirector.instance == (Object)null)
		{
			return;
		}
		_seeAt = Time.time + 0.7f;
		Vector3 val = Eye();
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if (!((Object)(object)valuable == (Object)null) && !((Object)(object)valuable.physGrabObject == (Object)null))
			{
				Vector3 centerPoint = valuable.physGrabObject.centerPoint;
				Vector3 val2 = centerPoint - val;
				if (!(val2.sqrMagnitude > 196f) && InView(centerPoint, 65f, 3f) && ClearLine(val, centerPoint, valuable.physGrabObject, null))
				{
					Seen.Add(valuable);
					_thingPos[valuable.physGrabObject] = centerPoint;
				}
			}
		}
	}

	private Vector3 DoFollow(out bool sprint, PlayerAvatar forced = null)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_0284: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		sprint = false;
		_leader = forced ?? PickLeader();
		if ((Object)(object)_leader == (Object)null)
		{
			_walking = false;
			if (SemiFunc.RunIsLevel() && (Object)(object)TruckSafetySpawnPoint.instance != (Object)null)
			{
				Vector3 position = ((Component)TruckSafetySpawnPoint.instance).transform.position;
				Vector3 val = Flat(position - _bot.Position);
				if (val.magnitude > 4f)
				{
					return Steer(_bot.Position, position) * 3.2f;
				}
			}
			return Vector3.zero;
		}
		Vector3 position2 = _bot.Position;
		Vector3? val2 = PersonPos(_leader);
		if ((Object)(object)forced != (Object)null && !val2.HasValue)
		{
			_walking = false;
			return Vector3.zero;
		}
		Vector3 val3 = val2 ?? ((Component)_leader).transform.position;
		Vector3 val4 = Flat(val3 - position2);
		float magnitude = val4.magnitude;
		if (!SeesPerson(_leader) && magnitude < 2f && (Object)(object)forced == (Object)null)
		{
			Say(Line("lost", null, null, null, null, _leader.playerName), 0, "lost:" + _bot.Name, 90f);
			Plugin.Log.LogInfo((object)(_bot.Name + " lost track of " + _leader.playerName));
			LostPerson(_leader);
			_leader = null;
			_walking = false;
			return Vector3.zero;
		}
		if (!_walking && magnitude > _followDist + 1.6f)
		{
			_walking = true;
		}
		else if (_walking && magnitude < _followDist)
		{
			_walking = false;
		}
		if (!_sprinting && magnitude > 9f)
		{
			_sprinting = true;
		}
		else if (_sprinting && magnitude < 5.5f)
		{
			_sprinting = false;
		}
		if (!_walking)
		{
			return Vector3.zero;
		}
		Vector3 val5 = Vector3.Cross(Vector3.up, val4.normalized);
		Vector3 goal = val3 + val5 * _sideBias - val4.normalized * 0.5f;
		sprint = _sprinting;
		return Steer(position2, goal) * (_sprinting ? 5.6f : 3.2f);
	}

	private Vector3 DoFlee(out bool sprint)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		sprint = _fleeRun && SprintWorthIt();
		TryDive();
		Vector3 val = Flat(_fleeTo - _bot.Position);
		if (val.magnitude < 1f)
		{
			return Vector3.zero;
		}
		return Steer(_bot.Position, _fleeTo) * (_fleeRun ? 5.6f : 3.2f);
	}

	private bool SprintWorthIt()
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (_danger == null)
		{
			return true;
		}
		float num = Vector3.Distance(_danger.Position, _bot.Position);
		if (num < 5f)
		{
			return true;
		}
		if ((Object)(object)_body != (Object)null && _body.EnergyMax > 0f && _body.Energy < _body.EnergyMax * 0.35f)
		{
			return false;
		}
		bool flag = _danger.Aggressive && (Object)(object)_danger.Target == (Object)(object)_bot.Avatar;
		return num < (flag ? 11f : 7f);
	}

	private Vector3 DoActivate()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		ExtractionPoint activateTarget = _activateTarget;
		if ((Object)(object)activateTarget == (Object)null)
		{
			DropJob("DoActivate", 1505);
			return Vector3.zero;
		}
		Vector3 position = ((Component)activateTarget).transform.position;
		Vector3 val = Flat(position - _bot.Position);
		if (val.magnitude < 2.5f && Mathf.Abs(position.y - _bot.Position.y) < 2.5f)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " turned on an extraction point"));
			activateTarget.OnClick();
			DropJob("DoActivate", 1513);
			return Vector3.zero;
		}
		return Steer(_bot.Position, position) * 3.2f;
	}

	private Vector3 DoCarry(out Vector3? lookAt)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_055d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0578: Unknown result type (might be due to invalid IL or missing references)
		//IL_0587: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0707: Unknown result type (might be due to invalid IL or missing references)
		//IL_070c: Unknown result type (might be due to invalid IL or missing references)
		//IL_070e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0710: Unknown result type (might be due to invalid IL or missing references)
		//IL_0711: Unknown result type (might be due to invalid IL or missing references)
		//IL_0716: Unknown result type (might be due to invalid IL or missing references)
		//IL_071b: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_07e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0747: Unknown result type (might be due to invalid IL or missing references)
		//IL_060b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d6c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0671: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d9d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0da7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0db1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d7b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c61: Unknown result type (might be due to invalid IL or missing references)
		//IL_10d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_10e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0686: Unknown result type (might be due to invalid IL or missing references)
		//IL_0435: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e65: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e79: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e5d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e36: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ddd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c6f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c71: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c73: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c74: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c79: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c7e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0fc3: Unknown result type (might be due to invalid IL or missing references)
		//IL_049a: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0453: Unknown result type (might be due to invalid IL or missing references)
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_045e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0416: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e89: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e90: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c8e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c95: Unknown result type (might be due to invalid IL or missing references)
		//IL_0877: Unknown result type (might be due to invalid IL or missing references)
		//IL_1163: Unknown result type (might be due to invalid IL or missing references)
		//IL_1168: Unknown result type (might be due to invalid IL or missing references)
		//IL_1120: Unknown result type (might be due to invalid IL or missing references)
		//IL_1127: Unknown result type (might be due to invalid IL or missing references)
		//IL_10cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_050a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0511: Unknown result type (might be due to invalid IL or missing references)
		//IL_047d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0484: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Unknown result type (might be due to invalid IL or missing references)
		//IL_0493: Unknown result type (might be due to invalid IL or missing references)
		//IL_034c: Unknown result type (might be due to invalid IL or missing references)
		//IL_039c: Unknown result type (might be due to invalid IL or missing references)
		//IL_089c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d56: Unknown result type (might be due to invalid IL or missing references)
		//IL_1004: Unknown result type (might be due to invalid IL or missing references)
		//IL_1006: Unknown result type (might be due to invalid IL or missing references)
		//IL_1007: Unknown result type (might be due to invalid IL or missing references)
		//IL_100c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0504: Unknown result type (might be due to invalid IL or missing references)
		//IL_0afb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b00: Unknown result type (might be due to invalid IL or missing references)
		//IL_09cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_09d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_09da: Unknown result type (might be due to invalid IL or missing references)
		//IL_09df: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_08cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_1068: Unknown result type (might be due to invalid IL or missing references)
		//IL_1035: Unknown result type (might be due to invalid IL or missing references)
		//IL_1022: Unknown result type (might be due to invalid IL or missing references)
		//IL_1023: Unknown result type (might be due to invalid IL or missing references)
		//IL_1025: Unknown result type (might be due to invalid IL or missing references)
		//IL_102f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f99: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b2c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b2e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b2f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b34: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b39: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b26: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_09f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_08db: Unknown result type (might be due to invalid IL or missing references)
		//IL_08dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_08de: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_08e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0bf5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0aeb: Unknown result type (might be due to invalid IL or missing references)
		//IL_12f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_12fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_131e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1323: Unknown result type (might be due to invalid IL or missing references)
		//IL_1324: Unknown result type (might be due to invalid IL or missing references)
		//IL_1329: Unknown result type (might be due to invalid IL or missing references)
		//IL_132e: Unknown result type (might be due to invalid IL or missing references)
		//IL_1360: Unknown result type (might be due to invalid IL or missing references)
		//IL_133f: Unknown result type (might be due to invalid IL or missing references)
		//IL_1341: Unknown result type (might be due to invalid IL or missing references)
		//IL_1346: Unknown result type (might be due to invalid IL or missing references)
		//IL_1350: Unknown result type (might be due to invalid IL or missing references)
		//IL_135a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1477: Unknown result type (might be due to invalid IL or missing references)
		//IL_1478: Unknown result type (might be due to invalid IL or missing references)
		//IL_147a: Unknown result type (might be due to invalid IL or missing references)
		//IL_1484: Unknown result type (might be due to invalid IL or missing references)
		//IL_148b: Unknown result type (might be due to invalid IL or missing references)
		//IL_1492: Unknown result type (might be due to invalid IL or missing references)
		//IL_14b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_13e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_13ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_14c5: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		PhysGrabObject target = _target;
		if ((Object)(object)target == (Object)null || target.dead || (_job == Job.Revive && ((Object)(object)_reviveOf == (Object)null || !_reviveOf.isDisabled)))
		{
			DropJob("DoCarry", 1525);
			return Vector3.zero;
		}
		if ((Object)(object)_cart != (Object)null && (Object)(object)ActiveExtraction() != (Object)null && ((HumanHolds(_cart.physGrabObject) && !_escortHaul) || ((Object)(object)_cart.physGrabObject.roomVolumeCheck != (Object)null && _cart.physGrabObject.roomVolumeCheck.inExtractionPoint)))
		{
			_cart = null;
		}
		Vector3 position = _bot.Position;
		Vector3 val = Eye();
		Vector3 val2;
		if (_carry == Carry.Approach)
		{
			if (!Recall(target, out var pos))
			{
				DropJob("DoCarry", 1537);
				return Vector3.zero;
			}
			lookAt = pos;
			if (Vector3.Distance(pos, val) < 2.5f && !CanSee(target.centerPoint, 22f, ((Component)target).transform) && Vector3.Distance(target.centerPoint, pos) > 1f)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + ": " + ((Object)target).name + " isn't where it was"));
				Forget(target);
				DropJob("DoCarry", 1546);
				return Vector3.zero;
			}
			pos = (CanSee(target.centerPoint, 22f, ((Component)target).transform) ? target.centerPoint : pos);
			val2 = pos - val;
			bool flag = val2.magnitude <= 3.5f;
			bool flag2 = flag && BotGrab.CanGrab(_bot, target, val, null, 3.5f);
			if (flag && !flag2 && _posture == 0 && BotGrab.CanGrab(_bot, target, _bot.Position + Vector3.up * 1f, null, 3.5f))
			{
				_grabCrouchUntil = Time.time + 2f;
				return Vector3.zero;
			}
			val2 = Flat(pos - position);
			bool flag3 = val2.magnitude < 1.5f || (_blockedSince > 0f && Time.time - _blockedSince > 1f);
			if (flag && !flag2 && !flag3 && !_approachFrom.HasValue)
			{
				return Steer(position, pos) * 3.2f;
			}
			if (flag && !flag2 && !_approachFrom.HasValue)
			{
				_approachFrom = ViewSpot(target, pos);
				if (!_approachFrom.HasValue && Time.time - _jobStarted > 8f)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " can't get a clear grab on " + ((Object)target).name + " (" + BotGrab.WhyNot(_bot, target, val, 3.5f) + ")"));
					Failed(target, 240f);
					Blacklist[target] = Time.time + 240f;
					DropJob("DoCarry", 1570);
					return Vector3.zero;
				}
			}
			if (flag2 && !NeedsOtherSide(target, pos, val))
			{
				if (BotGrab.Grab(_bot, target, val, null))
				{
					_carry = Carry.Hold;
					_carryProblemSince = -1f;
					_grabbedAt = Time.time;
					_corners.Clear();
					return Vector3.zero;
				}
				Blacklist[target] = Time.time + 60f;
				DropJob("DoCarry", 1585);
				return Vector3.zero;
			}
			if (!Available(target))
			{
				DropJob("DoCarry", 1590);
				return Vector3.zero;
			}
			if (_approachFrom.HasValue)
			{
				val2 = Flat(_approachFrom.Value - position);
				if (!(val2.magnitude < 0.6f))
				{
					return Steer(position, _approachFrom.Value) * 3.2f;
				}
				_approachFrom = null;
			}
			Vector3 val3 = Steer(position, pos);
			if (NoWayThere && Time.time - _jobStarted > 2f && !flag)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " can't find a way to " + ((Object)target).name));
				Failed(target, 90f);
				DropJob("DoCarry", 1606);
				return Vector3.zero;
			}
			return val3 * 3.2f;
		}
		if (_bot.HeldView == 0 || (Object)(object)BotGrab.Held(_bot) != (Object)(object)target)
		{
			_carry = Carry.Approach;
			return Vector3.zero;
		}
		if (HumanHolds(target))
		{
			DropJob("DoCarry", 1619);
			return Vector3.zero;
		}
		if (Time.time - _grabbedAt < 1.2f)
		{
			lookAt = target.centerPoint;
			return Vector3.zero;
		}
		bool flag4 = IsHeavy(target, HeavyLimit) && target.playerGrabbing.Count < 1 + HelpersNeeded(target, HeavyLimit);
		if (flag4 && Time.time - _grabbedAt < ((HelperCount(target) > 0) ? 18f : 6f))
		{
			lookAt = target.centerPoint;
			if (Time.time - _grabbedAt > 2.5f)
			{
				OnNeedHelp(target);
			}
			return Vector3.zero;
		}
		if (_job == Job.Revive && (Object)(object)_cart == (Object)null && Time.time >= _headCartCheckAt && (Object)(object)RoundDirector.instance != (Object)null && !RoundDirector.instance.allExtractionPointsCompleted)
		{
			_headCartCheckAt = Time.time + 2f;
			_cart = HeadCart(target.centerPoint, ((Object)(object)_reviveEP != (Object)null) ? _reviveEP : ReviveExtraction(target.centerPoint));
			if ((Object)(object)_cart != (Object)null)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " is putting " + _reviveOf.playerName + "'s head in the cart instead"));
			}
		}
		Vector3 dest;
		bool arrived;
		if (_job == Job.Revive && (Object)(object)_cart != (Object)null)
		{
			dest = _cart.inCart.position;
			val2 = Flat(dest - position);
			arrived = val2.magnitude < 2f;
			if (_cart.itemsInCart.Contains(target) || InCartBox(_cart, target.centerPoint))
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " put " + _reviveOf.playerName + "'s head in the cart"));
				BotSave.Add(_bot.SteamId, "rescues");
				Say(Line("headincart", null, null, null, null, _reviveOf.playerName), 1);
				ReleaseHeld("DoCarry", 1660);
				DropJob("DoCarry", 1661);
				return Vector3.zero;
			}
		}
		else if (_job == Job.Revive)
		{
			PlayerDeathHead playerDeathHead = _reviveOf.playerDeathHead;
			if ((Object)(object)_reviveEP != (Object)null && !_reviveEP.StateIs((ExtractionPoint.State)1) && !_reviveEP.StateIs((ExtractionPoint.State)2) && !_reviveEP.StateIs((ExtractionPoint.State)4))
			{
				_reviveEP = null;
			}
			RoundDirector instance = RoundDirector.instance;
			bool flag5 = (Object)(object)instance == (Object)null || instance.allExtractionPointsCompleted;
			if ((Object)(object)_reviveEP == (Object)null && !flag5)
			{
				_reviveEP = ReviveExtraction(target.centerPoint);
			}
			PlayerAvatar val4 = (((Object)(object)_reviveEP == (Object)null && !flag5) ? NearestPerson(position) : null);
			if ((Object)(object)val4 != (Object)null)
			{
				dest = PersonPos(val4) ?? ((Component)val4).transform.position;
				val2 = Flat(dest - position);
				arrived = val2.magnitude < 2.2f;
				if (arrived)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " brought " + _reviveOf.playerName + "'s head to " + val4.playerName));
					Say(Line("headhere", null, null, null, null, _reviveOf.playerName), 1);
					ReleaseHeld("DoCarry", 1685);
					Blacklist[target] = Time.time + 60f;
					DropJob("DoCarry", 1687);
					return Vector3.zero;
				}
			}
			else if ((Object)(object)_reviveEP != (Object)null)
			{
				dest = ExtractionSpot(_reviveEP);
				val2 = Flat(dest - position);
				arrived = val2.magnitude < 2.2f && Mathf.Abs(dest.y - position.y) < 2f;
				if ((Object)(object)playerDeathHead != (Object)null && playerDeathHead.inExtractionPoint)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " left " + _reviveOf.playerName + "'s head in an extraction point"));
					BotSave.Add(_bot.SteamId, "rescues");
					ReleaseHeld("DoCarry", 1700);
					RoundDirector instance2 = RoundDirector.instance;
					if ((Object)(object)instance2 != (Object)null && !instance2.extractionPointActive && _reviveEP.StateIs((ExtractionPoint.State)1) && !_reviveEP.isLocked)
					{
						Plugin.Log.LogInfo((object)(_bot.Name + " turned that extraction point on"));
						_reviveEP.OnClick();
					}
					DropJob("DoCarry", 1708);
					return Vector3.zero;
				}
			}
			else
			{
				dest = ((Component)TruckSafetySpawnPoint.instance).transform.position;
				if (flag5)
				{
					ReleaseHeld("DoCarry", 1718);
					DropJob("DoCarry", 1719);
					return Vector3.zero;
				}
				val2 = Flat(dest - position);
				arrived = val2.magnitude < 3f;
				if ((Object)(object)playerDeathHead != (Object)null && playerDeathHead.inTruck)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " got " + _reviveOf.playerName + "'s head into the truck"));
					BotSave.Add(_bot.SteamId, "rescues");
					Say(Line("headintruck", null, null, null, null, _reviveOf.playerName), 1);
					ReleaseHeld("DoCarry", 1728);
					DropJob("DoCarry", 1729);
					return Vector3.zero;
				}
			}
		}
		else if ((Object)(object)_bringTo != (Object)null)
		{
			if (_bringTo.isDisabled)
			{
				ReleaseHeld("DoCarry", 1738);
				DropJob("DoCarry", 1739);
				return Vector3.zero;
			}
			dest = PersonPos(_bringTo) ?? ((Component)_bringTo).transform.position;
			val2 = Flat(dest - position);
			arrived = val2.magnitude < 2.2f && Mathf.Abs(dest.y - position.y) < 2f;
			if (arrived)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " brought " + ((Object)target).name + " to " + _bringTo.playerName));
				Say(Line("bringdone"), 1);
				ReleaseHeld("DoCarry", 1748);
				Blacklist[target] = Time.time + 30f;
				DropJob("DoCarry", 1750);
				return Vector3.zero;
			}
		}
		else if ((Object)(object)_shopItem != (Object)null)
		{
			if (!ShopCarry(target, position, out dest, out arrived))
			{
				return Vector3.zero;
			}
		}
		else if ((Object)(object)_cart != (Object)null)
		{
			dest = _cart.inCart.position;
			val2 = Flat(dest - position);
			arrived = val2.magnitude < 2f;
			if (_cart.itemsInCart.Contains(target) || InCartBox(_cart, target.centerPoint))
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " put " + ((Object)target).name + " in the cart"));
				ReleaseHeld("DoCarry", 1766);
				DropJob("DoCarry", 1767);
				return Vector3.zero;
			}
		}
		else
		{
			ExtractionPoint val5 = ActiveExtraction();
			if ((Object)(object)val5 == (Object)null)
			{
				DropJob("DoCarry", 1776);
				return Vector3.zero;
			}
			dest = ExtractionSpot(val5);
			val2 = Flat(dest - position);
			arrived = val2.magnitude < 2.2f && Mathf.Abs(dest.y - position.y) < 2f;
			if ((Object)(object)target.roomVolumeCheck != (Object)null && target.roomVolumeCheck.inExtractionPoint)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " delivered " + ((Object)target).name));
				BotSave.Add(_bot.SteamId, "delivered");
				ValuableObject component = ((Component)target).GetComponent<ValuableObject>();
				if ((Object)(object)component != (Object)null)
				{
					BotSave.Add(_bot.SteamId, "money", Mathf.RoundToInt(component.dollarValueCurrent));
				}
				Say(Line("delivered", null, null, ItemName(target)), 0, "delivered", 20f);
				ReleaseHeld("DoCarry", 1789);
				Blacklist[target] = Time.time + 9999f;
				DropJob("DoCarry", 1791);
				return Vector3.zero;
			}
		}
		if (arrived)
		{
			if (_carry != Carry.Deliver)
			{
				_carry = Carry.Deliver;
				_deliverStarted = Time.time;
			}
			lookAt = DropPoint();
			if (Time.time - _deliverStarted > 6f)
			{
				if (_job == Job.Revive && Time.time - _deliverStarted < 14f)
				{
					val2 = Flat(dest - position);
					if (val2.magnitude > 0.6f)
					{
						return Steer(position, dest) * 2.2f;
					}
					return Vector3.zero;
				}
				if (_job == Job.Revive && (Object)(object)_reviveOf != (Object)null)
				{
					Say(Line("headleft", null, person: _reviveOf.playerName, where: RoomName(target.centerPoint) ?? "near the truck"), 1);
				}
				ReleaseHeld("DoCarry", 1816);
				Blacklist[target] = Time.time + 20f;
				DropJob("DoCarry", 1818);
			}
			return Vector3.zero;
		}
		_carry = Carry.Hold;
		float num = Vector3.Distance(target.centerPoint, HoldPoint(withLift: false));
		PhysGrabber physGrabber = _bot.Avatar.physGrabber;
		if ((Object)(object)physGrabber != (Object)null && (Object)(object)physGrabber.physGrabPoint != (Object)null)
		{
			num = Mathf.Min(num, Vector3.Distance(physGrabber.physGrabPoint.position, HoldPoint(withLift: false)));
		}
		bool flag6 = target.playerGrabbing.Count > 1;
		float num2;
		if (!((Object)(object)target.rb != (Object)null))
		{
			num2 = 0f;
		}
		else
		{
			val2 = target.rb.velocity;
			num2 = val2.magnitude;
		}
		float num3 = num2;
		if (num > (flag6 ? 2.4f : 1.6f) && num3 < 0.25f && Time.time >= _snagGraceUntil)
		{
			if (_snagSince < 0f)
			{
				_snagSince = Time.time;
			}
		}
		else if (num < 1.3f || num3 > 0.5f)
		{
			_snagSince = -1f;
		}
		bool flag7 = _snagSince > 0f && Time.time - _snagSince > 0.8f;
		if (flag7 && Time.time - _snagSince > 3.5f)
		{
			_snagSince = -1f;
			_snagGraceUntil = Time.time + 2.5f;
			flag7 = false;
		}
		if (flag7 || num > 3.5f)
		{
			if (_carryProblemSince < 0f)
			{
				_carryProblemSince = Time.time;
			}
			if (num > 6f || Time.time - _carryProblemSince > 10f)
			{
				ReleaseHeld("DoCarry", 1861);
				_carry = Carry.Approach;
				_carryProblemSince = -1f;
				if (++_wedgedCount >= 3)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " can't get " + ((Object)target).name + " unstuck"));
					Failed(target, 120f);
					DropJob("DoCarry", 1869);
				}
				return Vector3.zero;
			}
			lookAt = target.centerPoint;
			_liftUntil = Time.time + 1.5f;
			Vector3 val6 = Flat(target.centerPoint - position);
			if (val6.magnitude > 1.2f)
			{
				return Steer(position, target.centerPoint) * 3.2f * 0.5f;
			}
			return Vector3.zero;
		}
		if (num < 3f)
		{
			_carryProblemSince = -1f;
		}
		float num4 = (flag6 ? Mathf.Clamp(1.8f - num * 0.5f, 0.6f, 1f) : Mathf.Clamp(1.6f - num * 0.6f, 0.45f, 1f));
		if (Plugin.GentleSetDown.Value && !flag4 && !flag6 && num > 1.25f)
		{
			val2 = Flat(_bot.Velocity);
			if (num3 < Mathf.Max(0.4f, val2.magnitude * 0.5f))
			{
				num4 = 0f;
			}
		}
		if (flag4 && Time.time - _helpAskedAt > 20f)
		{
			_helpAskedAt = Time.time;
			OnNeedHelp(target);
		}
		float num5 = SafeBumpSpeed(target);
		float num6;
		if (Plugin.GentleSetDown.Value)
		{
			if (num5 < 0.5f)
			{
				num6 = 0.7f;
			}
			else
			{
				num6 = ((num5 < 1f) ? 0.85f : 1f);
			}
		}
		else
		{
			num6 = 1f;
		}
		return Steer(position, dest) * 2.2f * num4 * num6 * Mathf.Lerp(1.15f, 0.8f, P.Careful) * (flag4 ? 0.55f : 1f);
	}

	private Vector3 DoCart(out Vector3? lookAt)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_034d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0257: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0441: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_0447: Unknown result type (might be due to invalid IL or missing references)
		//IL_044c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0451: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0485: Unknown result type (might be due to invalid IL or missing references)
		//IL_0478: Unknown result type (might be due to invalid IL or missing references)
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c9: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		PhysGrabCart pushing = _pushing;
		ExtractionPoint val = ((_cartGoal == CartGoal.Extraction) ? ActiveExtraction() : null);
		if ((Object)(object)pushing == (Object)null || (_cartGoal == CartGoal.Extraction && (Object)(object)val == (Object)null) || HumanHolds(pushing.physGrabObject))
		{
			DropJob("DoCart", 1910);
			return Vector3.zero;
		}
		Vector3 val2 = (((Object)(object)val != (Object)null) ? ExtractionSpot(val) : _cartDest);
		Vector3 position = _bot.Position;
		Vector3 val3;
		if (_cartGoal != CartGoal.Extraction && _bot.HeldView != 0)
		{
			val3 = Flat(((Component)pushing).transform.position - val2);
			if (val3.magnitude < 2.5f && (_cartGoal != CartGoal.OutOfExtraction || !((Object)(object)pushing.physGrabObject.roomVolumeCheck != (Object)null) || !pushing.physGrabObject.roomVolumeCheck.inExtractionPoint))
			{
				CartArrived(pushing);
				return Vector3.zero;
			}
		}
		if (_cartGoal == CartGoal.Extraction && (Object)(object)pushing.physGrabObject.roomVolumeCheck != (Object)null && pushing.physGrabObject.roomVolumeCheck.inExtractionPoint)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " parked the cart in extraction"));
			ReleaseHeld("DoCart", 1926);
			DropJob("DoCart", 1927);
			return Vector3.zero;
		}
		Vector3 val7;
		if (_carry == Carry.Approach)
		{
			Collider val4 = CartHandle(pushing);
			Vector3 val5;
			if (!((Object)(object)val4 != (Object)null))
			{
				val5 = (((Object)(object)pushing.handlePoint != (Object)null) ? pushing.handlePoint.position : ((Component)pushing).transform.position);
			}
			else
			{
				Bounds bounds = val4.bounds;
				val5 = bounds.center;
			}
			Vector3 val6 = val5;
			val7 = HandleStandSpot(pushing, val6);
			lookAt = val6;
			if (_cartApproachAt < _jobStarted)
			{
				_cartApproachAt = Time.time;
			}
			if (Time.time - _cartApproachAt > 15f)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " couldn't reach the cart handle"));
				Blacklist[pushing.physGrabObject] = Time.time + 45f;
				DropJob("DoCart", 1944);
				return Vector3.zero;
			}
			val3 = Flat(val7 - position);
			if (!(val3.magnitude < 0.8f))
			{
				val3 = val6 - Eye();
				if (!(val3.magnitude < 1.6f))
				{
					goto IL_030d;
				}
			}
			if (BotGrab.CanGrab(_bot, pushing.physGrabObject, Eye(), val4, 3.5f))
			{
				if (BotGrab.Grab(_bot, pushing.physGrabObject, Eye(), val4))
				{
					_carry = Carry.Hold;
					_grabbedAt = Time.time;
					_cartApproachAt = -1f;
					_corners.Clear();
					return Vector3.zero;
				}
				DropJob("DoCart", 1957);
				return Vector3.zero;
			}
			goto IL_030d;
		}
		if (_bot.HeldView == 0 || (Object)(object)BotGrab.Held(_bot) != (Object)(object)pushing.physGrabObject)
		{
			_carry = Carry.Approach;
			return Vector3.zero;
		}
		if (Time.time - _grabbedAt < 0.6f && (Object)(object)_bot.Avatar.physGrabber != (Object)null)
		{
			_bot.Avatar.physGrabber.initialPressTimer = 0.1f;
		}
		if (Time.time - _grabbedAt < 1f)
		{
			lookAt = ((Component)pushing).transform.position + Vector3.up * 0.5f;
			return Vector3.zero;
		}
		Vector3 val8 = Flat(val2 - position);
		if (val8.magnitude < 1.2f)
		{
			if (Time.time - _deliverStarted > 5f)
			{
				ReleaseHeld("DoCart", 1984);
				DropJob("DoCart", 1985);
			}
			return Vector3.zero;
		}
		_deliverStarted = Time.time;
		val3 = Flat(((Component)pushing).transform.position - position);
		if (val3.magnitude > 5f)
		{
			ReleaseHeld("DoCart", 1993);
			_carry = Carry.Approach;
			return Vector3.zero;
		}
		if (PersonNear(((Component)pushing).transform.position, 2.2f))
		{
			lookAt = ((Component)pushing).transform.position;
			return Vector3.zero;
		}
		Vector3? val9 = PushAroundMonsters(position, pushing, val2, ref lookAt);
		if (val9.HasValue)
		{
			return val9.Value;
		}
		return DriveCart(pushing, val2, ref lookAt);
		IL_030d:
		return Steer(position, val7) * 3.2f;
	}

	private Vector3 HandleStandSpot(PhysGrabCart c, Vector3 hp)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(hp - ((Component)c).transform.position);
		Vector3 normalized = val.normalized;
		NavMeshHit val3 = default;
		for (int i = 0; i < 7; i++)
		{
			float num = ((i == 0) ? 0f : ((float)((i % 2 == 1) ? 1 : (-1)) * 30f * (float)((i + 1) / 2)));
			Vector3 val2 = Quaternion.Euler(0f, num, 0f) * normalized;
			if (NavMesh.SamplePosition(hp + val2 * 0.9f, out val3, 0.6f, -1))
			{
				Vector3 position = val3.position;
				if (!Physics.Linecast(hp, position + Vector3.up * 1f, GroundMask, (QueryTriggerInteraction)1))
				{
					return position;
				}
			}
		}
		return hp + normalized * 0.9f;
	}

	private bool PersonNear(Vector3 p, float range)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && !((Object)(object)player == (Object)(object)_bot.Avatar) && !player.isDisabled)
			{
				Vector3 val = BotRegistry.Get(player)?.Position ?? ((Component)player).transform.position;
				Vector3 val2 = Flat(val - p);
				if (val2.magnitude < range && Mathf.Abs(val.y - p.y) < 2f)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static Collider CartHandle(PhysGrabCart c)
	{
		PhysGrabObjectGrabArea component = ((Component)c).GetComponent<PhysGrabObjectGrabArea>();
		if ((Object)(object)component == (Object)null)
		{
			return null;
		}
		foreach (PhysGrabObjectGrabArea.GrabArea grabArea in component.grabAreas)
		{
			foreach (Collider grabAreaCollider in grabArea.grabAreaColliders)
			{
				if ((Object)(object)grabAreaCollider != (Object)null)
				{
					return grabAreaCollider;
				}
			}
		}
		return null;
	}

	private static void Dims(int posture, out float radius, out float height, out float step)
	{
		switch (posture)
		{
		case 2:
			radius = 0.24f;
			height = 0.62f;
			step = 0.2f;
			break;
		case 1:
			radius = 0.3f;
			height = 1.15f;
			step = 0.3f;
			break;
		default:
			radius = 0.32f;
			height = 1.7f;
			step = 0.4f;
			break;
		}
	}

	private bool Fits(Vector3 pos, int posture)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		Dims(posture, out var radius, out var height, out var step);
		Vector3 val = pos + Vector3.up * (step + radius);
		Vector3 val2 = pos + Vector3.up * Mathf.Max(height - radius, step + radius);
		int num = Physics.OverlapCapsuleNonAlloc(val, val2, radius - 0.02f, Overlaps, MoveMask, (QueryTriggerInteraction)1);
		for (int i = 0; i < num; i++)
		{
			if (!Ignored(Overlaps[i]) && !((Object)(object)((Component)Overlaps[i]).GetComponentInParent<PhysGrabObject>() != (Object)null))
			{
				return false;
			}
		}
		return true;
	}

	private bool Overhead(Vector3 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		int num = Physics.RaycastNonAlloc(pos + Vector3.up * 0.3f, Vector3.up, Hits, 1.7f, MoveMask, (QueryTriggerInteraction)1);
		for (int i = 0; i < num; i++)
		{
			if (!Ignored(Hits[i].collider) && (Object)(object)((Component)Hits[i].collider).GetComponentInParent<PhysGrabObject>() == (Object)null)
			{
				return true;
			}
		}
		return false;
	}

	private void UpdatePosture(Vector3 desired, bool wantCrouch, bool wantCrawl)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		int num = (wantCrawl ? 2 : (wantCrouch ? 1 : 0));
		Vector3 position = _bot.Position;
		Vector3 val = Flat(desired);
		Vector3 pos = ((val.sqrMagnitude > 0.01f) ? (position + val.normalized * 0.4f) : position);
		int num2 = ((!Fits(position, 0) && Overhead(position)) ? (Fits(position, 1) ? 1 : 2) : 0);
		int num3 = ((!Fits(pos, 0) && Overhead(pos)) ? (Fits(pos, 1) ? 1 : (Fits(pos, 2) ? 2 : (-1))) : 0);
		int num4 = ((num3 < 0) ? num2 : Mathf.Max(num2, num3));
		int num5 = Mathf.Max(num, num4);
		if (num5 < _posture && !Fits(position, num5))
		{
			num5 = _posture;
		}
		_posture = num5;
	}

	private bool FindHideSpot(Vector3 threat, bool allowUnder, out Vector3 spot, out bool under, bool truckOnly = false)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0299: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_0324: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032a: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_0336: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_036d: Unknown result type (might be due to invalid IL or missing references)
		spot = _bot.Position;
		under = false;
		Vector3 position = _bot.Position;
		List<Vector3> list = new List<Vector3>();
		if (allowUnder)
		{
			int num = Physics.OverlapSphereNonAlloc(position + Vector3.up * 0.5f, 9f, Nearby, MoveMask, (QueryTriggerInteraction)1);
			for (int i = 0; i < num; i++)
			{
				Collider val = Nearby[i];
				if (!((Object)(object)val == (Object)null) && !Ignored(val))
				{
					Bounds bounds = val.bounds;
					float num2 = bounds.max.y - position.y;
					if (!(bounds.size.x < 0.6f) && !(bounds.size.z < 0.6f) && !(num2 < 0.55f) && !(num2 > 1.5f) && !(bounds.min.y - position.y > 1.2f))
					{
						list.Add(new Vector3(bounds.center.x, position.y, bounds.center.z));
					}
				}
			}
		}
		NavMeshHit val3 = default;
		for (int j = 0; j < 14; j++)
		{
			Vector2 val2 = Random.insideUnitCircle * 9f;
			if (NavMesh.SamplePosition(position + new Vector3(val2.x, 0f, val2.y), out val3, 2f, -1))
			{
				list.Add(val3.position);
			}
		}
		float num3 = float.MinValue;
		int num4 = GroundMask | MoveMask;
		RaycastHit val4 = default;
		foreach (Vector3 item in list)
		{
			if (!Physics.Raycast(item + Vector3.up * 0.3f, Vector3.down, out val4, 1.5f, GroundMask, (QueryTriggerInteraction)1))
			{
				continue;
			}
			Vector3 point = val4.point;
			if (Mathf.Abs(point.y - position.y) > 1.2f)
			{
				continue;
			}
			bool flag = Physics.Raycast(point + Vector3.up * 0.15f, Vector3.up, 1.3f, MoveMask, (QueryTriggerInteraction)1);
			if ((flag && !allowUnder) || (flag ? (!Fits(point, 2)) : (!Fits(point, 1))))
			{
				continue;
			}
			float num5 = (flag ? 0.35f : 0.7f);
			bool flag2 = Physics.Linecast(threat, point + Vector3.up * num5, num4, (QueryTriggerInteraction)1);
			if (!flag2 && !flag)
			{
				continue;
			}
			float num6 = Vector3.Distance(point, position);
			float num7 = Vector3.Distance(point, threat);
			if (num7 < 3f && !(truckOnly & flag))
			{
				continue;
			}
			if (truckOnly)
			{
				RoomVolume val5 = RoomAt(point);
				if ((Object)(object)val5 == (Object)null || !val5.Truck || !flag)
				{
					continue;
				}
			}
			float num8 = (flag ? 10f : 0f) + (flag2 ? 8f : 0f) - 0.8f * num6 + 0.3f * num7;
			if (Vector3.Dot(Flat(point - position), Flat(threat - position)) > 0f && Vector3.Distance(threat, position) < num6 + 2f)
			{
				num8 -= 15f;
			}
			if (num8 > num3)
			{
				num3 = num8;
				spot = point;
				under = flag;
			}
		}
		return num3 > float.MinValue;
	}

	private Vector3 DoHide(out bool crouch, out Vector3? lookAt)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		crouch = true;
		lookAt = null;
		Vector3 val = Flat(_hideSpot - _bot.Position);
		if (val.magnitude < 0.35f)
		{
			return Vector3.zero;
		}
		return ((val.magnitude < 2.5f) ? val.normalized : Steer(_bot.Position, _hideSpot)) * 1.6f;
	}

	private void WhileTumbling(PlayerTumble tumble)
	{
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		_body?.SetActive(active: false);
		if (!_wasTumbling)
		{
			_wasTumbling = true;
			_tumbleStarted = Time.time;
			if (_bot.HeldView != 0)
			{
				ReleaseHeld("WhileTumbling", 2215);
			}
			if (!_tumbleOnPurpose)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " got knocked over"));
				OnKnockedOver();
			}
		}
		Vector3 position = ((Component)tumble).transform.position - Vector3.up * 0.3f;
		_bot.Position = position;
		_bot.Velocity = (((Object)(object)tumble.rb != (Object)null) ? tumble.rb.velocity : Vector3.zero);
		_bot.Moving = false;
		_bot.Sprinting = false;
		_bot.Crouching = false;
		_bot.Crawling = false;
		_posture = 0;
		if (!tumble.tumbleOverride && tumble.physGrabObject.playerGrabbing.Count <= 0)
		{
			float num = (_tumbleOnPurpose ? 0.7f : 1.5f);
			if (!(Time.time - _tumbleStarted < num) && !(tumble.notMovingTimer < 0.4f) && Physics.Raycast(((Component)tumble).transform.position, Vector3.down, 1.2f, GroundMask, (QueryTriggerInteraction)1))
			{
				_tumbleOnPurpose = false;
				tumble.TumbleRequest(false, true);
			}
		}
	}

	private void TryDive()
	{
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsDive.Value)
		{
			return;
		}
		PlayerTumble tumble = _bot.Avatar.tumble;
		if ((Object)(object)tumble == (Object)null || _danger == null || Time.time < _nextDiveAt || !_fleeRun || !_danger.Aggressive || !((Object)(object)_danger.Target == (Object)(object)_bot.Avatar) || _danger.Kind == "EnemyHead" || Vector3.Distance(_danger.Position, Eye()) > 3.2f)
		{
			return;
		}
		_nextDiveAt = Time.time + Random.Range(6f, 12f);
		if (!(Random.value < 0.4f))
		{
			Vector3 val = Flat(_bot.Position - _danger.Position);
			if (val.sqrMagnitude < 0.01f)
			{
				val = Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward;
			}
			val.Normalize();
			BotInfo bot = _bot;
			Quaternion val2 = Quaternion.LookRotation(val);
			bot.Yaw = val2.eulerAngles.y;
			_bot.Pitch = -10f;
			_tumbleOnPurpose = true;
			Plugin.Log.LogInfo((object)(_bot.Name + " dives away from " + _danger.Kind));
			tumble.TumbleRequest(true, true);
			tumble.TumbleForce(val * 7f + Vector3.up * 2.5f);
		}
	}

	private bool TryStartExplore()
	{
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		if (!SemiFunc.RunIsLevel() || (Object)(object)LevelGenerator.Instance == (Object)null || LevelGenerator.Instance.LevelPathPoints == null)
		{
			return false;
		}
		if (_job == Job.Explore && (Object)(object)_explore != (Object)null && !Visited.Contains(_explore) && Time.time - _exploreStarted < 45f)
		{
			return true;
		}
		LevelPoint val = null;
		float num = float.MaxValue;
		foreach (LevelPoint levelPathPoint in LevelGenerator.Instance.LevelPathPoints)
		{
			if ((Object)(object)levelPathPoint == (Object)null || levelPathPoint.Truck || levelPathPoint.inStartRoom || Visited.Contains(levelPathPoint) || ((Object)(object)levelPathPoint.Room != (Object)null && (levelPathPoint.Room.Truck || levelPathPoint.Room.Extraction)))
			{
				continue;
			}
			Vector3 position = ((Component)levelPathPoint).transform.position;
			float num2 = Vector3.Distance(position, _bot.Position);
			if (num2 > 70f || !SafeSpot(position))
			{
				continue;
			}
			float num3 = num2;
			Vector3? val2 = BaseSpot();
			if (val2.HasValue)
			{
				float num4 = Vector3.Distance(val2.Value, position);
				float num5 = Mathf.Lerp(38f, 20f, P.Social);
				if (num4 > num5)
				{
					num3 += (num4 - num5) * 1.5f;
				}
			}
			foreach (BotInfo value in BotRegistry.Bots.Values)
			{
				if (value != _bot && !((Object)(object)value.Avatar == (Object)null) && !(Vector3.Distance(value.Position, _bot.Position) > 20f))
				{
					float num6 = Vector3.Distance(value.Position, position);
					if (num6 < 12f)
					{
						num3 += (12f - num6) * 2f;
					}
				}
			}
			PlayerAvatar val3 = NearestPerson(position);
			if ((Object)(object)val3 != (Object)null)
			{
				float num7 = Vector3.Distance(PersonPos(val3) ?? position, position);
				float num8 = Mathf.Lerp(16f, 6f, P.Social);
				if (num7 < num8)
				{
					num3 += (num8 - num7) * 1.5f;
				}
			}
			num3 += Random.Range(0f, 4f);
			if (num3 < num)
			{
				num = num3;
				val = levelPathPoint;
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		if ((Object)(object)_explore != (Object)(object)val)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " is exploring" + (((Object)(object)val.Room != (Object)null) ? (" (" + ((Object)val.Room).name + ")") : "")));
		}
		_job = Job.Explore;
		_explore = val;
		_exploreStarted = Time.time;
		_corners.Clear();
		ResetProgress();
		return true;
	}

	private Vector3 DoExplore()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_explore == (Object)null || Visited.Contains(_explore))
		{
			_explore = null;
			_job = Job.Follow;
			_thinkAt = 0f;
			return Vector3.zero;
		}
		Vector3 position = ((Component)_explore).transform.position;
		Vector3 val = Flat(position - _bot.Position);
		if (val.magnitude < 2f)
		{
			Visited.Add(_explore);
			_explore = null;
			_thinkAt = 0f;
			return Vector3.zero;
		}
		Vector3 val2 = Steer(_bot.Position, position);
		if (NoWayThere && Time.time - _exploreStarted > 1f)
		{
			Visited.Add(_explore);
			_explore = null;
			_thinkAt = 0f;
			return Vector3.zero;
		}
		return val2 * 3.2f;
	}

	private bool InView(Vector3 p, float halfAngle, float feel)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Eye();
		Vector3 val2 = p - val;
		if (val2.sqrMagnitude < feel * feel)
		{
			return true;
		}
		return Vector3.Angle(Quaternion.Euler(_bot.Pitch, _bot.Yaw, 0f) * Vector3.forward, val2) <= halfAngle;
	}

	private Percept Know(EnemyParent ep)
	{
		if (!_known.TryGetValue(ep, out var value))
		{
			value = new Percept();
			_known[ep] = value;
		}
		return value;
	}

	private void Perceive()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0441: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		Vector3 val = Eye();
		bool flag = time - _bot.LastHurtTime < 1.5f;
		_seenNow.Clear();
		foreach (Threat item in BotThreats.All())
		{
			float num = Vector3.Distance(item.Position, val);
			bool flag2 = false;
			if (num < 22f && InView(item.Position, 60f, 3f) && ClearLine(val, item.Position, null, ((Component)item.Parent).transform))
			{
				flag2 = true;
			}
			if ((!flag2 & flag) && num < 8f)
			{
				flag2 = true;
			}
			if (flag2)
			{
				Percept percept = Know(item.Parent);
				if (time - percept.SeenAt > 20f)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " spotted " + item.Kind));
					OnSpotted(item);
				}
				percept.Pos = item.Position;
				percept.SeenAt = time;
				_seenNow.Add(item.Parent);
			}
		}
		foreach (HeardSound item2 in BotSenses.Recent)
		{
			if (item2.Time <= _heardUpTo || (Object)(object)item2.Enemy == (Object)null)
			{
				continue;
			}
			float num2 = Vector3.Distance(item2.Position, val);
			float num3 = item2.Range;
			if (!ClearLine(val, item2.Position, null, ((Component)item2.Enemy).transform))
			{
				num3 *= 0.55f;
			}
			if (num2 > num3)
			{
				continue;
			}
			Percept percept2 = Know(item2.Enemy);
			bool flag3 = time - percept2.HeardAt > 4f && time - percept2.SeenAt > 5f;
			Vector3 val2 = item2.Position + Random.insideUnitSphere * Mathf.Min(num2 * 0.12f, 3f);
			val2.y = item2.Position.y;
			if (!_seenNow.Contains(item2.Enemy))
			{
				percept2.Pos = val2;
			}
			percept2.HeardAt = time;
			if (flag3)
			{
				Plugin.Log.LogInfo((object)string.Format("{0} heard something ({1}) {2:0}m away", _bot.Name, ((object)item2.Enemy.Enemy)?.GetType().Name ?? "monster", num2));
			}
			if (!_seenNow.Contains(item2.Enemy) && time >= _attentionNext)
			{
				_attentionNext = time + 1.2f;
				_attention = val2;
				_attentionUntil = time + 1.6f;
				if (flag3 && num2 < 12f && (_job == Job.Follow || _job == Job.Explore))
				{
					_listenUntil = time + 0.9f;
					OnHeardNoise();
				}
			}
		}
		if (BotSenses.Recent.Count > 0)
		{
			_heardUpTo = BotSenses.Recent[BotSenses.Recent.Count - 1].Time;
		}
		foreach (KeyValuePair<EnemyParent, Percept> item3 in _known)
		{
			if (time - item3.Value.SeenAt > 0.5f)
			{
				continue;
			}
			foreach (BotInfo value in BotRegistry.Bots.Values)
			{
				if (value == _bot || (Object)(object)value.Avatar == (Object)null || value.Avatar.isDisabled || Vector3.Distance(value.Position, _bot.Position) > 15f)
				{
					continue;
				}
				BotBrain botBrain = BotManager.BrainOf(value.Avatar);
				if (botBrain != null)
				{
					Percept percept3 = botBrain.Know(item3.Key);
					if (time - percept3.SeenAt > 0.5f)
					{
						percept3.Pos = item3.Value.Pos;
						percept3.HeardAt = time;
					}
				}
			}
		}
		List<EnemyParent> list = null;
		foreach (KeyValuePair<EnemyParent, Percept> item4 in _known)
		{
			if ((Object)(object)item4.Key == (Object)null || (time - item4.Value.SeenAt > 20f && time - item4.Value.HeardAt > 20f))
			{
				(list ?? (list = new List<EnemyParent>())).Add(item4.Key);
			}
		}
		if (list != null)
		{
			foreach (EnemyParent item5 in list)
			{
				_known.Remove(item5);
			}
		}
		_knownFrame = -1;
	}

	private List<Threat> KnownThreats()
	{
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		if (_knownFrame == Time.frameCount)
		{
			return _knownList;
		}
		_knownFrame = Time.frameCount;
		_knownList.Clear();
		float time = Time.time;
		foreach (Threat item in BotThreats.All())
		{
			if (_known.TryGetValue(item.Parent, out var value))
			{
				bool flag = time - value.SeenAt < 5f;
				bool flag2 = time - value.HeardAt < 4f;
				if (flag || flag2)
				{
					bool flag3 = time - value.SeenAt < 1.5f;
					_knownList.Add(new Threat
					{
						Parent = item.Parent,
						Kind = item.Kind,
						State = (flag3 ? item.State : "Unknown"),
						Target = (flag3 ? item.Target : null),
						Position = (_seenNow.Contains(item.Parent) ? item.Position : value.Pos),
						Rule = item.Rule,
						Aggressive = (flag3 && item.Aggressive),
						Calm = (flag3 && item.Calm)
					});
				}
			}
		}
		return _knownList;
	}

	private BotBody Body()
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_body != (Object)null)
		{
			return _body;
		}
		if ((Object)(object)PlayerController.instance == (Object)null)
		{
			return null;
		}
		_body = BotBody.Create(_bot, _bot.Position);
		_bot.Body = ((Component)_body).gameObject;
		_body.IgnoreWith(((Component)((Component)_bot.Avatar).transform.root).gameObject);
		if ((Object)(object)_bot.Avatar.tumble != (Object)null)
		{
			_body.IgnoreWith(((Component)_bot.Avatar.tumble).gameObject);
		}
		return _body;
	}

	private void SyncBodyIgnores()
	{
		PhysGrabObject val = BotGrab.Held(_bot);
		if (!((Object)(object)val == (Object)(object)_bodyIgnoresHeld))
		{
			if ((Object)(object)_bodyIgnoresHeld != (Object)null)
			{
				_body.IgnoreWith(((Component)_bodyIgnoresHeld).gameObject, ignore: false);
			}
			if ((Object)(object)val != (Object)null)
			{
				_body.IgnoreWith(((Component)val).gameObject);
			}
			_bodyIgnoresHeld = val;
		}
	}

	private Vector3 EscapeFromCart()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_body == (Object)null || (Object)(object)_pushing != (Object)null)
		{
			return Vector3.zero;
		}
		PhysGrabCart val = null;
		if (_body.Grounded && (Object)(object)_body.GroundCollider != (Object)null)
		{
			val = ((Component)_body.GroundCollider).GetComponentInParent<PhysGrabCart>();
		}
		if ((Object)(object)val == (Object)null)
		{
			foreach (PhysGrabCart allCart in AllCarts)
			{
				if ((Object)(object)allCart != (Object)null && InCartBox(allCart, _bot.Position + Vector3.up * 0.3f))
				{
					val = allCart;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return Vector3.zero;
		}
		if (Time.time >= _escapeLogAt)
		{
			_escapeLogAt = Time.time + 5f;
			Plugin.Log.LogInfo((object)(_bot.Name + " is climbing out of the cart"));
		}
		Vector3 val2 = Flat(_bot.Position - ((Component)val).transform.position);
		if (val2.sqrMagnitude < 0.01f)
		{
			val2 = ((Component)val).transform.right;
		}
		return val2.normalized;
	}

	private bool ShouldJump(Vector3 desired)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_body == (Object)null || !_body.Grounded || _posture != 0 || _bot.HeldView != 0)
		{
			return false;
		}
		Vector3 val = Flat(desired);
		if (val.magnitude < 1f)
		{
			return false;
		}
		val.Normalize();
		Vector3 position = _bot.Position;
		float num = 0.75f;
		int num2 = MoveMask | LayerMask.GetMask(new string[1] { "PlayerOnlyCollision" });
		RaycastHit val2 = default;
		if (!Physics.Raycast(position + Vector3.up * 0.55f, val, out val2, num, num2, (QueryTriggerInteraction)1) || Ignored(val2.collider) || !(val2.normal.y < 0.5f))
		{
			return false;
		}
		if (Physics.Raycast(position + Vector3.up * 1.3f, val, num + 0.4f, num2, (QueryTriggerInteraction)1))
		{
			return false;
		}
		RaycastHit val3 = default;
		if (!Physics.Raycast(position + val * (num + 0.3f) + Vector3.up * 1.3f, Vector3.down, out val3, 1.3f, num2, (QueryTriggerInteraction)1))
		{
			return false;
		}
		if ((Object)(object)((Component)val3.collider).GetComponentInParent<PhysGrabObject>() != (Object)null || (Object)(object)((Component)val2.collider).GetComponentInParent<PhysGrabObject>() != (Object)null)
		{
			return false;
		}
		float num3 = val3.point.y - position.y;
		if (num3 > 0.35f)
		{
			return num3 < 1.1f;
		}
		return false;
	}

	private Vector3 HoldPoint(bool withLift = true)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		Quaternion val = Quaternion.Euler(0f, _bot.Yaw, 0f);
		float num = ((withLift && Time.time < _liftUntil) ? 0.6f : 0f);
		Vector3 val2 = _bot.Position + Vector3.up * (0.95f + num) + val * Vector3.forward * 1f;
		PhysGrabObject val3 = BotGrab.Held(_bot);
		if ((Object)(object)val3 != (Object)null && _job != Job.Cart)
		{
			if ((Object)(object)_holdSizeOf != (Object)(object)val3)
			{
				_holdSizeOf = val3;
				_holdHalfH = 0.15f;
				Collider[] componentsInChildren = ((Component)val3).GetComponentsInChildren<Collider>();
				foreach (Collider val4 in componentsInChildren)
				{
					if ((Object)(object)val4 != (Object)null && !val4.isTrigger)
					{
						float holdHalfH = _holdHalfH;
						Bounds bounds = val4.bounds;
						_holdHalfH = Mathf.Max(holdHalfH, bounds.extents.y);
					}
				}
				_holdHalfH = Mathf.Min(_holdHalfH, 1f);
			}
			RaycastHit val5 = default;
			if (Physics.Raycast(new Vector3(val2.x, _bot.Position.y + 1.5f + 0.4f, val2.z), Vector3.down, out val5, 3.1f, GroundMask, (QueryTriggerInteraction)1) && (Object)(object)((Component)val5.collider).GetComponentInParent<PhysGrabObject>() == (Object)null)
			{
				val2.y = Mathf.Max(val2.y, Mathf.Min(val5.point.y + _holdHalfH + 0.3f, _bot.Position.y + 1.5f + 0.4f));
			}
		}
		return val2;
	}

	private Vector3 CartDropGoal(PhysGrabObject held, Vector3 gp)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		Transform inCart = _cart.inCart;
		Vector3 position = inCart.position;
		float num = 0.15f;
		Collider[] componentsInChildren = ((Component)held).GetComponentsInChildren<Collider>();
		foreach (Collider val in componentsInChildren)
		{
			if ((Object)(object)val != (Object)null && !val.isTrigger)
			{
				float num2 = num;
				Bounds bounds = val.bounds;
				num = Mathf.Max(num2, bounds.extents.y);
			}
		}
		Vector3 val2 = gp - held.centerPoint;
		float num3 = inCart.position.y + Mathf.Abs(inCart.lossyScale.y) * 0.5f + num + 0.3f;
		Vector3 centerPoint = held.centerPoint;
		Vector3 val3 = Flat(centerPoint - position);
		Vector3 val4;
		if (val3.magnitude > 0.3f)
		{
			val4 = ((centerPoint.y < num3 - 0.1f) ? new Vector3(centerPoint.x, num3, centerPoint.z) : new Vector3(position.x, num3, position.z));
		}
		else
		{
			float num4 = inCart.position.y - Mathf.Abs(inCart.lossyScale.y) * 0.5f;
			val4 = new Vector3(position.x, num4 + num + 0.15f, position.z);
		}
		Vector3 val5 = val4 + val2;
		return Eye() + Vector3.ClampMagnitude(val5 - Eye(), 2.6f);
	}

	private Vector3 DropPoint()
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		if (_job == Job.Revive && (Object)(object)_cart != (Object)null && (Object)(object)_cart.inCart != (Object)null)
		{
			return _cart.inCart.position + Vector3.up * 0.35f;
		}
		if (_job == Job.Revive)
		{
			return (((Object)(object)_reviveEP != (Object)null) ? ExtractionSpot(_reviveEP) : ((Component)TruckSafetySpawnPoint.instance).transform.position) + Vector3.up * 0.6f;
		}
		if ((Object)(object)_shopItem != (Object)null)
		{
			return ShopDropPoint();
		}
		if ((Object)(object)_cart != (Object)null && (Object)(object)_cart.inCart != (Object)null)
		{
			return _cart.inCart.position + Vector3.up * 0.35f;
		}
		ExtractionPoint val = ActiveExtraction();
		if (!((Object)(object)val != (Object)null))
		{
			return HoldPoint();
		}
		return ExtractionSpot(val) + Vector3.up * 0.6f;
	}

	private void UpdatePuller(float dt)
	{
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0292: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0403: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0380: Unknown result type (might be due to invalid IL or missing references)
		//IL_047b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0480: Unknown result type (might be due to invalid IL or missing references)
		//IL_0486: Unknown result type (might be due to invalid IL or missing references)
		//IL_048b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0413: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		//IL_0418: Unknown result type (might be due to invalid IL or missing references)
		//IL_042f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0434: Unknown result type (might be due to invalid IL or missing references)
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_031d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_05be: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e7: Unknown result type (might be due to invalid IL or missing references)
		if (_bot.HeldView == 0)
		{
			return;
		}
		PhysGrabObject val = BotGrab.Held(_bot);
		if ((Object)(object)val == (Object)null || val.dead)
		{
			ReleaseHeld("UpdatePuller", 2720);
			return;
		}
		PhysGrabber physGrabber = _bot.Avatar.physGrabber;
		if ((Object)(object)_setDown != (Object)null && (Object)(object)_setDown == (Object)(object)val)
		{
			SetDownPuller();
			return;
		}
		if (_job == Job.Cart)
		{
			_bot.Puller = physGrabber.physGrabPoint.position;
			return;
		}
		if (_job == Job.Assist)
		{
			AssistPuller();
			return;
		}
		if (_doorHolding)
		{
			DoorPuller();
			return;
		}
		if (_job == Job.Gnome)
		{
			GnomePuller();
			return;
		}
		if (_job == Job.Open)
		{
			OpenPuller();
			return;
		}
		Vector3 position = physGrabber.physGrabPoint.position;
		Vector3 val2 = HoldPoint();
		if (_job == Job.Freeze)
		{
			val2 = position;
		}
		else if (_carry == Carry.Deliver && (Object)(object)_cart != (Object)null && (Object)(object)_cart.inCart != (Object)null && (_job == Job.Haul || _job == Job.Revive))
		{
			val2 = CartDropGoal(val, position);
		}
		else if (_carry == Carry.Deliver)
		{
			val2 = Eye() + Vector3.ClampMagnitude(DropPoint() - Eye(), 2.6f);
		}
		Vector3 val3 = val2 - position;
		float magnitude = val3.magnitude;
		Vector3 val4 = position;
		if (Time.time >= _crampedCheckAt)
		{
			_crampedCheckAt = Time.time + 0.25f;
			_itemCramped = ItemCramped(val);
		}
		bool flag = Time.time - _grabbedAt < 2f || _itemCramped;
		RaycastHit block2;
		if (magnitude > 0.03f)
		{
			Vector3 val5 = val3 / magnitude;
			float dist = Mathf.Clamp(magnitude, 0.15f, 0.7f);
			Vector3 val6 = Vector3.zero;
			if (ItemPathClear(val, val5, dist, out var block))
			{
				val6 = val5;
			}
			else
			{
				flag = true;
				Vector3 val7 = Eye() - position;
				Vector3 normalized = val7.normalized;
				val7 = Vector3.Cross(Vector3.up, val5);
				Vector3 normalized2 = val7.normalized;
				Vector3[] array = new Vector3[7];
				val7 = Vector3.ProjectOnPlane(val5, block.normal);
				array[0] = val7.normalized;
				array[1] = Vector3.up;
				val7 = Vector3.ProjectOnPlane(normalized, block.normal);
				array[2] = val7.normalized;
				array[3] = normalized;
				array[4] = block.normal;
				array[5] = normalized2;
				array[6] = -normalized2;
				Vector3[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					Vector3 val8 = array2[i];
					if (!(val8.sqrMagnitude < 0.5f) && (!(Vector3.Dot(val8, val5) < -0.3f) || !(val8 != normalized) || !(val8 != block.normal)) && ItemPathClear(val, val8, 0.25f, out block2))
					{
						val6 = val8;
						break;
					}
				}
			}
			if (val6 == Vector3.zero)
			{
				_itemBlocked += dt;
			}
			else
			{
				_itemBlocked = 0f;
				float num = (flag ? 0.22f : Mathf.Min(magnitude, 0.9f));
				val4 = position + val6 * num;
			}
		}
		else
		{
			val4 = val2;
			_itemBlocked = 0f;
		}
		float num2 = SafeBumpSpeed(val);
		Vector3 val9 = (((Object)(object)val.rb != (Object)null) ? val.rb.velocity : Vector3.zero);
		float magnitude2 = val9.magnitude;
		if (Plugin.GentleSetDown.Value && magnitude2 > Mathf.Max(num2, 0.25f))
		{
			float dist2 = 0.08f + magnitude2 * magnitude2 / 12f + magnitude2 * 0.06f;
			if (!ItemPathClear(val, val9 / magnitude2, dist2, out block2))
			{
				val4 = position - val9 / magnitude2 * Mathf.Min(0.3f, magnitude2 * 0.15f);
				flag = true;
			}
		}
		float num3;
		if (flag)
		{
			num3 = 0.8f;
		}
		else
		{
			num3 = (Plugin.GentleSetDown.Value ? Mathf.Clamp(num2 * 4f, 1.4f, 3f) : 3f);
		}
		Vector3 val10 = Vector3.MoveTowards(_bot.Puller, val4, num3 * dt);
		float num4;
		if (flag)
		{
			num4 = 0.3f;
		}
		else
		{
			num4 = ((Plugin.GentleSetDown.Value && num2 < 0.6f) ? 0.55f : 1f);
		}
		_bot.Puller = position + Vector3.ClampMagnitude(val10 - position, num4);
		if (_itemBlocked > 3f && (Object)(object)_target == (Object)(object)val)
		{
			_itemBlocked = 0f;
			ReleaseHeld("UpdatePuller", 2847);
			_carry = Carry.Approach;
			if (++_wedgedCount >= 2)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " couldn't get " + ((Object)val).name + " out without breaking it"));
				Blacklist[val] = Time.time + 120f;
				DropJob("UpdatePuller", 2853);
			}
			else
			{
				Vector3 val11 = Quaternion.Euler(0f, (Random.value < 0.5f) ? 70f : (-70f), 0f) * Flat(_bot.Position - val.centerPoint);
				_approachFrom = val.centerPoint + val11.normalized * 1.4f;
			}
		}
	}

	private bool ItemPathClear(PhysGrabObject held, Vector3 dir, float dist, out RaycastHit block)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		block = default;
		if ((Object)(object)held.rb == (Object)null)
		{
			return true;
		}
		RaycastHit[] array = held.rb.SweepTestAll(dir, dist, (QueryTriggerInteraction)1);
		float num = float.MaxValue;
		bool flag = false;
		Transform root = ((Component)_bot.Avatar).transform.root;
		RaycastHit[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RaycastHit val = array2[i];
			if ((Object)(object)val.collider == (Object)null)
			{
				continue;
			}
			RaycastHit val2 = val;
			if (val.distance <= 0.001f)
			{
				if (!PushesInto(held, val.collider, dir, out var outward))
				{
					continue;
				}
				val2.normal = outward;
			}
			Transform transform = ((Component)val.collider).transform;
			if (!((Object)(object)transform.root == (Object)(object)root) && !((Object)(object)((Component)val.collider).GetComponentInParent<PlayerAvatar>() != (Object)null) && !((Object)(object)((Component)val.collider).GetComponentInParent<PlayerTumble>() != (Object)null) && (!((Object)(object)_cart != (Object)null) || !transform.IsChildOf(((Component)_cart).transform) || _carry != Carry.Deliver) && val2.distance < num)
			{
				num = val2.distance;
				block = val2;
				flag = true;
			}
		}
		return !flag;
	}

	private static bool PushesInto(PhysGrabObject held, Collider other, Vector3 dir, out Vector3 outward)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		outward = -dir;
		Collider[] componentsInChildren = ((Component)held).GetComponentsInChildren<Collider>();
		Vector3 val2 = default;
		float num = default;
		foreach (Collider val in componentsInChildren)
		{
			if (!((Object)(object)val == (Object)null) && val.enabled && !val.isTrigger)
			{
				if (Physics.ComputePenetration(val, ((Component)val).transform.position - dir * 0.02f, ((Component)val).transform.rotation, other, ((Component)other).transform.position, ((Component)other).transform.rotation, out val2, out num) && num > 0.001f && Vector3.Dot(dir, val2) < -0.25f)
				{
					outward = val2;
					return true;
				}
				if (Physics.ComputePenetration(val, ((Component)val).transform.position + dir * 0.03f, ((Component)val).transform.rotation, other, ((Component)other).transform.position, ((Component)other).transform.rotation, out val2, out num) && num > 0.015f && Vector3.Dot(dir, val2) < -0.25f)
				{
					outward = val2;
					return true;
				}
			}
		}
		return false;
	}

	private Vector3? ViewSpot(PhysGrabObject t, Vector3 c)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		Vector3? result = null;
		float num = float.MaxValue;
		NavMeshHit val2 = default;
		for (int i = 0; i < 12; i++)
		{
			Vector3 val = Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward;
			float[] array = new float[2] { 1.4f, 2.4f };
			foreach (float num2 in array)
			{
				if (NavMesh.SamplePosition(c + val * num2, out val2, 1.2f, -1) && BotGrab.CanGrab(_bot, t, val2.position + Vector3.up * 1.5f, null, 3.5f))
				{
					float num3 = Vector3.Distance(val2.position, _bot.Position);
					if (num3 < num)
					{
						num = num3;
						result = val2.position;
					}
				}
			}
		}
		return result;
	}

	private bool NeedsOtherSide(PhysGrabObject t, Vector3 c, Vector3 eye)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		if (_approachFrom.HasValue)
		{
			return true;
		}
		Vector3 val = eye - c;
		Vector3 normalized = val.normalized;
		if (!ItemPathClear(t, normalized, 0.6f, out var block))
		{
			val = normalized + Vector3.up;
			if (!ItemPathClear(t, val.normalized, 0.6f, out block))
			{
				Vector3 val2 = Vector3.zero;
				float num = float.MaxValue;
				NavMeshHit val4 = default;
				for (int i = 0; i < 12; i++)
				{
					Vector3 val3 = Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward;
					val = val3 + Vector3.up * 0.3f;
					Vector3 normalized2 = val.normalized;
					if (ItemPathClear(t, normalized2, 0.6f, out block) && NavMesh.SamplePosition(c + val3 * 1.5f, out val4, 1.5f, -1) && ClearLine(val4.position + Vector3.up * 1.5f, c, t, null))
					{
						float num2 = Vector3.Distance(val4.position, _bot.Position);
						if (num2 < num)
						{
							num = num2;
							val2 = val4.position;
						}
					}
				}
				if (num == float.MaxValue)
				{
					return false;
				}
				val = Flat(val2 - _bot.Position);
				if (val.magnitude < 0.6f)
				{
					return false;
				}
				_approachFrom = val2;
				return true;
			}
		}
		return false;
	}

	private bool ItemCramped(PhysGrabObject held)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		Vector3[] array = new Vector3[5]
		{
			Vector3.up,
			Vector3.left,
			Vector3.right,
			Vector3.forward,
			Vector3.back
		};
		foreach (Vector3 dir in array)
		{
			if (!ItemPathClear(held, dir, 0.15f, out var _))
			{
				return true;
			}
		}
		return false;
	}

	private Vector3 FleeSpot(Vector3 threat, string kind = null)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		bool flag = kind == "EnemyBeamer";
		Vector3 val = Flat(_bot.Position - threat);
		if (val.sqrMagnitude < 0.01f)
		{
			val = Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.back;
		}
		val.Normalize();
		Vector3 position = _bot.Position;
		float num = float.MinValue;
		NavMeshHit val3 = default;
		for (int i = 0; i < 14; i++)
		{
			Vector3 val2 = Quaternion.Euler(0f, Random.Range(-100f, 100f), 0f) * val;
			if (NavMesh.SamplePosition(_bot.Position + val2 * Random.Range(6f, 14f), out val3, 3f, -1))
			{
				float num2 = Vector3.Distance(val3.position, threat);
				bool flag2 = Physics.Linecast(threat, val3.position + Vector3.up * 1f, GroundMask, (QueryTriggerInteraction)1);
				if (flag2)
				{
					num2 += (flag ? 15f : 8f);
				}
				if (flag && !flag2 && Vector3.Distance(val3.position, threat) < 11f)
				{
					num2 -= 10f;
				}
				PlayerAvatar val4 = NearestPerson(val3.position);
				if ((Object)(object)val4 != (Object)null)
				{
					num2 -= 0.2f * Vector3.Distance(val3.position, PersonPos(val4) ?? val3.position);
				}
				if (num2 > num)
				{
					num = num2;
					position = val3.position;
				}
			}
		}
		_corners.Clear();
		return position;
	}

	private PlayerAvatar PickLeader()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar result = null;
		float num = float.MaxValue;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			Vector3? val = PersonPos(player);
			if (val.HasValue)
			{
				Vector3 val2 = val.Value - _bot.Position;
				float num2 = val2.sqrMagnitude;
				if ((Object)(object)player == (Object)(object)_leader)
				{
					num2 *= 0.5f;
				}
				if (num2 < num)
				{
					num = num2;
					result = player;
				}
			}
		}
		return result;
	}

	private PlayerAvatar NearestPerson(Vector3 from)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar result = null;
		float num = float.MaxValue;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			Vector3? val = PersonPos(player);
			if (val.HasValue)
			{
				Vector3 val2 = val.Value - from;
				float sqrMagnitude = val2.sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = player;
				}
			}
		}
		return result;
	}

	private bool ClearWalk(Vector3 from, Vector3 to)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(to - from);
		float magnitude = val.magnitude;
		if (magnitude < 0.01f || Mathf.Abs(to.y - from.y) > 0.6f)
		{
			return magnitude < 0.01f;
		}
		Vector3 val2 = val / magnitude;
		float[] array = new float[2] { 0.5f, 1.2f };
		foreach (float num in array)
		{
			int num2 = Physics.SphereCastNonAlloc(from + Vector3.up * num, 0.288f, val2, Hits, magnitude, MoveMask, (QueryTriggerInteraction)1);
			for (int j = 0; j < num2; j++)
			{
				if (Hits[j].distance > 0f && !Ignored(Hits[j].collider))
				{
					return false;
				}
			}
		}
		return BotPits.FloorAlong(from, to);
	}

	private Vector3 Steer(Vector3 pos, Vector3 goal)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_02af: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a7: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _sidestepUntil)
		{
			return _sidestep;
		}
		Vector3 val;
		if (!(Time.time >= _repathAt) && (_corner < _corners.Count || _pathFailed))
		{
			val = goal - _pathGoal;
			if (!(val.sqrMagnitude > 1f))
			{
				goto IL_0060;
			}
		}
		Repath(pos, goal);
		goto IL_0060;
		IL_0060:
		if (_pathFailed)
		{
			if (!StraightOk(pos, goal))
			{
				return Vector3.zero;
			}
			val = Flat(goal - pos);
			return AvoidObjects(pos, val.normalized);
		}
		Vector3 val2 = ((_pathPartial && _corners.Count > 0) ? _corners[_corners.Count - 1] : goal);
		float num = (Plugin.RealPhysics.Value ? 0.7f : 0.35f);
		while (_corner < _corners.Count)
		{
			Vector3 val3 = _corners[_corner];
			val = Flat(val3 - pos);
			if (val.magnitude > num)
			{
				val2 = val3;
				break;
			}
			if (_pathPartial && _corner == _corners.Count - 1)
			{
				break;
			}
			_corner++;
		}
		if (_pathPartial && _corner >= _corners.Count - 1)
		{
			val = Flat(val2 - pos);
			if (val.magnitude <= num)
			{
				if (!StraightOk(pos, goal))
				{
					return Vector3.zero;
				}
				val = Flat(goal - pos);
				return AvoidObjects(pos, val.normalized);
			}
		}
		bool flag = _bot.HeldView != 0 && (_job == Job.Haul || _job == Job.Revive);
		while (!flag && _corner + 1 < _corners.Count && ClearWalk(pos, _corners[_corner + 1]))
		{
			_corner++;
			val2 = _corners[_corner];
		}
		NavMeshHit val4 = default;
		if (flag && NavMesh.FindClosestEdge(val2, out val4, -1) && val4.distance < 0.9f)
		{
			Vector3 val5 = Flat(val2 - val4.position);
			Vector3 normalized;
			if (!(val5.sqrMagnitude > 0.0025f))
			{
				val = Flat(val4.normal);
				normalized = val.normalized;
			}
			else
			{
				normalized = val5.normalized;
			}
			val5 = normalized;
			val2 += val5 * (0.9f - val4.distance);
		}
		Vector3 val6 = Flat(val2 - pos);
		if (val6.sqrMagnitude <= 0.0001f)
		{
			return Vector3.zero;
		}
		return AvoidObjects(pos, val6.normalized);
	}

	private Vector3 AvoidObjects(Vector3 pos, Vector3 dir)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _avoidUntil && Vector3.Dot(_avoidDir, dir) > -0.2f)
		{
			return _avoidDir;
		}
		if (!ObjectAhead(pos, dir, 1.4f))
		{
			return dir;
		}
		float[] array = new float[6] { 40f, -40f, 70f, -70f, 100f, -100f };
		foreach (float num in array)
		{
			Vector3 val = Quaternion.Euler(0f, num, 0f) * dir;
			if (!ObjectAhead(pos, val, 1.2f) && ClearWalk(pos, pos + val * 1f))
			{
				_avoidDir = val;
				_avoidUntil = Time.time + 0.6f;
				return val;
			}
		}
		return dir;
	}

	private bool ObjectAhead(Vector3 pos, Vector3 dir, float dist)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		float[] array = new float[2] { 0.45f, 1f };
		foreach (float num in array)
		{
			int num2 = Physics.SphereCastNonAlloc(pos + Vector3.up * num, 0.32f, dir, Hits, dist, MoveMask, (QueryTriggerInteraction)1);
			for (int j = 0; j < num2; j++)
			{
				Collider collider = Hits[j].collider;
				if (!((Object)(object)collider == (Object)null) && !(Hits[j].distance <= 0f) && !Ignored(collider))
				{
					PhysGrabObject componentInParent = ((Component)collider).GetComponentInParent<PhysGrabObject>();
					if ((!((Object)(object)componentInParent != (Object)null) || (!((Object)(object)componentInParent == (Object)(object)_target) && !((Object)(object)componentInParent == (Object)(object)_assist) && (!((Object)(object)_pushing != (Object)null) || !((Object)(object)componentInParent == (Object)(object)_pushing.physGrabObject)))) && (Object)(object)componentInParent != (Object)null && (Object)(object)((Component)collider).GetComponentInParent<PhysGrabHinge>() == (Object)null)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private bool StraightOk(Vector3 pos, Vector3 goal)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(goal - pos);
		float magnitude = val.magnitude;
		if (magnitude > 9f || Mathf.Abs(goal.y - pos.y) > 1.6f)
		{
			return false;
		}
		if (magnitude < 1.3f)
		{
			return true;
		}
		Vector3 val2 = pos + val / magnitude * (magnitude - 1.3f);
		return ClearWalk(pos, new Vector3(val2.x, pos.y, val2.z));
	}

	private void Repath(Vector3 from, Vector3 to)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Invalid comparison between Unknown and I4
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Invalid comparison between Unknown and I4
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		_repathAt = Time.time + 0.5f;
		_pathGoal = to;
		_corners.Clear();
		_corner = 0;
		_pathFailed = true;
		_pathPartial = false;
		NavMeshHit val = default;
		if (!NavMesh.SamplePosition(from, out val, 2f, -1))
		{
			return;
		}
		float[] array = new float[2] { 3f, 8f };
		NavMeshHit val2 = default;
		foreach (float num in array)
		{
			if (!NavMesh.SamplePosition(to, out val2, num, -1) || !NavMesh.CalculatePath(val.position, val2.position, -1, _path) || (int)_path.status == 2 || _path.corners.Length == 0)
			{
				continue;
			}
			_pathFailed = false;
			int pathPartial;
			if ((int)_path.status != 1)
			{
				Vector3 val3 = Flat(val2.position - to);
				pathPartial = ((val3.magnitude > 1.5f) ? 1 : 0);
			}
			else
			{
				pathPartial = 1;
			}
			_pathPartial = (byte)pathPartial != 0;
			_corners.AddRange(_path.corners);
			if (BotPits.RouteOverPit(_corners, 0))
			{
				_corners.Clear();
				_pathFailed = true;
				if (Time.time - _pitLogAt > 5f)
				{
					_pitLogAt = Time.time;
					Plugin.Log.LogInfo((object)$"{_bot.Name}'s route goes over a pit, not taking it ({_job})");
				}
			}
			else
			{
				CheckRoute(to);
			}
			break;
		}
	}

	private static bool OnNavmesh(Vector3 p)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		NavMeshHit val = default;
		if (NavMesh.SamplePosition(p, out val, 2.5f, -1))
		{
			return Mathf.Abs(val.position.y - p.y) < 2.5f;
		}
		return false;
	}

	private Vector3 Separation(Vector3 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_021f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Vector3.zero;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player == (Object)null || (Object)(object)player == (Object)(object)_bot.Avatar || player.isDisabled)
			{
				continue;
			}
			BotInfo botInfo = BotRegistry.Get(player);
			Vector3 val2 = botInfo?.Position ?? ((Component)player).transform.position;
			Vector3 val3 = Flat(pos - val2);
			float magnitude = val3.magnitude;
			PhysGrabObject val4;
			if (botInfo != null)
			{
				val4 = BotGrab.Held(botInfo);
			}
			else
			{
				val4 = (((Object)(object)player.physGrabber != (Object)null && player.physGrabber.grabbed) ? player.physGrabber.grabbedPhysGrabObject : null);
			}
			bool flag = (Object)(object)val4 != (Object)null && (Object)(object)((Component)val4).GetComponent<ValuableObject>() != (Object)null;
			float num;
			if (flag)
			{
				num = (HoldingLoot() ? 1.6f : 2.2f);
			}
			else
			{
				num = 1.1f;
			}
			if (magnitude < num && magnitude > 0.001f)
			{
				val += val3 / magnitude * (num - magnitude) * (flag ? 4f : 3f);
			}
			if (flag)
			{
				Vector3 val5 = Flat(pos - val4.centerPoint);
				float magnitude2 = val5.magnitude;
				if (magnitude2 < 1.4f && magnitude2 > 0.001f)
				{
					val += val5 / magnitude2 * (1.4f - magnitude2) * 4f;
				}
			}
		}
		float num2 = ((_bot.HeldView != 0) ? 4f : 2.5f);
		foreach (Threat item in KnownThreats())
		{
			if (item.Rule == ThreatRule.Harmless)
			{
				Vector3 val6 = Flat(pos - item.Position);
				float magnitude3 = val6.magnitude;
				if (magnitude3 < num2 && magnitude3 > 0.001f)
				{
					val += val6 / magnitude3 * (num2 - magnitude3) * 2f;
				}
			}
		}
		return val;
	}

	private bool Ignored(Collider c)
	{
		Transform transform = ((Component)c).transform;
		if ((Object)(object)transform.root == (Object)(object)((Component)_bot.Avatar).transform.root)
		{
			return true;
		}
		PhysGrabObject val = BotGrab.Held(_bot);
		if ((Object)(object)val != (Object)null && transform.IsChildOf(((Component)val).transform))
		{
			return true;
		}
		if ((Object)(object)_pushing != (Object)null && transform.IsChildOf(((Component)_pushing).transform))
		{
			return true;
		}
		return false;
	}

	private Vector3 MoveWithCollision(Vector3 pos, Vector3 delta)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(delta);
		for (int i = 0; i < 3; i++)
		{
			if (!(val.sqrMagnitude > 1E-08f))
			{
				break;
			}
			float magnitude = val.magnitude;
			Vector3 val2 = val / magnitude;
			Dims(_posture, out var radius, out var height, out var step);
			Vector3 val3 = pos + Vector3.up * (step + radius);
			Vector3 val4 = pos + Vector3.up * Mathf.Max(height - radius, step + radius);
			int num = Physics.CapsuleCastNonAlloc(val3, val4, radius, val2, Hits, magnitude + 0.03f, MoveMask, (QueryTriggerInteraction)1);
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int j = 0; j < num; j++)
			{
				if (!(Hits[j].distance <= 0f) && !Ignored(Hits[j].collider) && !(Vector3.Dot(Hits[j].normal, val2) > 0f) && Hits[j].distance < num3)
				{
					num3 = Hits[j].distance;
					num2 = j;
				}
			}
			if (num2 < 0)
			{
				pos += val;
				break;
			}
			RaycastHit hit = Hits[num2];
			float num4 = Mathf.Max(0f, hit.distance - 0.03f);
			pos += val2 * num4;
			Push(hit, val2);
			Vector3 val5 = Flat(hit.normal);
			if (val5.sqrMagnitude < 1E-06f)
			{
				break;
			}
			val5.Normalize();
			val = val2 * (magnitude - num4);
			val -= val5 * Vector3.Dot(val, val5);
		}
		return pos;
	}

	private void Push(RaycastHit hit, Vector3 dir)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		Rigidbody rigidbody = hit.rigidbody;
		if (!((Object)(object)rigidbody == (Object)null) && !rigidbody.isKinematic)
		{
			if ((Object)(object)((Component)rigidbody).GetComponentInParent<PhysGrabHinge>() != (Object)null)
			{
				rigidbody.AddForceAtPosition(dir * 25f, hit.point, (ForceMode)5);
			}
			else if ((Object)(object)((Component)rigidbody).GetComponentInParent<ValuableObject>() == (Object)null && rigidbody.mass < 3f)
			{
				rigidbody.AddForce(dir * 4f, (ForceMode)5);
			}
		}
	}

	private void CheckBlocked(Vector3 desired, Vector3 actual)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(desired);
		if (val.magnitude < 1f || Time.time < _sidestepUntil)
		{
			_blockedSince = -1f;
		}
		else if (Vector3.Dot(actual, val.normalized) > val.magnitude * 0.35f)
		{
			_blockedSince = -1f;
		}
		else if (!ClearWalk(_bot.Position, _bot.Position + val.normalized * 0.6f))
		{
			if (_blockedSince < 0f)
			{
				_blockedSince = Time.time;
			}
			if (_blockedSince < 0f)
			{
				_blockedSince = Time.time;
			}
			else
			{
				if (Time.time - _blockedSince < 0.5f)
				{
					return;
				}
				_blockedSince = -1f;
				Vector3 normalized = val.normalized;
				float[] array = new float[6] { 50f, -50f, 90f, -90f, 130f, -130f };
				foreach (float num in array)
				{
					Vector3 val2 = Quaternion.Euler(0f, num, 0f) * normalized;
					Vector3 val3 = _bot.Position + Vector3.up * 0.72f;
					Vector3 val4 = _bot.Position + Vector3.up * 1.3800001f;
					bool flag = false;
					int num2 = Physics.CapsuleCastNonAlloc(val3, val4, 0.32f, val2, Hits, 0.9f, MoveMask, (QueryTriggerInteraction)1);
					for (int j = 0; j < num2; j++)
					{
						if (Hits[j].distance > 0f && !Ignored(Hits[j].collider))
						{
							flag = true;
						}
					}
					if (!flag)
					{
						_sidestep = val2;
						_sidestepUntil = Time.time + 0.5f;
						_corners.Clear();
						break;
					}
				}
			}
		}
		else
		{
			_blockedSince = -1f;
		}
	}

	private Vector3 SnapToGround(Vector3 p, Vector3 prev)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		Transform root = ((Component)_bot.Avatar).transform.root;
		int num = Physics.RaycastNonAlloc(p + Vector3.up * 0.5f, Vector3.down, Hits, 2.6000001f, GroundMask, (QueryTriggerInteraction)1);
		float num2 = float.MaxValue;
		float num3 = float.NaN;
		for (int i = 0; i < num; i++)
		{
			if (!((Object)(object)((Component)Hits[i].collider).transform.root == (Object)(object)root) && !(Hits[i].point.y > prev.y + 0.4f + 0.05f) && Hits[i].distance < num2)
			{
				num2 = Hits[i].distance;
				num3 = Hits[i].point.y;
			}
		}
		if (!float.IsNaN(num3))
		{
			p.y = num3;
			return p;
		}
		NavMeshHit val = default;
		if (NavMesh.SamplePosition(p, out val, 1.5f, -1) && val.position.y <= prev.y + 0.4f + 0.05f)
		{
			return val.position;
		}
		return prev;
	}

	private void Look(Vector3 vel, Vector3? lookAt, float dt)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0400: Unknown result type (might be due to invalid IL or missing references)
		//IL_0405: Unknown result type (might be due to invalid IL or missing references)
		//IL_041c: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_0415: Unknown result type (might be due to invalid IL or missing references)
		//IL_0428: Unknown result type (might be due to invalid IL or missing references)
		//IL_042d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0431: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		//IL_030f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0313: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		float num = _bot.Yaw;
		float num2 = 0f;
		Vector3 val = Flat(vel);
		Vector3 val2 = Eye();
		bool flag = HoldingLoot();
		Vector3 val3;
		Quaternion val6;
		if (_job == Job.Freeze)
		{
			num2 = 35f;
		}
		else
		{
			if (lookAt.HasValue)
			{
				val3 = lookAt.Value - val2;
				if (val3.sqrMagnitude > 0.01f)
				{
					Quaternion val4 = Quaternion.LookRotation(lookAt.Value - val2);
					num = val4.eulerAngles.y;
					num2 = Mathf.Clamp(NormalizeAngle(val4.eulerAngles.x), -60f, 70f);
					goto IL_038d;
				}
			}
			if (Time.time < _attentionUntil && _job != Job.Flee && !flag)
			{
				val3 = _attention - val2;
				if (val3.sqrMagnitude > 0.01f)
				{
					Quaternion val5 = Quaternion.LookRotation(_attention - val2);
					num = val5.eulerAngles.y;
					num2 = Mathf.Clamp(NormalizeAngle(val5.eulerAngles.x), -40f, 40f);
					goto IL_038d;
				}
			}
			if (val.sqrMagnitude > 0.3f)
			{
				val6 = Quaternion.LookRotation(val);
				num = val6.eulerAngles.y;
				if (_job == Job.Explore && !flag)
				{
					if (Time.time >= _scanAt)
					{
						_scanAt = Time.time + Random.Range(1.2f, 3f);
						_scanYaw = ((Random.value < 0.5f) ? Random.Range(-75f, 75f) : 0f);
					}
					num += _scanYaw;
					num2 = 10f;
				}
			}
			else if ((_job == Job.Flee || _job == Job.Hide) && _danger != null)
			{
				Vector3 val7 = _danger.Position - val2;
				if (val7.sqrMagnitude > 0.01f)
				{
					val6 = Quaternion.LookRotation(val7);
					num = val6.eulerAngles.y;
				}
			}
			else if (_job == Job.Follow && (Object)(object)_leader != (Object)null)
			{
				if (Time.time >= _glanceAt)
				{
					_glanceAt = Time.time + Random.Range(1.5f, 4f);
					float value = Random.value;
					ref float glanceYaw = ref _glanceYaw;
					float num3;
					if (value < 0.2f)
					{
						num3 = 0f;
					}
					else
					{
						num3 = ((value < 0.6f) ? 999f : (2000f + _bot.Yaw + Random.Range(-160f, 160f)));
					}
					glanceYaw = num3;
				}
				if (_glanceYaw == 0f && SeesPerson(_leader))
				{
					Vector3 val8 = ((Component)_leader).transform.position + Vector3.up * 1.5f - val2;
					if (val8.sqrMagnitude > 0.01f)
					{
						Quaternion val9 = Quaternion.LookRotation(val8);
						num = val9.eulerAngles.y;
						num2 = Mathf.Clamp(NormalizeAngle(val9.eulerAngles.x), -40f, 40f);
					}
				}
				else if (_glanceYaw == 999f && SeesPerson(_leader))
				{
					num = ((Component)_leader).transform.eulerAngles.y;
					num2 = 5f;
				}
				else
				{
					num = _glanceYaw - 2000f;
					num2 = 0f;
				}
			}
		}
		goto IL_038d;
		IL_038d:
		foreach (Vector3 item in _dontLook)
		{
			Vector3 val10 = item - val2;
			if (!(val10.sqrMagnitude < 0.01f) && Vector3.Angle(Quaternion.Euler(num2, num, 0f) * Vector3.forward, val10) < 65f)
			{
				if (val10.y > 1.2f)
				{
					num2 = 70f;
					continue;
				}
				val3 = Flat(-val10);
				val6 = Quaternion.LookRotation((val3.sqrMagnitude > 0.01f) ? Flat(-val10) : Vector3.forward);
				num = val6.eulerAngles.y;
				num2 = 10f;
			}
		}
		bool flag2 = _bot.HeldView != 0 && (_job == Job.Haul || _job == Job.Revive || _job == Job.Freeze);
		bool flag3 = flag2 && Plugin.GentleSetDown.Value && (Object)(object)_target != (Object)null && SafeBumpSpeed(_target) < 0.6f;
		float num4;
		if (_job == Job.Cart && _bot.HeldView != 0)
		{
			num4 = 60f;
		}
		else if (flag3)
		{
			num4 = 75f;
		}
		else
		{
			num4 = ((flag2 | flag) ? 110f : 360f);
		}
		float num5 = 400f;
		if (flag && Plugin.GentleSetDown.Value)
		{
			num2 = Mathf.Clamp(num2, -10f, (_carry == Carry.Deliver || (Object)(object)_setDown != (Object)null) ? 40f : 20f);
			num5 = 25f;
		}
		if (Plugin.KeyboardMouse.Value)
		{
			MouseLook(num, num2, (num4 < 150f) ? num4 : (num4 * 2.2f), dt, num5);
			return;
		}
		_bot.Yaw = Mathf.MoveTowardsAngle(_bot.Yaw, num, num4 * dt);
		_bot.Pitch = Mathf.MoveTowards(_bot.Pitch, num2, Mathf.Min(160f, num5) * dt);
	}

	private Vector3 Keys(Vector3 want, bool sprint)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(want);
		float magnitude = val.magnitude;
		if (magnitude < 0.15f)
		{
			return Vector3.zero;
		}
		float num;
		if (_posture == 2)
		{
			num = 0.9f;
		}
		else if (_posture == 1)
		{
			num = 1.6f;
		}
		else
		{
			num = (sprint ? 5.6f : 3.2f);
		}
		float num2 = Vector3.SignedAngle(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward, val, Vector3.up);
		float num3 = ((Mathf.Abs(num2) < 20f) ? 0f : (Mathf.Round(num2 / 45f) * 45f));
		Vector3 val2 = Quaternion.Euler(0f, _bot.Yaw + num3, 0f) * Vector3.forward;
		float num4 = Mathf.Clamp01(magnitude / num);
		if (num4 >= 0.4f)
		{
			return val2 * Mathf.Min(magnitude, num);
		}
		_tapPhase = Mathf.Repeat(_tapPhase + Time.deltaTime / 0.25f, 1f);
		if (!(_tapPhase > num4 * 2f))
		{
			return val2 * num * 0.5f;
		}
		return Vector3.zero;
	}

	private bool HoldingLoot()
	{
		if (_bot.HeldView == 0 || _job == Job.Cart || _doorHolding || _job == Job.Open)
		{
			return false;
		}
		PhysGrabObject val = BotGrab.Held(_bot);
		if ((Object)(object)val != (Object)null)
		{
			if (!((Object)(object)((Component)val).GetComponent<ValuableObject>() != (Object)null))
			{
				return (Object)(object)((Component)val).GetComponent<PlayerDeathHead>() != (Object)null;
			}
			return true;
		}
		return false;
	}

	private void MouseLook(float targetYaw, float targetPitch, float maxRate, float dt, float maxPitchRate = 400f)
	{
		dt = Mathf.Min(dt, 0.05f);
		float num = Mathf.DeltaAngle(_bot.Yaw, targetYaw);
		_yawVel += (num * 70f - _yawVel * 12.5f) * dt;
		_yawVel = Mathf.Clamp(_yawVel, 0f - maxRate, maxRate);
		float num2 = targetPitch - _bot.Pitch;
		_pitchVel += (num2 * 55f - _pitchVel * 12f) * dt;
		_pitchVel = Mathf.Clamp(_pitchVel, 0f - maxPitchRate, maxPitchRate);
		float num3 = Time.time * 0.9f + (float)_bot.Index * 7.3f;
		float num4 = (Mathf.PerlinNoise(num3, 0.5f) - 0.5f) * 1.2f;
		float num5 = (Mathf.PerlinNoise(0.5f, num3) - 0.5f) * 0.8f;
		_bot.Yaw = Mathf.Repeat(_bot.Yaw + _yawVel * dt + num4 * dt * 4f, 360f);
		_bot.Pitch = Mathf.Clamp(_bot.Pitch + _pitchVel * dt + num5 * dt * 4f, -80f, 80f);
	}

	private void CheckStuck()
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f1: Unknown result type (might be due to invalid IL or missing references)
		if (Typing)
		{
			ResetProgress();
			return;
		}
		bool flag;
		Vector3 val;
		switch (_job)
		{
		case Job.Follow:
			flag = _walking;
			break;
		case Job.Flee:
			flag = true;
			break;
		case Job.Activate:
			flag = true;
			break;
		case Job.Explore:
			flag = true;
			break;
		case Job.Escort:
			flag = false;
			break;
		case Job.Guard:
			flag = false;
			break;
		case Job.Freeze:
			flag = false;
			break;
		case Job.Hide:
			val = Flat(_hideSpot - _bot.Position);
			flag = val.magnitude > 0.5f;
			break;
		case Job.Cart:
			flag = true;
			break;
		case Job.Heal:
			flag = !_bot.HoldingStatic;
			break;
		case Job.Open:
			flag = _openStep == OpenStep.Approach;
			break;
		case Job.HangOut:
			flag = _hangMoving;
			break;
		case Job.Gnome:
			flag = _bot.HeldView == 0;
			break;
		case Job.Order:
			flag = _orderMoving;
			break;
		default:
			flag = _jobWantsToMove && _carry != Carry.Deliver && _carryProblemSince < 0f && Time.time - _grabbedAt > 1.2f;
			break;
		}
		if (flag)
		{
			val = _bot.Position - _progressPos;
			if (!(val.sqrMagnitude > 1f))
			{
				if (Time.time - _progressAt < 6f)
				{
					return;
				}
				ResetProgress();
				if (_job == Job.Order)
				{
					_corners.Clear();
					if (++_orderStuck >= 3)
					{
						Plugin.Log.LogInfo((object)(_bot.Name + " can't get there, giving up the order"));
						Say(Line("cantreach"), 1);
						ClearOrder(say: false);
					}
				}
				else if (_job == Job.Follow && (Object)(object)_leader != (Object)null)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " got stuck following, looking for another way"));
					_corners.Clear();
					if (!SeesPerson(_leader))
					{
						LostPerson(_leader);
						_leader = null;
					}
				}
				else if (_job != Job.Flee)
				{
					if ((Object)(object)_target != (Object)null && (_job == Job.Haul || _job == Job.Revive))
					{
						Plugin.Log.LogInfo((object)string.Format("{0} carry state: {1}, holding {2}, item {3:0.0}m from hands, path {4}", _bot.Name, _carry, _bot.HeldView != 0, Vector3.Distance(_target.centerPoint, HoldPoint()), _pathFailed ? "none" : (_pathPartial ? "partial" : (_corners.Count + " corners"))));
					}
					Plugin.Log.LogInfo((object)($"{_bot.Name} got stuck, trying something else ({_job}, at {_bot.Position.x:0.0},{_bot.Position.y:0.0},{_bot.Position.z:0.0}" + ((_job == Job.Truck && (Object)(object)TruckSafetySpawnPoint.instance != (Object)null) ? $", {Vector3.Distance(_bot.Position, ((Component)TruckSafetySpawnPoint.instance).transform.position):0.0}m from the truck spot" : "") + ")"));
					Failed(_target, 45f);
					if ((Object)(object)_explore != (Object)null)
					{
						Visited.Add(_explore);
					}
					_explore = null;
					if ((Object)(object)_target != (Object)null)
					{
						Blacklist[_target] = Time.time + 45f;
					}
					if ((Object)(object)_pushing != (Object)null)
					{
						Blacklist[_pushing.physGrabObject] = Time.time + 30f;
					}
					DropJob("CheckStuck", 3719);
				}
				return;
			}
		}
		ResetProgress();
	}

	private void ResetProgress()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		_progressPos = _bot.Position;
		_progressAt = Time.time;
	}

	private void TeleportNear(Vector3 center, Vector3 forward)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if (_bot.HeldView != 0)
		{
			_dropNow = true;
			ReleaseHeld("TeleportNear", 3743);
			_dropNow = false;
		}
		Vector3 position = FindSpotNear(center - forward * 1.5f, 0.5f, 2.5f);
		_bot.Position = position;
		_bot.Velocity = Vector3.zero;
		_corners.Clear();
		ResetProgress();
	}

	private Vector3 Eye()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = _bot.Position;
		Vector3 up = Vector3.up;
		float num;
		if (_posture == 2)
		{
			num = 0.45f;
		}
		else
		{
			num = ((_posture == 1) ? 1f : 1.5f);
		}
		return position + up * num;
	}

	private static Vector3 Flat(Vector3 v)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		return new Vector3(v.x, 0f, v.z);
	}

	private bool ClearLine(Vector3 from, Vector3 to, PhysGrabObject target, Transform targetRoot)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = to - from;
		float magnitude = val.magnitude;
		if (magnitude < 0.01f)
		{
			return true;
		}
		int num = (int)SemiFunc.LayerMaskGetVisionObstruct() & ~LayerMask.GetMask(new string[1] { "Player" });
		int num2 = Physics.RaycastNonAlloc(from, val / magnitude, Hits, magnitude, num, (QueryTriggerInteraction)1);
		Transform root = ((Component)_bot.Avatar).transform.root;
		for (int i = 0; i < num2; i++)
		{
			Transform transform = ((Component)Hits[i].collider).transform;
			if (!((Object)(object)transform.root == (Object)(object)root) && (!((Object)(object)target != (Object)null) || !transform.IsChildOf(((Component)target).transform)) && (!((Object)(object)targetRoot != (Object)null) || !transform.IsChildOf(targetRoot)) && !(Hits[i].distance > magnitude - 0.3f))
			{
				return false;
			}
		}
		return true;
	}

	public static Vector3 FindSpotNear(Vector3 center, float minR, float maxR)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		NavMeshHit val2 = default;
		for (int i = 0; i < 16; i++)
		{
			Vector2 insideUnitCircle = Random.insideUnitCircle;
			Vector2 val = insideUnitCircle.normalized * Random.Range(minR, maxR);
			if (NavMesh.SamplePosition(center + new Vector3(val.x, 0f, val.y), out val2, 1.5f, -1) && !(Mathf.Abs(val2.position.y - center.y) > 1.5f) && !Physics.Linecast(center + Vector3.up * 1f, val2.position + Vector3.up * 1f, GroundMask, (QueryTriggerInteraction)1) && !Physics.CheckCapsule(val2.position + Vector3.up * 0.72f, val2.position + Vector3.up * 1.3800001f, 0.32f, MoveMask, (QueryTriggerInteraction)1))
			{
				return val2.position;
			}
		}
		NavMeshHit val3 = default;
		if (NavMesh.SamplePosition(center, out val3, 3f, -1))
		{
			return val3.position;
		}
		return center;
	}

	private static float NormalizeAngle(float a)
	{
		a %= 360f;
		if (a > 180f)
		{
			a -= 360f;
		}
		if (a < -180f)
		{
			a += 360f;
		}
		return a;
	}

	private static void ResetCartStatics()
	{
		_driver = null;
		_escort = null;
		HumanTouchedAt.Clear();
	}

	private static bool PersonHolds(PhysGrabCart c)
	{
		if ((Object)(object)c == (Object)null || (Object)(object)c.physGrabObject == (Object)null)
		{
			return false;
		}
		foreach (PhysGrabber item in c.physGrabObject.playerGrabbing)
		{
			if ((Object)(object)item != (Object)null && !BotRegistry.IsBot(item.playerAvatar))
			{
				return true;
			}
		}
		return false;
	}

	private static bool PeopleOwn(PhysGrabCart c)
	{
		if (PersonHolds(c))
		{
			HumanTouchedAt[c] = Time.time;
			return true;
		}
		if (HumanTouchedAt.TryGetValue(c, out var value))
		{
			return Time.time - value < 25f;
		}
		return false;
	}

	private static float IdleFor(PhysGrabCart c)
	{
		if (!HumanTouchedAt.TryGetValue(c, out var value))
		{
			return Time.timeSinceLevelLoad;
		}
		return Time.time - value;
	}

	private PhysGrabCart NearestKnownCart()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabCart result = null;
		float num = float.MaxValue;
		foreach (PhysGrabCart cart in Carts)
		{
			if (!((Object)(object)cart == (Object)null) && !((Object)(object)cart.physGrabObject == (Object)null) && !((Object)(object)cart.inCart == (Object)null) && _thingPos.TryGetValue(cart.physGrabObject, out var value))
			{
				float num2 = Vector3.Distance(value, _bot.Position);
				if (num2 < 70f && num2 < num)
				{
					num = num2;
					result = cart;
				}
			}
		}
		return result;
	}

	private bool TryStartDrive()
	{
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0310: Unknown result type (might be due to invalid IL or missing references)
		//IL_0311: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsUseCarts.Value || !SemiFunc.RunIsLevel())
		{
			return false;
		}
		if (_driver != null && ((Object)(object)_driver._bot.Avatar == (Object)null || _driver._bot.Avatar.isDisabled))
		{
			_driver = null;
		}
		if (_cartFan < 0)
		{
			_cartFan = ((Random.value < Plugin.CartFanChance.Value) ? 1 : 0);
			if (_cartFan == 1)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " really wants the cart this round"));
			}
		}
		PhysGrabCart val = NearestKnownCart();
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		if (PeopleOwn(val))
		{
			if (_driver == this)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " is leaving the cart to the people"));
				_driver = null;
			}
			return false;
		}
		if (_driver == null)
		{
			if (BotRegistry.Bots.Count < 2)
			{
				return false;
			}
			if (_cartFan != 1 && !(IdleFor(val) > 45f))
			{
				return false;
			}
			_driver = this;
			Plugin.Log.LogInfo((object)(_bot.Name + " is taking charge of the cart"));
			Say(Line("driver"), 0, "driver", 300f);
		}
		if (_driver != this)
		{
			return false;
		}
		if (GuardCart(val))
		{
			return true;
		}
		if (Time.time < _cartDecideAt)
		{
			return false;
		}
		_cartDecideAt = Time.time + 2f;
		ExtractionPoint val2 = ActiveExtraction();
		bool num = (Object)(object)val.physGrabObject.roomVolumeCheck != (Object)null && val.physGrabObject.roomVolumeCheck.inExtractionPoint;
		int itemsInCartCount = val.itemsInCartCount;
		Vector3 position = ((Component)val).transform.position;
		if (num)
		{
			bool flag = (Object)(object)val2 != (Object)null && Vector3.Distance(ExtractionSpot(val2), position) < 6f;
			if (itemsInCartCount == 0 || !flag)
			{
				Vector3 dest = LootZone() ?? AwayFrom(position, 10f);
				return StartPush(val, CartGoal.OutOfExtraction, dest, "is wheeling the cart back out of extraction");
			}
			return false;
		}
		string text = CartHasHead(val);
		if ((Object)(object)val2 != (Object)null && text != null)
		{
			return StartPush(val, CartGoal.Extraction, ExtractionSpot(val2), "is rushing the cart to extraction with " + text + "'s head in it");
		}
		if ((Object)(object)val2 != (Object)null && itemsInCartCount > 0 && (itemsInCartCount >= 5 || !LootNear(position, 12f)))
		{
			bool flag2 = StartPush(val, CartGoal.Extraction, ExtractionSpot(val2), $"is pushing the cart to extraction (${val.haulCurrent})");
			if (flag2)
			{
				Say(Line("cartin", null, null, null, Money(val.haulCurrent)), 1, "cartin", 60f);
			}
			return flag2;
		}
		if (itemsInCartCount < 5 && Time.time - _cartParkedAt > 20f)
		{
			Vector3? val3 = LootZone();
			if (val3.HasValue)
			{
				Vector3 val4 = Flat(val3.Value - position);
				if (val4.magnitude > 12f)
				{
					return StartPush(val, CartGoal.LootZone, val3.Value, "is bringing the cart over to the loot");
				}
			}
		}
		return false;
	}

	private bool StartPush(PhysGrabCart cart, CartGoal goal, Vector3 dest, string what)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (!Available(cart.physGrabObject) || PersonNear(((Component)cart).transform.position, 3f))
		{
			return false;
		}
		if (ThreatNear(((Component)cart).transform.position, 7f) != null || (goal != CartGoal.Extraction && !SafeSpot(dest)))
		{
			return false;
		}
		_detour = null;
		_routeWaitSince = -1f;
		_routeLogged = false;
		DropJob("StartPush", 184);
		_cartForPerson = false;
		_job = Job.Cart;
		_pushing = cart;
		_cartGoal = goal;
		_cartDest = dest;
		_carry = Carry.Approach;
		_jobStarted = Time.time;
		Claims[cart.physGrabObject] = this;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " " + what));
		return true;
	}

	private void CartArrived(PhysGrabCart c)
	{
		Plugin.Log.LogInfo((object)((_cartGoal == CartGoal.OutOfExtraction) ? (_bot.Name + " got the cart back out") : (_bot.Name + " parked the cart by the loot")));
		_cartParkedAt = Time.time;
		ReleaseHeld("CartArrived", 206);
		if (_cartForPerson)
		{
			_cartForPerson = false;
			HumanTouchedAt[c] = Time.time + 60f;
			if (_driver == this)
			{
				_driver = null;
			}
			Plugin.Log.LogInfo((object)(_bot.Name + " left the cart with the person who asked for it"));
		}
		DropJob("CartArrived", 216);
	}

	private Vector3? LootZone()
	{
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)ValuableDirector.instance == (Object)null)
		{
			return null;
		}
		List<Vector3> list = new List<Vector3>();
		List<float> list2 = new List<float>();
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if (!((Object)(object)valuable == (Object)null) && Seen.Contains(valuable) && !((Object)(object)valuable.physGrabObject == (Object)null) && !valuable.physGrabObject.dead)
			{
				PhysGrabObject physGrabObject = valuable.physGrabObject;
				if ((!((Object)(object)physGrabObject.roomVolumeCheck != (Object)null) || (!physGrabObject.roomVolumeCheck.inExtractionPoint && !physGrabObject.roomVolumeCheck.inTruck)) && (!Blacklist.TryGetValue(physGrabObject, out var value) || !(value > Time.time + 1000f)) && !InSomeCart(physGrabObject) && _thingPos.TryGetValue(physGrabObject, out var value2))
				{
					list.Add(value2);
					list2.Add(1f + Mathf.Min(valuable.dollarValueCurrent / 2000f, 3f));
				}
			}
		}
		float num = 0f;
		Vector3 val = Vector3.zero;
		for (int i = 0; i < list.Count; i++)
		{
			float num2 = 0f;
			Vector3 val2 = Vector3.zero;
			float num3 = 0f;
			for (int j = 0; j < list.Count; j++)
			{
				if (!(Vector3.Distance(list[i], list[j]) > 8f) && !(Mathf.Abs(list[i].y - list[j].y) > 2.5f))
				{
					num2 += list2[j];
					val2 += list[j];
					num3++;
				}
			}
			if (num2 > num)
			{
				num = num2;
				val = val2 / num3;
			}
		}
		if (num < 2f)
		{
			return null;
		}
		NavMeshHit val3 = default;
		if (!NavMesh.SamplePosition(val, out val3, 4f, -1) || !SafeSpot(val3.position))
		{
			return null;
		}
		return val3.position;
	}

	private bool LootNear(Vector3 p, float range)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if (!((Object)(object)valuable == (Object)null) && Seen.Contains(valuable) && !((Object)(object)valuable.physGrabObject == (Object)null) && Available(valuable.physGrabObject) && !InSomeCart(valuable.physGrabObject) && _thingPos.TryGetValue(valuable.physGrabObject, out var value) && Vector3.Distance(value, p) < range)
			{
				return true;
			}
		}
		return false;
	}

	private static Vector3 AwayFrom(Vector3 p, float dist)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		NavMeshHit val2 = default;
		for (int i = 0; i < 12; i++)
		{
			Vector3 val = Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward;
			if (NavMesh.SamplePosition(p + val * dist, out val2, 3f, -1))
			{
				return val2.position;
			}
		}
		return p;
	}

	private Threat ThreatNear(Vector3 p, float range)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		Threat result = null;
		float num = range;
		foreach (Threat item in KnownThreats())
		{
			if (item.Rule != ThreatRule.Harmless || item.Aggressive)
			{
				float num2 = Vector3.Distance(item.Position, p);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	private Vector3? SaferCartSpot(Vector3 threat)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		Vector3? result = LootZone();
		if (result.HasValue && Vector3.Distance(result.Value, threat) > 14f && SafeSpot(result.Value))
		{
			return result;
		}
		Vector3 val = Flat(_bot.Position - threat);
		if (val.sqrMagnitude < 0.01f)
		{
			val = Vector3.forward;
		}
		val.Normalize();
		NavMeshHit val3 = default;
		for (int i = 0; i < 10; i++)
		{
			Vector3 val2 = Quaternion.Euler(0f, (float)((i % 2 == 0) ? 1 : (-1)) * 25f * (float)((i + 1) / 2), 0f) * val;
			if (NavMesh.SamplePosition(threat + val2 * 15f, out val3, 3f, -1) && SafeSpot(val3.position))
			{
				return val3.position;
			}
		}
		return null;
	}

	private bool GuardCart(PhysGrabCart cart)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)cart).transform.position;
		Threat threat = ThreatNear(position, 7f);
		if (threat != null)
		{
			_cartThreatAt = Time.time;
			_cartThreatPos = threat.Position;
			if (!_guarding)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " is keeping clear of the cart, " + threat.Kind + " is at it"));
				_guarding = true;
				_guardSpot = null;
			}
			if (!_guardSpot.HasValue || Vector3.Distance(_guardSpot.Value, threat.Position) < 8f)
			{
				_guardSpot = FleeSpot(threat.Position);
			}
			_job = Job.Guard;
			return true;
		}
		if (!_guarding)
		{
			return false;
		}
		if (Time.time - _cartThreatAt < 3f)
		{
			_job = Job.Guard;
			return true;
		}
		_guarding = false;
		_guardSpot = null;
		Vector3? val = SaferCartSpot(_cartThreatPos);
		if (val.HasValue)
		{
			Vector3 val2 = Flat(val.Value - position);
			if (val2.magnitude > 6f)
			{
				return StartPush(cart, CartGoal.LootZone, val.Value, "is moving the cart somewhere safer");
			}
		}
		return false;
	}

	private Vector3 DoGuard(out Vector3? lookAt)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		PhysGrabCart val = NearestKnownCart();
		if ((Object)(object)val != (Object)null)
		{
			lookAt = ((Component)val).transform.position + Vector3.up * 0.5f;
		}
		if (_guardSpot.HasValue)
		{
			Vector3 val2 = Flat(_guardSpot.Value - _bot.Position);
			if (!(val2.magnitude < 1f))
			{
				return Steer(_bot.Position, _guardSpot.Value) * 3.2f;
			}
		}
		return Vector3.zero;
	}

	private Threat RouteThreat(Vector3 pos, Vector3 cartPos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		Threat threat = ThreatNear(cartPos, 6f);
		if (threat != null)
		{
			return threat;
		}
		Vector3 val = pos;
		float num = 0f;
		for (int i = _corner; i < _corners.Count; i++)
		{
			if (!(num < 15f))
			{
				break;
			}
			Vector3 val2 = _corners[i];
			foreach (Threat item in KnownThreats())
			{
				if ((item.Rule != ThreatRule.Harmless || item.Aggressive) && DistanceToSegment(item.Position, val, val2) < 5f)
				{
					return item;
				}
			}
			num += Vector3.Distance(val, val2);
			val = val2;
		}
		return null;
	}

	private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = b - a;
		float sqrMagnitude = val.sqrMagnitude;
		float num = ((sqrMagnitude < 1E-06f) ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, val) / sqrMagnitude));
		return Vector3.Distance(p, a + val * num);
	}

	private Vector3? DetourAround(Vector3 threat, Vector3 pos, Vector3 dest)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		Vector3? result = null;
		float num = float.MaxValue;
		NavMeshHit val2 = default;
		for (int i = 0; i < 12; i++)
		{
			Vector3 val = Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward;
			if (NavMesh.SamplePosition(threat + val * 9f, out val2, 2.5f, -1) && !(Vector3.Distance(val2.position, threat) < 7f) && SafeSpot(val2.position))
			{
				float num2 = Vector3.Distance(pos, val2.position) + Vector3.Distance(val2.position, dest);
				if (num2 < num)
				{
					num = num2;
					result = val2.position;
				}
			}
		}
		return result;
	}

	private Vector3? PushAroundMonsters(Vector3 pos, PhysGrabCart c, Vector3 dest, ref Vector3? lookAt)
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		if (_detour.HasValue)
		{
			Vector3 val = Flat(_detour.Value - pos);
			if (!(val.magnitude < 2f))
			{
				return Steer(pos, _detour.Value) * 2.2f;
			}
			_detour = null;
		}
		Threat threat = RouteThreat(pos, ((Component)c).transform.position);
		if (threat == null)
		{
			_routeWaitSince = -1f;
			_routeLogged = false;
			return null;
		}
		if (_routeWaitSince < 0f)
		{
			_routeWaitSince = Time.time;
		}
		_detour = DetourAround(threat.Position, pos, dest);
		if (!_routeLogged)
		{
			_routeLogged = true;
			Plugin.Log.LogInfo((object)(_bot.Name + " is " + (_detour.HasValue ? "taking the cart around" : "waiting with the cart for") + " " + threat.Kind));
		}
		if (_detour.HasValue)
		{
			return Steer(pos, _detour.Value) * 2.2f;
		}
		lookAt = ((Component)c).transform.position;
		if (Time.time - _routeWaitSince > 15f)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " gave up pushing the cart past " + threat.Kind));
			ReleaseHeld("PushAroundMonsters", 476);
			DropJob("PushAroundMonsters", 477);
		}
		return Vector3.zero;
	}

	private void ShoveCartAway(Vector3 threat)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		if (_job != Job.Cart || (Object)(object)_pushing == (Object)null || _bot.HeldView == 0)
		{
			return;
		}
		Rigidbody val = (((Object)(object)_pushing.rb != (Object)null) ? _pushing.rb : _pushing.physGrabObject.rb);
		if (!((Object)(object)val == (Object)null) && !val.isKinematic)
		{
			Vector3 val2 = Flat(((Component)_pushing).transform.position - threat);
			if (val2.sqrMagnitude < 0.01f)
			{
				val2 = Flat(((Component)_pushing).transform.position - _bot.Position);
			}
			val2.Normalize();
			val.AddForce(val2 * 4f + Vector3.up * 0.5f, (ForceMode)2);
			Plugin.Log.LogInfo((object)(_bot.Name + " shoved the cart clear before running"));
		}
	}

	private bool TryStartEscort()
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsUseCarts.Value || !SemiFunc.RunIsLevel())
		{
			return false;
		}
		if (_escort != null && _escort != this)
		{
			if ((Object)(object)_escort._bot.Avatar != (Object)null && !_escort._bot.Avatar.isDisabled && (_escort._job == Job.Escort || _escort._escortHaul))
			{
				return false;
			}
			_escort = null;
		}
		foreach (PhysGrabCart cart in Carts)
		{
			if (!((Object)(object)cart == (Object)null) && PersonHolds(cart) && _thingPos.TryGetValue(cart.physGrabObject, out var value) && !(Vector3.Distance(value, _bot.Position) > 25f))
			{
				if (_job != Job.Escort)
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " is going along with the cart"));
				}
				DropJob("TryStartEscort", 520);
				_escort = this;
				_escortCart = cart;
				_job = Job.Escort;
				_jobStarted = Time.time;
				_escortLostAt = -1f;
				_cartStillSince = -1f;
				_corners.Clear();
				return true;
			}
		}
		return false;
	}

	private bool EscortStillNeeded()
	{
		if ((Object)(object)_escortCart == (Object)null)
		{
			return false;
		}
		if (PersonHolds(_escortCart))
		{
			_escortLostAt = -1f;
			return true;
		}
		if (_escortLostAt < 0f)
		{
			_escortLostAt = Time.time;
		}
		return Time.time - _escortLostAt < 8f;
	}

	private Vector3 DoEscort(out bool sprint, out Vector3? lookAt)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		sprint = false;
		lookAt = null;
		PhysGrabCart escortCart = _escortCart;
		if (!EscortStillNeeded())
		{
			_escortCart = null;
			DropJob("DoEscort", 557);
			return Vector3.zero;
		}
		Vector3 position = ((Component)escortCart).transform.position;
		_thingPos[escortCart.physGrabObject] = position;
		float num;
		Vector3 val;
		if (!((Object)(object)escortCart.rb != (Object)null))
		{
			num = 0f;
		}
		else
		{
			val = Flat(escortCart.rb.velocity);
			num = val.magnitude;
		}
		if (num < 0.3f)
		{
			if (_cartStillSince < 0f)
			{
				_cartStillSince = Time.time;
			}
		}
		else
		{
			_cartStillSince = -1f;
		}
		if (_cartStillSince > 0f && Time.time - _cartStillSince > 1.5f && _bot.HeldView == 0)
		{
			PhysGrabObject val2 = NearbyLootFor(position, 9f);
			if ((Object)(object)val2 != (Object)null)
			{
				StartCarry(Job.Haul, val2, escortCart);
				_escortHaul = true;
				Plugin.Log.LogInfo((object)(_bot.Name + " is putting " + ((Object)val2).name + " in the cart"));
				return Vector3.zero;
			}
		}
		Vector3 val3 = Flat(_bot.Position - position);
		if (val3.sqrMagnitude < 0.01f)
		{
			val3 = ((Component)escortCart).transform.right;
		}
		Vector3 val4 = position + val3.normalized * 2.8f;
		lookAt = position + Vector3.up * 0.5f;
		val = Flat(val4 - _bot.Position);
		float magnitude = val.magnitude;
		if (magnitude < 0.8f)
		{
			return Vector3.zero;
		}
		sprint = magnitude > 8f;
		return Steer(_bot.Position, val4) * (sprint ? 5.6f : 3.2f);
	}

	private PhysGrabObject NearbyLootFor(Vector3 p, float range)
	{
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabObject result = null;
		float num = float.MaxValue;
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if ((Object)(object)valuable == (Object)null || !Seen.Contains(valuable) || (Object)(object)valuable.physGrabObject == (Object)null)
			{
				continue;
			}
			PhysGrabObject physGrabObject = valuable.physGrabObject;
			if (Available(physGrabObject) && !((Object)(object)physGrabObject.rb == (Object)null) && !physGrabObject.rb.isKinematic && !IsHeavy(physGrabObject, HeavyLimit) && !InSomeCart(physGrabObject) && (!((Object)(object)physGrabObject.roomVolumeCheck != (Object)null) || (!physGrabObject.roomVolumeCheck.inExtractionPoint && !physGrabObject.roomVolumeCheck.inTruck)) && _thingPos.TryGetValue(physGrabObject, out var value))
			{
				float num2 = Vector3.Distance(value, p);
				if (num2 < range && num2 < num)
				{
					num = num2;
					result = physGrabObject;
				}
			}
		}
		return result;
	}

	private void MeasureCart(PhysGrabCart c)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_cartMeasured == (Object)(object)c)
		{
			return;
		}
		_cartMeasured = c;
		bool flag = false;
		Bounds val = default;
		Collider[] componentsInChildren = ((Component)c).GetComponentsInChildren<Collider>();
		foreach (Collider val2 in componentsInChildren)
		{
			if ((Object)(object)val2 == (Object)null || val2.isTrigger || !val2.enabled)
			{
				continue;
			}
			Bounds bounds = val2.bounds;
			for (int j = 0; j < 8; j++)
			{
				Vector3 val3 = new Vector3(((j & 1) == 0) ? bounds.min.x : bounds.max.x, ((j & 2) == 0) ? bounds.min.y : bounds.max.y, ((j & 4) == 0) ? bounds.min.z : bounds.max.z);
				Vector3 val4 = ((Component)c).transform.InverseTransformPoint(val3);
				if (!flag)
				{
					val = new Bounds(val4, Vector3.zero);
					flag = true;
				}
				else
				{
					val.Encapsulate(val4);
				}
			}
		}
		if (!flag)
		{
			val = new Bounds(Vector3.up * 0.5f, new Vector3(1f, 1f, 1.4f));
		}
		_cartHalf = val.extents * 0.9f;
		_cartCenterLocal = val.center;
	}

	private bool CartFits(PhysGrabCart c, Vector3 dir, float dist)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = ((Component)c).transform.TransformPoint(_cartCenterLocal) + Vector3.up * 0.12f;
		Vector3 val2 = new Vector3(_cartHalf.x, Mathf.Max(0.05f, _cartHalf.y - 0.15f), _cartHalf.z);
		int num = Physics.BoxCastNonAlloc(val, val2, dir, Hits, ((Component)c).transform.rotation, dist, CartWallMask, (QueryTriggerInteraction)1);
		for (int i = 0; i < num; i++)
		{
			Collider collider = Hits[i].collider;
			if (!((Object)(object)collider == (Object)null) && !(Hits[i].distance <= 0f) && !Ignored(collider) && !((Component)collider).transform.IsChildOf(((Component)c).transform))
			{
				return false;
			}
		}
		return true;
	}

	private Vector3 PathAhead(Vector3 pos, Vector3 dest, float dist)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = pos;
		Vector3 val2 = dest;
		float num = dist;
		Vector3 val4;
		for (int i = _corner; i < _corners.Count; i++)
		{
			Vector3 val3 = _corners[i];
			val4 = Flat(val3 - val);
			float magnitude = val4.magnitude;
			if (magnitude >= num)
			{
				val2 = val + (val3 - val) * (num / Mathf.Max(magnitude, 0.001f));
				num = 0f;
				break;
			}
			num -= magnitude;
			val = val3;
		}
		if (num > 0f && _corners.Count > 0)
		{
			val2 = _corners[_corners.Count - 1];
		}
		float num2 = Mathf.Max(0.6f, _cartHalf.x + 0.25f);
		NavMeshHit val5 = default;
		if (NavMesh.FindClosestEdge(val2, out val5, -1) && val5.distance < num2)
		{
			Vector3 val6 = Flat(val2 - val5.position);
			Vector3 normalized;
			if (!(val6.sqrMagnitude > 0.0025f))
			{
				val4 = Flat(val5.normal);
				normalized = val4.normalized;
			}
			else
			{
				normalized = val6.normalized;
			}
			val6 = normalized;
			val2 += val6 * (num2 - val5.distance);
		}
		return val2;
	}

	private bool CartHeldRight(PhysGrabCart c)
	{
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabber physGrabber = _bot.Avatar.physGrabber;
		if ((Object)(object)physGrabber == (Object)null || Time.time - _grabbedAt < 1.2f)
		{
			return true;
		}
		PhysGrabObjectGrabArea physGrabObjectGrabArea = c.physGrabObjectGrabArea;
		if (!c.cartActive || !((Object)(object)physGrabObjectGrabArea != (Object)null) || physGrabObjectGrabArea.listOfAllGrabbers.Count <= 0 || !((Object)(object)physGrabObjectGrabArea.listOfAllGrabbers[0] == (Object)(object)physGrabber))
		{
			if (_cartModeBadSince < 0f)
			{
				_cartModeBadSince = Time.time;
			}
			else if (Time.time - _cartModeBadSince > 0.8f)
			{
				_cartModeBadSince = -1f;
				bool flag = (Object)(object)physGrabObjectGrabArea != (Object)null && physGrabObjectGrabArea.listOfAllGrabbers.Contains(physGrabber);
				Plugin.Log.LogInfo((object)$"{_bot.Name} is dragging the cart instead of pushing it (handle mode {c.cartActive}, on handle {flag}): grabbing the handle again");
				ReleaseHeld("CartHeldRight", 120);
				_carry = Carry.Approach;
				_jobStarted = Time.time;
				return false;
			}
		}
		else
		{
			_cartModeBadSince = -1f;
		}
		if (Mathf.Abs(Mathf.DeltaAngle(((Component)physGrabber).transform.eulerAngles.y, _bot.Yaw)) > 25f)
		{
			if (_cartAimBadSince < 0f)
			{
				_cartAimBadSince = Time.time;
			}
			else if (Time.time - _cartAimBadSince > 0.5f)
			{
				if (!_cartAimLogged)
				{
					_cartAimLogged = true;
					Plugin.Log.LogInfo((object)string.Format("{0}'s grabber faces {1:0} but the bot faces {2:0} ({3} under {4}): turning it", _bot.Name, ((Component)physGrabber).transform.eulerAngles.y, _bot.Yaw, ((Object)((Component)physGrabber).transform).name, ((Object)(object)((Component)physGrabber).transform.parent != (Object)null) ? ((Object)((Component)physGrabber).transform.parent).name : "-"));
				}
				((Component)physGrabber).transform.rotation = Quaternion.Euler(0f, _bot.Yaw, 0f);
			}
		}
		else
		{
			_cartAimBadSince = -1f;
		}
		return true;
	}

	private Vector3 DriveCart(PhysGrabCart c, Vector3 dest, ref Vector3? lookAt)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_031e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02be: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02da: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02dc: Unknown result type (might be due to invalid IL or missing references)
		if (!CartHeldRight(c))
		{
			return Vector3.zero;
		}
		Vector3 position = _bot.Position;
		MeasureCart(c);
		Vector3 val = Flat(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward);
		Vector3 normalized = val.normalized;
		Vector3 val2 = Eye();
		if (Time.time < _cartBackUntil)
		{
			val = normalized + _cartBackTurn * 0.35f;
			lookAt = val2 + val.normalized * 4f;
			return -normalized * 1.1f;
		}
		Vector3 val3 = Steer(position, dest);
		Vector3 val4 = Flat(PathAhead(position, dest, 2.2f) - position);
		if (val4.sqrMagnitude < 0.04f)
		{
			val4 = val3;
		}
		if (val4.sqrMagnitude < 0.0001f)
		{
			return Vector3.zero;
		}
		val4.Normalize();
		Vector3 val5 = val4;
		bool flag = false;
		float[] array = new float[7] { 0f, 15f, -15f, 30f, -30f, 50f, -50f };
		foreach (float num in array)
		{
			Vector3 val6 = Quaternion.Euler(0f, num, 0f) * val4;
			if (CartFits(c, val6, 0.7f))
			{
				val5 = val6;
				flag = true;
				break;
			}
		}
		lookAt = val2 + val5 * 4f;
		float num2 = Vector3.Angle(normalized, val5);
		float num3 = 2.2f * Mathf.Lerp(1f, 0.65f, P.Careful);
		Vector3 result = ((flag && !(num2 > 35f)) ? (normalized * num3 * ((num2 > 15f) ? 0.45f : 1f)) : Vector3.zero);
		float num4;
		if (!((Object)(object)c.physGrabObject.rb != (Object)null))
		{
			num4 = 1f;
		}
		else
		{
			val = Flat(c.physGrabObject.rb.velocity);
			num4 = val.magnitude;
		}
		float num5 = num4;
		if ((result.sqrMagnitude > 0.1f || !flag) && num5 < 0.15f && Time.time - _grabbedAt > 2f)
		{
			if (_cartBlockedSince < 0f)
			{
				_cartBlockedSince = Time.time;
			}
			else if (Time.time - _cartBlockedSince > 1.2f)
			{
				_cartBlockedSince = -1f;
				_cartBackUntil = Time.time + 1.1f;
				Vector3 val7 = Vector3.Cross(Vector3.up, normalized);
				_cartBackTurn = (CartFits(c, val7, 0.5f) ? val7 : (-val7));
				_corners.Clear();
				ResetProgress();
				Plugin.Log.LogInfo((object)(_bot.Name + " is backing the cart up, it's caught on something"));
			}
		}
		else
		{
			_cartBlockedSince = -1f;
		}
		return result;
	}

	private bool DoorTakenByOther(PhysGrabHinge h)
	{
		if (DoorClaims.TryGetValue(h, out var value) && value != null && value != this && value._door != null && (Object)(object)value._door.Hinge == (Object)(object)h)
		{
			return value.Alive;
		}
		return false;
	}

	private bool BackThroughDoor(Vector3 walk, ref Vector3 desired, ref Vector3? lookAt)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0191: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabHinge backDoor = _backDoor;
		Vector3 val = Flat(walk);
		if ((Object)(object)backDoor == (Object)null || (Object)(object)backDoor.physGrabObject == (Object)null || backDoor.broken || backDoor.dead || _bot.HeldView == 0 || Time.time > _backUntil || val.sqrMagnitude < 0.25f)
		{
			_backDoor = null;
			return false;
		}
		if ((Object)(object)DoorAhead(walk) != (Object)(object)backDoor)
		{
			if (_backClearSince < 0f)
			{
				_backClearSince = Time.time;
			}
			else if (Time.time - _backClearSince > 0.8f)
			{
				_backDoor = null;
				return false;
			}
		}
		else
		{
			_backClearSince = -1f;
		}
		Vector3 normalized = val.normalized;
		Vector3 val2 = Eye();
		lookAt = val2 - normalized * 4f;
		if (Vector3.Dot(Flat(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward), normalized) > -0.5f)
		{
			desired = Vector3.zero;
			return true;
		}
		desired = normalized * Mathf.Min(val.magnitude, 1.9200001f);
		Collider componentInChildren = ((Component)backDoor).GetComponentInChildren<Collider>();
		if ((Object)(object)componentInChildren != (Object)null && Time.time >= _backPushAt)
		{
			Vector3 val3 = _bot.Position + Vector3.up * 0.9f;
			Vector3 val4 = componentInChildren.ClosestPoint(val3) - val3;
			if (val4.magnitude < 0.9f)
			{
				_backPushAt = Time.time + 0.2f;
				Shoulder(backDoor, walk);
			}
		}
		return true;
	}

	private PhysGrabHinge DoorAhead(Vector3 walk)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(walk);
		if (val.sqrMagnitude < 0.5f)
		{
			return null;
		}
		val.Normalize();
		int num = Physics.SphereCastNonAlloc(_bot.Position + Vector3.up * 0.9f, 0.25f, val, DoorHits, 1.3f, HingeMask, (QueryTriggerInteraction)1);
		for (int i = 0; i < num; i++)
		{
			PhysGrabHinge componentInParent = ((Component)DoorHits[i].collider).GetComponentInParent<PhysGrabHinge>();
			if (!((Object)(object)componentInParent == (Object)null) && !((Object)(object)componentInParent.physGrabObject == (Object)null) && !((Object)(object)componentInParent.joint == (Object)null) && !componentInParent.broken && !componentInParent.dead && !BoxOf.ContainsKey(componentInParent) && (!_doorRest.TryGetValue(componentInParent, out var value) || !(Time.time < value)) && !(Mathf.Abs(componentInParent.joint.angle) > 50f))
			{
				return componentInParent;
			}
		}
		return null;
	}

	private bool DoorTick(Vector3 walk, ref Vector3 desired, ref Vector3? lookAt)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_0338: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03da: Unknown result type (might be due to invalid IL or missing references)
		//IL_03df: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0433: Unknown result type (might be due to invalid IL or missing references)
		//IL_0435: Unknown result type (might be due to invalid IL or missing references)
		//IL_0497: Unknown result type (might be due to invalid IL or missing references)
		//IL_049c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_044d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_0457: Unknown result type (might be due to invalid IL or missing references)
		//IL_0480: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Unknown result type (might be due to invalid IL or missing references)
		//IL_0469: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0472: Unknown result type (might be due to invalid IL or missing references)
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		//IL_048a: Unknown result type (might be due to invalid IL or missing references)
		//IL_048f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_backDoor != (Object)null)
		{
			return BackThroughDoor(walk, ref desired, ref lookAt);
		}
		if (Time.time < _doorWaitUntil)
		{
			Vector3 val = Flat(_bot.Position - _doorWaitFrom);
			desired = ((val.magnitude < 1.3f && val.sqrMagnitude > 0.0001f) ? (val.normalized * 1.2f) : Vector3.zero);
			lookAt = _doorWaitFrom + Vector3.up * 1.2f;
			return true;
		}
		if (_door == null)
		{
			if (Time.time < _doorCheckAt || Typing)
			{
				return false;
			}
			_doorCheckAt = Time.time + 0.2f;
			PhysGrabHinge val2 = DoorAhead(walk);
			if ((Object)(object)val2 == (Object)null)
			{
				return false;
			}
			if (_job == Job.Flee || Time.time < _escapeUntil)
			{
				Shoulder(val2, walk, 3f);
				return false;
			}
			if (_bot.HeldView != 0 || _bot.HoldingStatic)
			{
				if (_job == Job.Cart || _bot.HoldingStatic)
				{
					Shoulder(val2, walk);
					return false;
				}
				Plugin.Log.LogInfo((object)(_bot.Name + " is backing through a door with the loot"));
				_backDoor = val2;
				_backUntil = Time.time + 5f;
				_backClearSince = -1f;
				return BackThroughDoor(walk, ref desired, ref lookAt);
			}
			if (DoorTakenByOther(val2))
			{
				_doorWaitUntil = Time.time + 2.5f;
				_doorWaitFrom = val2.physGrabObject.centerPoint;
				_doorRest[val2] = Time.time + 2.5f;
				return false;
			}
			StartDoor(val2);
		}
		Box door = _door;
		if (BoxGone(door) || Time.time - _doorStarted > 6f)
		{
			EndDoor(opened: false);
			return false;
		}
		float num = OpenAngle(door);
		Vector3 eye = Eye();
		if (!_doorHolding)
		{
			if (num > 60f)
			{
				EndDoor(opened: true);
				return false;
			}
			Vector3 centerPoint = door.Door.centerPoint;
			lookAt = centerPoint;
			if (!PickDoorPoint(door, eye, out var point, out var which) || !BotGrab.GrabAt(_bot, door.Door, point, which))
			{
				return false;
			}
			_doorHolding = true;
			_doorLocal = ((Component)door.Door).transform.InverseTransformPoint(point);
			_doorSign = AwayFromMe(door, point, walk);
			_doorBest = num;
			_doorProgressAt = Time.time;
			Plugin.Log.LogInfo((object)(_bot.Name + " is opening a door"));
		}
		int num2 = (((Object)(object)door.Door.photonView != (Object)null) ? door.Door.photonView.ViewID : 0);
		if (_bot.HeldView != num2 || num2 == 0)
		{
			EndDoor(opened: false);
			return false;
		}
		lookAt = ((Component)door.Door).transform.TransformPoint(_doorLocal);
		if (num >= 75f)
		{
			EndDoor(opened: true);
			return false;
		}
		if (num > _doorBest + 3f)
		{
			_doorBest = num;
			_doorProgressAt = Time.time;
		}
		else if (Time.time - _doorProgressAt > 0.7f)
		{
			if (num >= 50f)
			{
				EndDoor(opened: true);
				return false;
			}
			_doorSign = 0f - _doorSign;
			_doorFlips++;
			_doorProgressAt = Time.time;
			if (_doorFlips > 3)
			{
				EndDoor(opened: false);
				return false;
			}
		}
		Vector3 anchor = HingeAnchor(door, out var axis);
		Vector3 centerPoint2 = door.Door.centerPoint;
		float num3 = FromAxis(centerPoint2, anchor, axis) * 2f + 0.15f;
		if (FromAxis(new Vector3(_bot.Position.x, centerPoint2.y, _bot.Position.z), anchor, axis) < num3)
		{
			Vector3 val3 = Flat(_bot.Position - centerPoint2);
			Vector3 val5;
			if (!(val3.sqrMagnitude > 0.01f))
			{
				Vector3 val4 = Flat(walk);
				val5 = -val4.normalized;
			}
			else
			{
				val5 = val3.normalized;
			}
			desired = val5 * 1.4f;
		}
		else
		{
			desired = Vector3.zero;
		}
		return true;
	}

	private float AwayFromMe(Box d, Vector3 grab, Vector3 walk)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = HingeAnchor(d, out var axis);
		Vector3 val2 = grab - val;
		Vector3 val3 = val + Quaternion.AngleAxis(30f, axis) * val2 - grab;
		Vector3 val4 = val + Quaternion.AngleAxis(-30f, axis) * val2 - grab;
		Vector3 val5 = Flat(walk);
		float result = ((Vector3.Dot(val3, val5) >= Vector3.Dot(val4, val5)) ? 1f : (-1f));
		JointLimits limits = d.Hinge.joint.limits;
		if (d.Hinge.joint.useLimits && limits.max <= 1f && limits.min < -1f)
		{
			result = -1f;
		}
		else if (d.Hinge.joint.useLimits && limits.min >= -1f && limits.max > 1f)
		{
			result = 1f;
		}
		return result;
	}

	private void StartDoor(PhysGrabHinge h)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		_door = new Box
		{
			Hinge = h,
			Door = h.physGrabObject,
			Kind = "door",
			Furniture = new Bounds(h.physGrabObject.centerPoint, Vector3.one)
		};
		_doorHolding = false;
		_doorStarted = Time.time;
		_doorFlips = 0;
		DoorClaims[h] = this;
	}

	private void EndDoor(bool opened)
	{
		Box door = _door;
		_door = null;
		if (door != null && (Object)(object)door.Hinge != (Object)null && DoorClaims.TryGetValue(door.Hinge, out var value) && value == this)
		{
			DoorClaims.Remove(door.Hinge);
		}
		if (_doorHolding && door != null && (Object)(object)door.Door != (Object)null && (Object)(object)door.Door.photonView != (Object)null && _bot.HeldView == door.Door.photonView.ViewID)
		{
			BotGrab.Release(_bot);
		}
		_doorHolding = false;
		if (door != null && (Object)(object)door.Hinge != (Object)null)
		{
			_doorRest[door.Hinge] = Time.time + (opened ? 3f : 10f);
			if (!opened)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " couldn't open a door"));
			}
		}
		_corners.Clear();
		ResetProgress();
	}

	private void DoorPuller()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		Box door = _door;
		if (!BoxGone(door))
		{
			Vector3 val = HingeAnchor(door, out var axis);
			Vector3 val2 = ((Component)door.Door).transform.TransformPoint(_doorLocal);
			Vector3 val3 = val + Quaternion.AngleAxis(_doorSign * 35f, axis) * (val2 - val);
			Vector3 val4 = Eye();
			_bot.Puller = val4 + Vector3.ClampMagnitude(val3 - val4, 4f);
		}
	}

	private void Shoulder(PhysGrabHinge h, Vector3 walk, float strength = 1.2f)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		Rigidbody rb = h.physGrabObject.rb;
		if (!((Object)(object)rb == (Object)null) && !rb.isKinematic)
		{
			Vector3 val = Flat(walk);
			Vector3 normalized = val.normalized;
			Vector3 val2 = h.physGrabObject.centerPoint;
			Collider componentInChildren = ((Component)h).GetComponentInChildren<Collider>();
			if ((Object)(object)componentInChildren != (Object)null)
			{
				val2 = componentInChildren.ClosestPoint(_bot.Position + Vector3.up * 0.9f);
			}
			rb.AddForceAtPosition(normalized * strength, val2, (ForceMode)2);
		}
	}

	private static bool IsGnome(Threat t)
	{
		if (t != null)
		{
			return t.Kind == "EnemyGnome";
		}
		return false;
	}

	private static bool GnomeDead(EnemyParent ep)
	{
		Enemy val = (((Object)(object)ep != (Object)null) ? ep.Enemy : null);
		if (!((Object)(object)val == (Object)null) && ((Component)ep).gameObject.activeInHierarchy && (!val.HasHealth || !((Object)(object)val.Health != (Object)null) || (!val.Health.dead && val.Health.healthCurrent > 0)) && !((Object)(object)val.Rigidbody == (Object)null))
		{
			return (Object)(object)val.Rigidbody.physGrabObject == (Object)null;
		}
		return true;
	}

	private bool TryStartGnome()
	{
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _gnomeCheckAt || !SemiFunc.RunIsLevel() || _bot.HeldView != 0 || _bot.HoldingStatic)
		{
			return false;
		}
		_gnomeCheckAt = Time.time + 1f;
		if (Nerve < 0.2f || InDanger())
		{
			return false;
		}
		float num = Mathf.Lerp(6f, 15f, P.Brave);
		Threat threat = null;
		float num2 = float.MaxValue;
		foreach (Threat item in KnownThreats())
		{
			if (!IsGnome(item) || item.State.Contains("Despawn") || item.State.Contains("Spawn") || GnomeDead(item.Parent))
			{
				continue;
			}
			bool flag = false;
			foreach (BotBrain brain in BotManager.Brains)
			{
				if (brain != null && brain != this && (Object)(object)brain._gnome == (Object)(object)item.Parent)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				float num3 = Vector3.Distance(item.Position, _bot.Position);
				if (num3 < num && num3 < num2 && OnNavmesh(item.Position))
				{
					num2 = num3;
					threat = item;
				}
			}
		}
		if (threat == null)
		{
			return false;
		}
		DropJob("TryStartGnome", 54);
		_job = Job.Gnome;
		_gnome = threat.Parent;
		_jobStarted = Time.time;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " is going after a gnome"));
		Say(Line("gnomego"), 1, "gnome:" + _bot.Name);
		return true;
	}

	private bool GnomeStillGoing()
	{
		if ((Object)(object)_gnome != (Object)null && !GnomeDead(_gnome))
		{
			return Time.time - _jobStarted < 30f;
		}
		return false;
	}

	private Vector3 DoGnome(out Vector3? lookAt)
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		EnemyParent gnome = _gnome;
		if (GnomeDead(gnome))
		{
			if ((Object)(object)gnome != (Object)null && _bot.HeldView != 0)
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " killed a gnome"));
				Say(Line("gnomedead"), 1, "gnomedead:" + _bot.Name, 10f);
				BotSave.Add(_bot.SteamId, "gnomes");
			}
			ReleaseHeld("DoGnome", 79);
			_gnome = null;
			DropJob("DoGnome", 81);
			return Vector3.zero;
		}
		PhysGrabObject physGrabObject = gnome.Enemy.Rigidbody.physGrabObject;
		Vector3 centerPoint = physGrabObject.centerPoint;
		lookAt = centerPoint;
		Vector3 val = Eye();
		int num = (((Object)(object)physGrabObject.photonView != (Object)null) ? physGrabObject.photonView.ViewID : 0);
		if (_bot.HeldView != num || num == 0)
		{
			if (_bot.HeldView != 0)
			{
				ReleaseHeld("DoGnome", 93);
			}
			Vector3 val2 = centerPoint - val;
			if (val2.magnitude <= 3.5f && BotGrab.CanGrab(_bot, physGrabObject, val, null, 3.5f) && BotGrab.Grab(_bot, physGrabObject, val, null))
			{
				_grabbedAt = Time.time;
				_gnomeSlamAt = Time.time + 0.5f;
				_gnomeUp = true;
				return Vector3.zero;
			}
			val2 = Flat(centerPoint - _bot.Position);
			if (!(val2.magnitude < 1.2f))
			{
				return Steer(_bot.Position, centerPoint) * 5.6f * 0.8f;
			}
			return Vector3.zero;
		}
		if (Time.time >= _gnomeSlamAt)
		{
			_gnomeUp = !_gnomeUp;
			_gnomeSlamAt = Time.time + (_gnomeUp ? 0.55f : 0.35f);
		}
		return Vector3.zero;
	}

	private void GnomePuller()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward);
		Vector3 normalized = val.normalized;
		Vector3 val2 = _bot.Position + normalized * 1.3f;
		_bot.Puller = (_gnomeUp ? (val2 + Vector3.up * 2.3f) : (val2 - Vector3.up * 0.6f));
	}

	private Vector3 DoHangOut(out Vector3? lookAt)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		float time = Time.time;
		bool flag = SemiFunc.RunIsShop();
		if (!_hangAnchored || _hangInShop != flag)
		{
			_hangAnchored = true;
			_hangInShop = flag;
			_hangSpot = null;
			_hangAnchor = (((Object)(object)TruckSafetySpawnPoint.instance != (Object)null) ? ((Component)TruckSafetySpawnPoint.instance).transform.position : _bot.Position);
			Transform val = ((flag && (Object)(object)ShopManager.instance != (Object)null) ? ShopManager.instance.extractionPoint : null);
			if ((Object)(object)val != (Object)null)
			{
				_hangAnchor = val.position;
			}
			_hangMoveAt = time + Random.Range(0.5f, 3f);
		}
		if (!_hangSpot.HasValue || time >= _hangMoveAt)
		{
			_hangSpot = PickHangSpot();
			_hangMoveAt = time + Random.Range(10f, 30f) * Mathf.Lerp(0.6f, 1.4f, P.Careful);
		}
		lookAt = HangLook();
		Vector3 val2 = Flat(_hangSpot.Value - _bot.Position);
		if (!_hangMoving && val2.magnitude > 0.8f)
		{
			_hangMoving = true;
		}
		else if (_hangMoving && val2.magnitude < 0.35f)
		{
			_hangMoving = false;
		}
		if (!_hangMoving)
		{
			return Vector3.zero;
		}
		lookAt = null;
		return Steer(_bot.Position, _hangSpot.Value) * 3.2f * 0.6f;
	}

	private Vector3? HangLook()
	{
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (time >= _hangLookAt)
		{
			_hangLookAt = time + Random.Range(2f, 6f);
			PlayerAvatar val = (_hangWho = ((Random.value < 0.35f + 0.5f * P.Social) ? SomeoneNearby(8f) : null));
			_hangLook = null;
			if ((Object)(object)val == (Object)null && (Object)(object)_browsing != (Object)null && Random.value < 0.6f && Vector3.Distance(((Component)_browsing).transform.position, _bot.Position) < 4f)
			{
				_hangLook = ((Component)_browsing).transform.position;
			}
			else if ((Object)(object)val == (Object)null)
			{
				_hangLook = Eye() + Quaternion.Euler(Random.Range(-10f, 15f), _bot.Yaw + Random.Range(-120f, 120f), 0f) * Vector3.forward * 4f;
			}
		}
		if ((Object)(object)_hangWho != (Object)null && !_hangWho.isDisabled)
		{
			return (BotRegistry.Get(_hangWho)?.Position ?? ((Component)_hangWho).transform.position) + Vector3.up * 1.4f;
		}
		return _hangLook;
	}

	private ItemAttributes BrowseItem()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		ShopTeam();
		ItemAttributes result = null;
		int num = 0;
		foreach (ItemAttributes item in ShopStock)
		{
			if (!((Object)(object)item == (Object)null) && !InZone(item) && !(Vector3.Distance(((Component)item).transform.position, _hangAnchor) > 25f))
			{
				PhysGrabObject component = ((Component)item).GetComponent<PhysGrabObject>();
				if (!((Object)(object)component == (Object)null) && component.playerGrabbing.Count <= 0 && Random.Range(0, ++num) == 0)
				{
					result = item;
				}
			}
		}
		return result;
	}

	private PlayerAvatar SomeoneNearby(float range)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar result = null;
		float num = float.MaxValue;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && !((Object)(object)player == (Object)(object)_bot.Avatar) && !player.isDisabled)
			{
				float num2 = Vector3.Distance(BotRegistry.Get(player)?.Position ?? ((Component)player).transform.position, _bot.Position);
				float num3 = num2 + (BotRegistry.IsBot(player) ? 3f : 0f) + Random.Range(0f, 3f);
				if (num2 < range && num3 < num)
				{
					num = num3;
					result = player;
				}
			}
		}
		return result;
	}

	private Vector3 PickHangSpot()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = _hangAnchor;
		float num = Mathf.Lerp(4.5f, 2f, P.Social);
		_browsing = null;
		if (_hangInShop)
		{
			num = Mathf.Lerp(8f, 4f, P.Social);
			if (Random.value < 0.45f + 0.35f * P.Greed)
			{
				_browsing = BrowseItem();
				if ((Object)(object)_browsing != (Object)null)
				{
					val = ((Component)_browsing).transform.position;
					num = 1.6f;
				}
			}
		}
		if ((Object)(object)_browsing == (Object)null && Random.value < P.Social)
		{
			PlayerAvatar val2 = SomeoneNearby(12f);
			if ((Object)(object)val2 != (Object)null && !BotRegistry.IsBot(val2))
			{
				val = Vector3.Lerp(val, ((Component)val2).transform.position, 0.7f);
			}
		}
		Vector3 result = _bot.Position;
		float num2 = float.MinValue;
		NavMeshHit val4 = default;
		NavMeshHit val7 = default;
		for (int i = 0; i < 14; i++)
		{
			Vector2 val3 = Random.insideUnitCircle * num;
			if (!NavMesh.SamplePosition(val + new Vector3(val3.x, 0f, val3.y), out val4, 1f, -1))
			{
				continue;
			}
			Vector3 position = val4.position;
			if (Mathf.Abs(position.y - (((Object)(object)_browsing != (Object)null) ? val.y : _anchorY())) > (((Object)(object)_browsing != (Object)null) ? 2f : 1.5f))
			{
				continue;
			}
			float num3 = 99f;
			foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
			{
				if (!((Object)(object)player == (Object)null) && !((Object)(object)player == (Object)(object)_bot.Avatar) && !player.isDisabled)
				{
					BotBrain botBrain = (BotRegistry.IsBot(player) ? BotManager.BrainOf(player) : null);
					Vector3 val5 = ((botBrain != null && botBrain._hangSpot.HasValue) ? botBrain._hangSpot.Value : (BotRegistry.Get(player)?.Position ?? ((Component)player).transform.position));
					float num4 = num3;
					Vector3 val6 = Flat(val5 - position);
					num3 = Mathf.Min(num4, val6.magnitude);
				}
			}
			if (!(num3 < 1.1f))
			{
				float num5 = Mathf.Min(num3, 3f) + Random.Range(0f, 1f);
				if (P.Social < 0.4f && NavMesh.FindClosestEdge(position, out val7, -1))
				{
					num5 += Mathf.Clamp01(1.2f - val7.distance) * 2f;
				}
				if (num5 > num2)
				{
					num2 = num5;
					result = position;
				}
			}
		}
		return result;
	}

	private float _anchorY()
	{
		return _hangAnchor.y;
	}

	private static PlayerHealthGrab Neck(PlayerAvatar p)
	{
		if ((Object)(object)p == (Object)null)
		{
			return null;
		}
		if (NeckOf.TryGetValue(p, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (Time.time >= _necksAt)
		{
			_necksAt = Time.time + 2f;
			PlayerHealthGrab[] array = Object.FindObjectsOfType<PlayerHealthGrab>();
			foreach (PlayerHealthGrab val in array)
			{
				if ((Object)(object)val != (Object)null && (Object)(object)val.playerAvatar != (Object)null)
				{
					NeckOf[val.playerAvatar] = val;
				}
			}
		}
		if (!NeckOf.TryGetValue(p, out value))
		{
			return null;
		}
		return value;
	}

	public void LearnHurt(PlayerAvatar p)
	{
		if ((Object)(object)p != (Object)null && (Object)(object)p != (Object)(object)_bot.Avatar)
		{
			_knowHurt[p] = Time.time;
		}
	}

	internal static void TeamLearnsHurt(PlayerAvatar p, Vector3 heardAt, float range)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain.Alive && Vector3.Distance(brain.Position, heardAt) < range)
			{
				brain.LearnHurt(p);
			}
		}
	}

	public void OrderHeal(PlayerAvatar who)
	{
		if (Alive && !((Object)(object)who == (Object)null) && !who.isDisabled && !((Object)(object)who == (Object)(object)_bot.Avatar))
		{
			HeardFrom(who);
			if (!CanSpare)
			{
				Say(Line("healrefuse", null, null, null, null, who.playerName), 3);
				return;
			}
			ClearOrder(say: false);
			StartHeal(who, asked: true);
			Say(Line("healyes", null, null, null, null, who.playerName), 3);
		}
	}

	private bool TryStartHeal()
	{
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _healCheckAt || (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsLobby()) || _bot.HeldView != 0 || _bot.HoldingStatic)
		{
			return false;
		}
		_healCheckAt = Time.time + 1.5f;
		if (!CanSpare || InDanger())
		{
			return false;
		}
		PlayerAvatar val = null;
		float num = float.MaxValue;
		foreach (KeyValuePair<PlayerAvatar, float> item in _knowHurt)
		{
			PlayerAvatar key = item.Key;
			if ((Object)(object)key == (Object)null || key.isDisabled || Time.time - item.Value > 45f || (Object)(object)Neck(key) == (Object)null || (BotRegistry.IsBot(key) && (Object)(object)key.playerHealth != (Object)null && ((float)key.playerHealth.health >= (float)key.playerHealth.maxHealth * 0.85f || MyHealth - 10 < key.playerHealth.health + 20)))
			{
				continue;
			}
			bool flag = false;
			foreach (BotBrain brain in BotManager.Brains)
			{
				if (brain != null && brain != this && (Object)(object)brain._healing == (Object)(object)key)
				{
					flag = true;
				}
			}
			if (flag)
			{
				continue;
			}
			Vector3? val2 = ((!BotRegistry.IsBot(key)) ? PersonPos(key) : BotRegistry.Get(key)?.Position);
			if (val2.HasValue)
			{
				float num2 = Vector3.Distance(val2.Value, _bot.Position);
				float num3 = Mathf.Lerp(8f, 30f, P.Helpful);
				float num4 = num2 + (BotRegistry.IsBot(key) ? 10f : 0f);
				if (num2 < num3 && num4 < num && SafeSpot(val2.Value))
				{
					num = num4;
					val = key;
				}
			}
		}
		if ((Object)(object)val == (Object)null || Random.value > 0.35f + 0.65f * P.Helpful)
		{
			return false;
		}
		_knowHurt.Remove(val);
		StartHeal(val, asked: false);
		Say(Line("healoffer", null, null, null, null, val.playerName), 1, "heal:" + val.playerName);
		return true;
	}

	private void StartHeal(PlayerAvatar who, bool asked)
	{
		DropJob("StartHeal", 136);
		_job = Job.Heal;
		_healing = who;
		_healAsked = asked;
		_healStartHp = MyHealth;
		_healTargetHp = (((Object)(object)who.playerHealth != (Object)null) ? who.playerHealth.health : 0);
		_healProgressAt = Time.time;
		_jobStarted = Time.time;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)$"{_bot.Name} is going to heal {who.playerName} (gives up to {HealBudget}, keeps at least {HealFloor})");
	}

	internal static bool IsHealingSomeone(PlayerAvatar p)
	{
		if ((Object)(object)p == (Object)null || (Object)(object)p.physGrabber == (Object)null)
		{
			return false;
		}
		Neck(p);
		foreach (KeyValuePair<PlayerAvatar, PlayerHealthGrab> item in NeckOf)
		{
			if ((Object)(object)item.Value != (Object)null && (Object)(object)item.Value.staticGrabObject != (Object)null && item.Value.staticGrabObject.playerGrabbing.Contains(p.physGrabber))
			{
				return true;
			}
		}
		return false;
	}

	private void StopHeal(bool finished)
	{
		PlayerHealthGrab val = Neck(_healing);
		if (!finished && (Object)(object)_healing != (Object)null && !_healing.isDisabled)
		{
			_knowHurt[_healing] = Time.time;
		}
		if (_bot.HoldingStatic && (Object)(object)val != (Object)null)
		{
			BotGrab.ReleaseStatic(_bot, val.staticGrabObject);
		}
		if (finished && (Object)(object)_healing != (Object)null)
		{
			int num = Mathf.Max(0, _healStartHp - MyHealth);
			Plugin.Log.LogInfo((object)$"{_bot.Name} healed {_healing.playerName} for {num}");
			BotSave.Add(_bot.SteamId, "healed", num);
			if (num > 0)
			{
				Say(Line((num >= HealBudget || !CanSpare) ? "healdone" : "healdonefull", null, null, null, null, _healing.playerName, Words(num)), 1);
			}
		}
		_healing = null;
	}

	private bool HealStillGoing()
	{
		if ((Object)(object)_healing != (Object)null && !_healing.isDisabled)
		{
			return Time.time - _jobStarted < 40f;
		}
		return false;
	}

	private Vector3 DoHeal(out Vector3? lookAt)
	{
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0282: Unknown result type (might be due to invalid IL or missing references)
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		PlayerAvatar healing = _healing;
		PlayerHealthGrab val = Neck(healing);
		if ((Object)(object)healing == (Object)null || healing.isDisabled || (Object)(object)val == (Object)null || (Object)(object)val.staticGrabObject == (Object)null)
		{
			if ((Object)(object)healing != (Object)null && healing.isDisabled)
			{
				CancelTyping("heal:" + healing.playerName);
			}
			StopHeal(finished: false);
			DropJob("DoHeal", 193);
			return Vector3.zero;
		}
		Collider component = ((Component)val).GetComponent<Collider>();
		Vector3 val2;
		if (!((Object)(object)component != (Object)null) || !component.enabled)
		{
			val2 = ((Component)val).transform.position;
		}
		else
		{
			Bounds bounds = component.bounds;
			val2 = bounds.center;
		}
		Vector3 val3 = val2;
		lookAt = val3;
		Vector3 val4 = Eye();
		Vector3 val5 = val3 - val4;
		float magnitude = val5.magnitude;
		int num = _healStartHp - MyHealth;
		if (_bot.HoldingStatic)
		{
			_bot.Puller = val3;
			int num2 = (((Object)(object)healing.playerHealth != (Object)null) ? healing.playerHealth.health : 0);
			if (num2 != _healTargetHp)
			{
				_healTargetHp = num2;
				_healProgressAt = Time.time;
			}
			if (((Object)(object)healing.playerHealth != (Object)null && num2 >= healing.playerHealth.maxHealth) || num >= HealBudget || !CanSpare || Time.time - _healProgressAt > 3.5f)
			{
				StopHeal(finished: true);
				DropJob("DoHeal", 217);
				return Vector3.zero;
			}
			if (magnitude > 4.1f)
			{
				BotGrab.ReleaseStatic(_bot, val.staticGrabObject);
			}
			return Vector3.zero;
		}
		if (magnitude < 2.9f && NeckLine(val4, val3, healing, component))
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " grabbed " + healing.playerName + "'s neck"));
			BotGrab.GrabStatic(_bot, val.staticGrabObject, val3);
			_healProgressAt = Time.time;
			return Vector3.zero;
		}
		Vector3 position = ((Component)healing).transform.position;
		Vector3 val6 = Flat(_bot.Position - position);
		position += ((val6.sqrMagnitude > 0.01f) ? val6.normalized : Vector3.forward) * 1.4f;
		NavMeshHit val7 = default;
		if (NavMesh.SamplePosition(position, out val7, 1.5f, -1))
		{
			position = val7.position;
		}
		val5 = Flat(position - _bot.Position);
		if (val5.magnitude < 0.5f)
		{
			return Vector3.zero;
		}
		return Steer(_bot.Position, position) * 3.2f;
	}

	private bool NeckLine(Vector3 eye, Vector3 point, PlayerAvatar who, Collider neck)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = point - eye;
		float magnitude = val.magnitude;
		Transform root = ((Component)_bot.Avatar).transform.root;
		Transform root2 = ((Component)who).transform.root;
		RaycastHit[] array = Physics.RaycastAll(eye, val / magnitude, magnitude, LayerMask.GetMask(new string[4] { "Default", "PhysGrabObject", "PhysGrabObjectCart", "StaticGrabObject" }), (QueryTriggerInteraction)1);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit val2 = array[i];
			Transform transform = ((Component)val2.collider).transform;
			if (!((Object)(object)transform.root == (Object)(object)root) && !((Object)(object)transform.root == (Object)(object)root2) && !((Object)(object)val2.collider == (Object)(object)neck) && (!((Object)(object)_bot.Body != (Object)null) || !transform.IsChildOf(_bot.Body.transform)))
			{
				return false;
			}
		}
		return true;
	}

	private static bool HunterComing(Threat t, Vector3 me)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Invalid comparison between Unknown and I4
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Invalid comparison between Unknown and I4
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Invalid comparison between Unknown and I4
		EnemyHunter val = (((Object)(object)t.Parent != (Object)null) ? ((Component)t.Parent).GetComponentInChildren<EnemyHunter>() : null);
		if (!((Object)(object)val == (Object)null))
		{
			Vector3 val2 = Flat(t.Position - me);
			if (!(val2.magnitude > 22f))
			{
				EnemyHunter.State currentState = val.currentState;
				if ((int)currentState == 3 || (int)currentState == 4 || (int)currentState == 5 || (int)currentState == 6)
				{
					val2 = Flat(val.investigatePoint - me);
					return val2.magnitude < 4f;
				}
				return false;
			}
		}
		return false;
	}

	private bool HunterComing(Threat t)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		return HunterComing(t, _bot.Position);
	}

	private void HuntsmanSneak(Threat t)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0250: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Unknown result type (might be due to invalid IL or missing references)
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0275: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0291: Unknown result type (might be due to invalid IL or missing references)
		//IL_0296: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (_hunterLastAt > 0f && time - _hunterLastAt < 1.5f && time > _hunterLastAt)
		{
			_hunterVel = Vector3.Lerp(_hunterVel, Flat(t.Position - _hunterLastPos) / (time - _hunterLastAt), 0.5f);
		}
		_hunterLastPos = t.Position;
		_hunterLastAt = time;
		Vector3 position = _bot.Position;
		Vector3 val = Flat(position - t.Position);
		float magnitude = val.magnitude;
		bool flag = magnitude < 8f && _hunterVel.magnitude > 0.3f && Vector3.Angle(_hunterVel, val) < 40f;
		if (magnitude < 12f && HunterComing(t, position))
		{
			flag = true;
		}
		Vector3 val2;
		if (_sneakTo.HasValue)
		{
			val2 = Flat(_sneakTo.Value - position);
			if (val2.magnitude > 0.4f)
			{
				return;
			}
		}
		if (!flag)
		{
			if (_sneakTo.HasValue)
			{
				val2 = Flat(_sneakTo.Value - position);
				if (val2.magnitude <= 0.4f)
				{
					return;
				}
			}
			_sneakTo = null;
			if (time - _frozeAt > 12f)
			{
				_frozeAt = time;
				Vector3 val3 = FleeSpot(t.Position);
				if (Vector3.Distance(val3, t.Position) > magnitude + 2f)
				{
					_sneakTo = val3;
					_sneakUnder = false;
					Plugin.Log.LogInfo((object)(_bot.Name + " is creeping away from the Huntsman"));
				}
			}
			return;
		}
		if ((FindHideSpot(t.Position, allowUnder: true, out var spot, out var under) & under) && Vector3.Distance(spot, position) < 5f)
		{
			_sneakTo = spot;
			_sneakUnder = true;
		}
		else
		{
			Vector3 val4;
			if (!(_hunterVel.sqrMagnitude > 0.01f))
			{
				val4 = -val.normalized;
			}
			else
			{
				val2 = Flat(_hunterVel);
				val4 = val2.normalized;
			}
			Vector3 val5 = val4;
			Vector3 val6 = Vector3.Cross(Vector3.up, val5);
			if (Vector3.Dot(val6, val) < 0f)
			{
				val6 = -val6;
			}
			NavMeshHit val7 = default;
			if (!NavMesh.SamplePosition(position + val6 * 2.5f, out val7, 1.5f, -1) && !NavMesh.SamplePosition(position - val6 * 2.5f, out val7, 1.5f, -1))
			{
				return;
			}
			_sneakTo = val7.position;
			_sneakUnder = false;
		}
		Plugin.Log.LogInfo((object)(_bot.Name + " is sneaking out of the Huntsman's way" + (_sneakUnder ? " (under something)" : "")));
	}

	private Vector3 DoFreeze(out bool crawl)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		crawl = false;
		if (!_sneakTo.HasValue)
		{
			return Vector3.zero;
		}
		Vector3 val = Flat(_sneakTo.Value - _bot.Position);
		crawl = _sneakUnder && val.magnitude < 1.6f;
		if (val.magnitude < 0.35f)
		{
			return Vector3.zero;
		}
		return Steer(_bot.Position, _sneakTo.Value) * 1.6f * 0.7f;
	}

	internal void HurtZone(HurtCollider hc)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)hc == (Object)null || !Alive)
		{
			return;
		}
		Collider component = ((Component)hc).GetComponent<Collider>();
		Vector3 val;
		Bounds bounds;
		if (!((Object)(object)component != (Object)null))
		{
			val = ((Component)hc).transform.position;
		}
		else
		{
			bounds = component.bounds;
			val = bounds.center;
		}
		Vector3 val2 = val;
		float num;
		if (!((Object)(object)component != (Object)null))
		{
			num = 1f;
		}
		else
		{
			bounds = component.bounds;
			float x = bounds.extents.x;
			bounds = component.bounds;
			num = Mathf.Max(x, bounds.extents.z);
		}
		float num2 = num;
		_zoneCenter = val2;
		_zoneRadius = Mathf.Clamp(num2, 0.5f, 6f);
		_zoneUntil = Time.time + 20f;
		if (Time.time < _escapeUntil)
		{
			return;
		}
		Vector3 val3 = Flat(_bot.Position - val2);
		if (val3.sqrMagnitude < 0.01f)
		{
			val3 = -Flat(Quaternion.Euler(0f, _bot.Yaw, 0f) * Vector3.forward);
		}
		val3.Normalize();
		Vector3 escapeTo = _bot.Position + val3 * (_zoneRadius + 2.5f);
		float[] array = new float[7] { 0f, 35f, -35f, 70f, -70f, 110f, -110f };
		NavMeshHit val4 = default;
		foreach (float num3 in array)
		{
			if (NavMesh.SamplePosition(_bot.Position + Quaternion.Euler(0f, num3, 0f) * val3 * (_zoneRadius + 2.5f), out val4, 1.5f, -1))
			{
				Vector3 val5 = Flat(val4.position - val2);
				if (val5.magnitude > _zoneRadius + 1f)
				{
					escapeTo = val4.position;
					break;
				}
			}
		}
		_escapeTo = escapeTo;
		_escapeUntil = Time.time + 2.5f;
		_corners.Clear();
		Plugin.Log.LogInfo((object)(_bot.Name + " is getting out of whatever is hurting it"));
	}

	private bool EscapingZone(ref Vector3 desired, ref bool sprint)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time >= _escapeUntil)
		{
			return false;
		}
		Vector3 val = Flat(_escapeTo - _bot.Position);
		if (val.magnitude < 0.5f)
		{
			_escapeUntil = 0f;
			return false;
		}
		desired = Steer(_bot.Position, _escapeTo) * 5.6f;
		sprint = true;
		return true;
	}

	private bool InHurtZone(Vector3 p)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _zoneUntil)
		{
			Vector3 val = Flat(p - _zoneCenter);
			return val.magnitude < _zoneRadius + 1f;
		}
		return false;
	}

	internal static RoomVolume RoomAt(Vector3 p)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		int num = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.5f, 0.25f, MapHits, RoomLayer, (QueryTriggerInteraction)2);
		for (int i = 0; i < num; i++)
		{
			RoomVolume val = (((Object)(object)MapHits[i] != (Object)null) ? ((Component)MapHits[i]).GetComponentInParent<RoomVolume>() : null);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		return null;
	}

	private bool Remembers(RoomVolume rv)
	{
		if (!((Object)(object)rv == (Object)null))
		{
			if (_rooms.TryGetValue(rv, out var value))
			{
				return Time.time - value < MemorySpan;
			}
			return false;
		}
		return true;
	}

	private static bool OnTeamMap(RoomVolume rv)
	{
		if ((Object)(object)rv != (Object)null)
		{
			return rv.Explored;
		}
		return false;
	}

	private void LearnMap()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (time < _mapLearnAt)
		{
			return;
		}
		_mapLearnAt = time + 0.5f;
		RoomVolume val = RoomAt(_bot.Position);
		if ((Object)(object)val != (Object)null)
		{
			_rooms[val] = time;
		}
		if ((Object)(object)LevelGenerator.Instance == (Object)null || LevelGenerator.Instance.LevelPathPoints == null)
		{
			return;
		}
		Vector3 val2 = Eye();
		foreach (LevelPoint levelPathPoint in LevelGenerator.Instance.LevelPathPoints)
		{
			if (!((Object)(object)levelPathPoint == (Object)null) && !((Object)(object)levelPathPoint.Room == (Object)null) && !((Object)(object)levelPathPoint.Room == (Object)(object)val))
			{
				Vector3 val3 = ((Component)levelPathPoint).transform.position + Vector3.up;
				Vector3 val4 = val3 - val2;
				if (!(val4.sqrMagnitude > 144f) && InView(val3, 60f, 0f) && !Physics.Linecast(val2, val3, GroundMask, (QueryTriggerInteraction)1))
				{
					_rooms[levelPathPoint.Room] = time;
				}
			}
		}
	}

	private void CheckRoute(Vector3 goal)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		if (_mapOut)
		{
			return;
		}
		Vector3 val = goal - _routeCheckedGoal;
		if (val.sqrMagnitude < 36f || _corners.Count == 0 || _job == Job.Follow || _job == Job.Flee || _job == Job.Hide || _job == Job.Freeze || _job == Job.Assist || _job == Job.Escort || _job == Job.Guard || _job == Job.Open || (_job == Job.Order && _order == OrderKind.Follow))
		{
			return;
		}
		_routeCheckedGoal = goal;
		int num = 0;
		bool flag = false;
		Vector3 val2 = _bot.Position;
		HashSet<RoomVolume> hashSet = new HashSet<RoomVolume>();
		for (int i = 0; i < _corners.Count; i++)
		{
			Vector3 val3 = _corners[i];
			float num2 = Vector3.Distance(val2, val3);
			int num3 = Mathf.Max(1, Mathf.CeilToInt(num2 / 4f));
			for (int j = 1; j <= num3; j++)
			{
				RoomVolume val4 = RoomAt(Vector3.Lerp(val2, val3, (float)j / (float)num3));
				if (!((Object)(object)val4 == (Object)null) && hashSet.Add(val4) && !Remembers(val4))
				{
					num++;
					if (OnTeamMap(val4))
					{
						flag = true;
					}
				}
			}
			val2 = val3;
		}
		if ((num > 0) & flag)
		{
			CheckMap("doesn't remember the way");
		}
	}

	public bool CheckMap(string why, Action after = null)
	{
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null || avatar.isDisabled || _mapOut || !SemiFunc.RunIsLevel())
		{
			return false;
		}
		if (Time.time < _mapNextAllowed || InDanger() || _job == Job.Hide)
		{
			return false;
		}
		if (_bot.HeldView != 0 || _bot.HoldingStatic)
		{
			return false;
		}
		_mapOut = true;
		_mapUntil = Time.time + Mathf.Lerp(1.6f, 4f, P.Careful) * Random.Range(0.85f, 1.2f);
		_mapAfter = after;
		SetMapTool(on: true);
		Plugin.Log.LogInfo((object)(_bot.Name + " is checking the map (" + why + ")"));
		if (after == null && Random.value < 0.35f)
		{
			Say(Line("mapcheck"), 0, "mapcheck:" + _bot.Name, 60f);
		}
		return true;
	}

	private void PutMapAway(bool finished)
	{
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		if (!_mapOut)
		{
			return;
		}
		_mapOut = false;
		_mapNextAllowed = Time.time + 20f;
		SetMapTool(on: false);
		if (!finished)
		{
			_mapAfter = null;
			return;
		}
		int num = 0;
		RoomVolume[] array = Object.FindObjectsOfType<RoomVolume>();
		foreach (RoomVolume val in array)
		{
			if (OnTeamMap(val))
			{
				if (!Remembers(val))
				{
					num++;
				}
				_rooms[val] = Time.time;
			}
		}
		Plugin.Log.LogInfo((object)$"{_bot.Name} looked at the map ({num} rooms refreshed, {_rooms.Count} known)");
		_routeCheckedGoal = new Vector3(9999f, 9999f, 9999f);
		Action mapAfter = _mapAfter;
		_mapAfter = null;
		mapAfter?.Invoke();
	}

	private void SetMapTool(bool on)
	{
		MapToolController val = (((Object)(object)_bot.Avatar != (Object)null) ? _bot.Avatar.mapToolController : null);
		if (!((Object)(object)val == (Object)null) && !((Object)(object)val.photonView == (Object)null))
		{
			val.photonView.RPC("SetActiveRPC", (RpcTarget)0, new object[1] { on });
		}
	}

	private float MapTick()
	{
		float time = Time.time;
		LearnMap();
		if (_mapOut)
		{
			if (InDanger() || _job == Job.Hide)
			{
				PutMapAway(finished: false);
				return 1f;
			}
			if (time >= _mapUntil)
			{
				PutMapAway(finished: true);
				return 1f;
			}
			return 0.45f;
		}
		if (!SemiFunc.RunIsLevel())
		{
			return 1f;
		}
		if (_mapIdleAt < 0f)
		{
			_mapIdleAt = time + Random.Range(60f, 120f);
		}
		if (time >= _mapIdleAt)
		{
			_mapIdleAt = time + Mathf.Lerp(200f, 70f, P.Careful) * Random.Range(0.8f, 1.3f);
			if ((_job == Job.Follow || _job == Job.Explore) && Random.value < 0.3f + 0.6f * P.Careful)
			{
				CheckMap("habit");
			}
		}
		return 1f;
	}

	private void AfterChase()
	{
		if (Random.value < Mathf.Lerp(0.25f, 0.7f, P.Careful))
		{
			CheckMap("got turned around running");
		}
	}

	private static void ResetMemoryStatics()
	{
		AllCarts.Clear();
		_allCartsAt = 0f;
	}

	private bool CanSee(Vector3 p, float range, Transform ignore)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Eye();
		Vector3 val2 = p - val;
		if (val2.sqrMagnitude > range * range)
		{
			return false;
		}
		if (!InView(p, 60f, 3f))
		{
			return false;
		}
		return ClearLine(val, p, null, ignore);
	}

	private bool Recall(PhysGrabObject o, out Vector3 pos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		pos = Vector3.zero;
		if ((Object)(object)o == (Object)null)
		{
			return false;
		}
		if (CanSee(o.centerPoint, 22f, ((Component)o).transform))
		{
			_thingPos[o] = o.centerPoint;
			pos = o.centerPoint;
			return true;
		}
		return _thingPos.TryGetValue(o, out pos);
	}

	private void Forget(PhysGrabObject o)
	{
		if (!((Object)(object)o == (Object)null))
		{
			_thingPos.Remove(o);
			ValuableObject component = ((Component)o).GetComponent<ValuableObject>();
			if ((Object)(object)component != (Object)null)
			{
				Seen.Remove(component);
			}
		}
	}

	private void UpdateMemory()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_033b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0397: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_042c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0431: Unknown result type (might be due to invalid IL or missing references)
		//IL_0436: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Unknown result type (might be due to invalid IL or missing references)
		//IL_044b: Unknown result type (might be due to invalid IL or missing references)
		//IL_044d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d4: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		if (time < _memoryAt)
		{
			return;
		}
		_memoryAt = time + 0.3f;
		Vector3 val = Eye();
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player == (Object)null || (Object)(object)player == (Object)(object)_bot.Avatar || BotRegistry.IsBot(player))
			{
				continue;
			}
			if (!_people.TryGetValue(player, out var value))
			{
				value = new PersonMemory();
				_people[player] = value;
			}
			value.SeenNow = false;
			if (!player.isDisabled)
			{
				Vector3 val2 = (((Object)(object)player.PlayerVisionTarget != (Object)null) ? player.PlayerVisionTarget.VisionTransform.position : (((Component)player).transform.position + Vector3.up * 1.4f));
				bool flag = (value.SeenNow = CanSee(val2, 30f, ((Component)player).transform.root));
				if (!flag && player.voiceChatFetched && (Object)(object)player.voiceChat != (Object)null && player.voiceChat.isTalking)
				{
					flag = Vector3.Distance(val2, val) < (ClearLine(val, val2, null, ((Component)player).transform.root) ? 20f : 10f);
				}
				if (flag)
				{
					value.Pos = ((Component)player).transform.position;
					value.Time = time;
				}
			}
		}
		if (time >= _allCartsAt)
		{
			_allCartsAt = time + 5f;
			AllCarts.Clear();
			AllCarts.AddRange(Object.FindObjectsOfType<PhysGrabCart>());
		}
		foreach (PhysGrabCart allCart in AllCarts)
		{
			if (!((Object)(object)allCart == (Object)null) && !((Object)(object)allCart.physGrabObject == (Object)null) && CanSee(((Component)allCart).transform.position + Vector3.up * 0.5f, 22f, ((Component)allCart).transform))
			{
				_thingPos[allCart.physGrabObject] = ((Component)allCart).transform.position;
				if (!Carts.Contains(allCart))
				{
					Carts.Add(allCart);
				}
			}
		}
		Carts.RemoveAll((PhysGrabCart c) => (Object)(object)c == (Object)null);
		foreach (PlayerAvatar player2 in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player2 == (Object)null || (Object)(object)player2 == (Object)(object)_bot.Avatar || !player2.isDisabled || (Object)(object)player2.playerDeathHead == (Object)null || !player2.playerDeathHead.triggered)
			{
				continue;
			}
			PhysGrabObject physGrabObject = player2.playerDeathHead.physGrabObject;
			if ((Object)(object)physGrabObject != (Object)null && CanSee(physGrabObject.centerPoint, 22f, ((Component)physGrabObject).transform))
			{
				if (!_thingPos.ContainsKey(physGrabObject))
				{
					Plugin.Log.LogInfo((object)(_bot.Name + " found " + player2.playerName + "'s head"));
				}
				_thingPos[physGrabObject] = physGrabObject.centerPoint;
			}
		}
		if ((Object)(object)LevelGenerator.Instance != (Object)null && LevelGenerator.Instance.LevelPathPoints != null)
		{
			foreach (LevelPoint levelPathPoint in LevelGenerator.Instance.LevelPathPoints)
			{
				if ((Object)(object)levelPathPoint == (Object)null || Visited.Contains(levelPathPoint))
				{
					continue;
				}
				Vector3 val3 = ((Component)levelPathPoint).transform.position - _bot.Position;
				Vector3 val4;
				if (Mathf.Abs(val3.y) < 2.5f)
				{
					val4 = Flat(val3);
					if (val4.sqrMagnitude < 36f)
					{
						Visited.Add(levelPathPoint);
						continue;
					}
				}
				foreach (KeyValuePair<PlayerAvatar, PersonMemory> person in _people)
				{
					if (!person.Value.SeenNow)
					{
						continue;
					}
					Vector3 val5 = ((Component)levelPathPoint).transform.position - person.Value.Pos;
					if (Mathf.Abs(val5.y) < 2.5f)
					{
						val4 = Flat(val5);
						if (val4.sqrMagnitude < 25f)
						{
							Visited.Add(levelPathPoint);
							break;
						}
					}
				}
			}
		}
		if (time >= _shareAt)
		{
			_shareAt = time + 1f;
			ShareWithNearbyBots();
		}
	}

	private void ShareWithNearbyBots()
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		foreach (BotInfo value2 in BotRegistry.Bots.Values)
		{
			if (value2 == _bot || (Object)(object)value2.Avatar == (Object)null || value2.Avatar.isDisabled || Vector3.Distance(value2.Position, _bot.Position) > 15f)
			{
				continue;
			}
			BotBrain botBrain = BotManager.BrainOf(value2.Avatar);
			if (botBrain == null)
			{
				continue;
			}
			foreach (ValuableObject item in Seen)
			{
				if ((Object)(object)item != (Object)null)
				{
					botBrain.Seen.Add(item);
				}
			}
			foreach (KeyValuePair<PhysGrabObject, Vector3> thingPo in _thingPos)
			{
				if ((Object)(object)thingPo.Key != (Object)null && !botBrain._thingPos.ContainsKey(thingPo.Key))
				{
					botBrain._thingPos[thingPo.Key] = thingPo.Value;
				}
			}
			foreach (PhysGrabCart cart in Carts)
			{
				if ((Object)(object)cart != (Object)null && !botBrain.Carts.Contains(cart))
				{
					botBrain.Carts.Add(cart);
				}
			}
			botBrain.Visited.UnionWith(Visited);
			foreach (KeyValuePair<PlayerAvatar, PersonMemory> person in _people)
			{
				if (!((Object)(object)person.Key == (Object)null))
				{
					if (!botBrain._people.TryGetValue(person.Key, out var value))
					{
						value = new PersonMemory();
						botBrain._people[person.Key] = value;
					}
					if (person.Value.Time > value.Time)
					{
						value.Pos = person.Value.Pos;
						value.Time = person.Value.Time;
					}
				}
			}
		}
	}

	private Vector3? PersonPos(PlayerAvatar p)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)p == (Object)null || p.isDisabled || !_people.TryGetValue(p, out var value))
		{
			return null;
		}
		if (value.SeenNow)
		{
			return ((Component)p).transform.position;
		}
		if (Time.time - value.Time > 90f)
		{
			return null;
		}
		return value.Pos;
	}

	private bool SeesPerson(PlayerAvatar p)
	{
		if ((Object)(object)p != (Object)null && _people.TryGetValue(p, out var value))
		{
			return value.SeenNow;
		}
		return false;
	}

	private void LostPerson(PlayerAvatar p)
	{
		if ((Object)(object)p != (Object)null && _people.TryGetValue(p, out var value) && !value.SeenNow)
		{
			value.Time = -100f;
		}
	}

	private string Ack(string specific)
	{
		if (P.Helpful < 0.3f && Random.value < 0.4f)
		{
			return Line("ackgrumpy") + ", " + specific;
		}
		if (!(Random.value < 0.6f))
		{
			return Pick(Okays) + " " + specific;
		}
		return specific;
	}

	private void HeardFrom(PlayerAvatar p)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)p == (Object)null) && !p.isDisabled && !BotRegistry.IsBot(p))
		{
			if (!_people.TryGetValue(p, out var value))
			{
				value = new PersonMemory();
				_people[p] = value;
			}
			value.Pos = ((Component)p).transform.position;
			value.Time = Time.time;
		}
	}

	private void StartOrder(OrderKind kind, PlayerAvatar by, Vector3 pos, float seconds)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		DropJob("StartOrder", 58);
		_order = kind;
		_orderBy = by;
		_orderPos = pos;
		_orderUntil = Time.time + seconds;
		_orderArrived = false;
		_orderStuck = 0;
		_orderLostSince = -1f;
		_job = Job.Order;
		_jobStarted = Time.time;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)($"{_bot.Name} was told to {kind}" + (((Object)(object)by != (Object)null) ? (" by " + by.playerName) : "")));
	}

	public void ClearOrder(bool say)
	{
		if (_order != OrderKind.None)
		{
			Plugin.Log.LogInfo((object)$"{_bot.Name} is done with {_order}");
			_order = OrderKind.None;
			_orderBy = null;
			if (_job == Job.Order)
			{
				_job = Job.Follow;
			}
			if (say)
			{
				Say(Line("ackrelease"), 3);
			}
		}
	}

	public void OrderFollow(PlayerAvatar by)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		if (Alive && !((Object)(object)by == (Object)null))
		{
			if (_order == OrderKind.Follow && (Object)(object)_orderBy == (Object)(object)by)
			{
				ClearOrder(say: true);
				return;
			}
			HeardFrom(by);
			StartOrder(OrderKind.Follow, by, ((Component)by).transform.position, 600f);
			Say(Ack(Line("ackfollow")), 3);
		}
	}

	public void OrderCome(PlayerAvatar by)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (Alive && !((Object)(object)by == (Object)null))
		{
			HeardFrom(by);
			StartOrder(OrderKind.Come, by, ((Component)by).transform.position, 120f);
			float num = Vector3.Distance(((Component)by).transform.position, _bot.Position);
			Say(Ack((num > 30f) ? Line("ackcomefar") : Line("ackcome")), 3);
		}
	}

	public void OrderHold(PlayerAvatar by)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		if (Alive)
		{
			StartOrder(OrderKind.Hold, by, _bot.Position, 150f);
			Say(Ack(Line("ackhold")), 3);
		}
	}

	public void OrderGoTo(PlayerAvatar by, Vector3 point)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if (Alive)
		{
			NavMeshHit val = default;
			if (NavMesh.SamplePosition(point, out val, 2.5f, -1))
			{
				point = val.position;
				StartOrder(OrderKind.GoTo, by, point, 150f);
				Say(Ack(Line("ackgo")), 3);
			}
			else
			{
				Say(Line("cantreach"), 3);
			}
		}
	}

	public void OrderTruck(PlayerAvatar by)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (Alive && !((Object)(object)TruckSafetySpawnPoint.instance == (Object)null))
		{
			StartOrder(OrderKind.Truck, by, ((Component)TruckSafetySpawnPoint.instance).transform.position, 300f);
			Say(Ack(Pick("Heading to the truck.", "Back to the truck.", "See you at the truck.")), 3);
		}
	}

	public void OrderGrab(PlayerAvatar by, PhysGrabObject o, bool bringToPerson)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		if (!Alive || (Object)(object)o == (Object)null || o.dead)
		{
			return;
		}
		ClearOrder(say: false);
		HeardFrom(by);
		if (Claims.TryGetValue(o, out var value) && value != this)
		{
			value.DropJob("OrderGrab", 152);
		}
		Blacklist.Remove(o);
		Fails.Remove(o);
		_thingPos[o] = o.centerPoint;
		ValuableObject component = ((Component)o).GetComponent<ValuableObject>();
		if ((Object)(object)component != (Object)null)
		{
			Seen.Add(component);
		}
		PhysGrabCart val = null;
		PlayerAvatar val2 = null;
		ExtractionPoint val3 = ActiveExtraction();
		if (bringToPerson || (Object)(object)component == (Object)null)
		{
			val2 = by;
		}
		else if ((Object)(object)val3 == (Object)null)
		{
			val = KnownCart();
			if ((Object)(object)val == (Object)null)
			{
				val2 = by;
			}
		}
		else if (Plugin.BotsUseCarts.Value)
		{
			val = CartFor(o.centerPoint, ExtractionSpot(val3));
		}
		StartCarry(Job.Haul, o, val);
		_bringTo = val2;
		string text = ItemName(o);
		Say(Ack(((Object)(object)val2 != (Object)null) ? Pick("Bringing you the " + text + ".", "I'll get the " + text + " for you.") : ("Grabbing the " + text + ".")), 3);
	}

	public void OrderCart(PlayerAvatar by, PhysGrabCart c, bool toPerson)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		if (!Alive || (Object)(object)c == (Object)null)
		{
			return;
		}
		ClearOrder(say: false);
		HeardFrom(by);
		Carts.Remove(c);
		Carts.Add(c);
		_thingPos[c.physGrabObject] = ((Component)c).transform.position;
		if (Claims.TryGetValue(c.physGrabObject, out var value) && value != this)
		{
			value.DropJob("OrderCart", 188);
		}
		Blacklist.Remove(c.physGrabObject);
		HumanTouchedAt.Remove(c);
		ExtractionPoint val = ActiveExtraction();
		if (!toPerson && (Object)(object)val != (Object)null && c.itemsInCartCount > 0)
		{
			_driver = this;
			ForcePush(c, CartGoal.Extraction, ExtractionSpot(val));
			Say(Ack(Pick("Taking the cart to extraction.", "Cart's going in.")), 3);
		}
		else if (toPerson && (Object)(object)by != (Object)null)
		{
			Vector3 val2 = Flat(_bot.Position - ((Component)by).transform.position);
			Vector3 val3 = ((Component)by).transform.position + ((val2.sqrMagnitude > 0.01f) ? val2.normalized : ((Component)by).transform.forward) * 2.5f;
			NavMeshHit val4 = default;
			if (NavMesh.SamplePosition(val3, out val4, 2f, -1))
			{
				val3 = val4.position;
			}
			_driver = this;
			ForcePush(c, CartGoal.LootZone, val3);
			_cartForPerson = true;
			Say(Ack(Pick("Bringing the cart over.", "Cart's coming to you.")), 3);
		}
		else
		{
			_driver = this;
			_cartFan = 1;
			_cartDecideAt = 0f;
			Say(Ack(((Object)(object)val == (Object)null) ? Pick("I'll take the cart. I'll fill it up.", "Cart's mine. I'll load it.") : Pick("I'll drive the cart.", "I've got the cart.")), 3);
		}
	}

	private void ForcePush(PhysGrabCart cart, CartGoal goal, Vector3 dest)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		_detour = null;
		_routeWaitSince = -1f;
		_routeLogged = false;
		DropJob("ForcePush", 225);
		_job = Job.Cart;
		_pushing = cart;
		_cartGoal = goal;
		_cartDest = dest;
		_carry = Carry.Approach;
		_jobStarted = Time.time;
		Claims[cart.physGrabObject] = this;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)$"{_bot.Name} was told to push the cart ({goal})");
	}

	public bool OrderRevive(PlayerAvatar by, PlayerAvatar dead)
	{
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		if (!Alive || (Object)(object)dead == (Object)null || !dead.isDisabled || (Object)(object)dead.playerDeathHead == (Object)null)
		{
			return false;
		}
		if ((Object)(object)RoundDirector.instance != (Object)null && RoundDirector.instance.allExtractionPointsCompleted && SemiFunc.RunIsLevel())
		{
			Say(Pick("no need, extractions are all done", "theyll be fine, were done here"), 3);
			return true;
		}
		PlayerDeathHead playerDeathHead = dead.playerDeathHead;
		if (!playerDeathHead.triggered || (Object)(object)playerDeathHead.physGrabObject == (Object)null)
		{
			return false;
		}
		if (_job == Job.Revive && (Object)(object)_reviveOf == (Object)(object)dead)
		{
			Say(Pick("Already on it.", "Yeah, getting it.", "Working on it."), 3);
			return true;
		}
		ClearOrder(say: false);
		if (Claims.TryGetValue(playerDeathHead.physGrabObject, out var value) && value != this)
		{
			value.DropJob("OrderRevive", 257);
		}
		Blacklist.Remove(playerDeathHead.physGrabObject);
		_thingPos[playerDeathHead.physGrabObject] = playerDeathHead.physGrabObject.centerPoint;
		StartCarry(Job.Revive, playerDeathHead.physGrabObject, null);
		_reviveOf = dead;
		_reviveEP = ReviveExtraction(playerDeathHead.physGrabObject.centerPoint);
		Say(Ack(Pick("Getting " + dead.playerName + "'s head.", "I'll bring " + dead.playerName + " back.")), 3);
		return true;
	}

	public void OrderActivate(PlayerAvatar by, ExtractionPoint ep)
	{
		if (Alive && !((Object)(object)ep == (Object)null))
		{
			if (!ep.StateIs((ExtractionPoint.State)1) || ep.isLocked)
			{
				Say(Pick("That one can't be turned on right now.", "Can't use that one."), 3);
				return;
			}
			if ((Object)(object)RoundDirector.instance != (Object)null && RoundDirector.instance.extractionPointActive)
			{
				Say(Pick("There's already one going.", "Finish the other extraction first."), 3);
				return;
			}
			ClearOrder(say: false);
			DropJob("OrderActivate", 282);
			_job = Job.Activate;
			_activateTarget = ep;
			_activator = this;
			_jobStarted = Time.time;
			Say(Ack("Turning it on."), 3);
		}
	}

	private bool RunOrder()
	{
		if (_order == OrderKind.None)
		{
			return false;
		}
		if (Time.time > _orderUntil || (_order == OrderKind.Follow && ((Object)(object)_orderBy == (Object)null || _orderBy.isDisabled)))
		{
			ClearOrder(say: false);
			return false;
		}
		if (_order == OrderKind.Follow)
		{
			if (_job == Job.Assist && AssistStillNeeded())
			{
				return true;
			}
			if (_bot.HeldView == 0 && TryHelpPerson())
			{
				return true;
			}
		}
		if (_job != Job.Order)
		{
			if (_job == Job.Assist)
			{
				StopAssist();
			}
			if (_bot.HeldView != 0)
			{
				ReleaseHeld("RunOrder", 315);
			}
			_job = Job.Order;
			_corners.Clear();
		}
		return true;
	}

	private Vector3 DoOrder(out bool sprint, out Vector3? lookAt)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0323: Unknown result type (might be due to invalid IL or missing references)
		//IL_0328: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0333: Unknown result type (might be due to invalid IL or missing references)
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_026b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_0346: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_0368: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0307: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0254: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		sprint = false;
		lookAt = null;
		Vector3 position = _bot.Position;
		Vector3 result;
		Vector3 val;
		switch (_order)
		{
		case OrderKind.Follow:
		{
			result = DoFollow(out sprint, _orderBy);
			Vector3? val2 = (((Object)(object)_orderBy != (Object)null) ? PersonPos(_orderBy) : ((Vector3?)null));
			if ((Object)(object)_orderBy != (Object)null)
			{
				if (!val2.HasValue)
				{
					goto IL_00d2;
				}
				if (!SeesPerson(_orderBy))
				{
					val = Flat(val2.Value - position);
					if (val.magnitude < 2.5f)
					{
						goto IL_00d2;
					}
				}
			}
			_orderLostSince = -1f;
			return result;
		}
		case OrderKind.Come:
		{
			if ((Object)(object)_orderBy != (Object)null && SeesPerson(_orderBy))
			{
				_orderPos = ((Component)_orderBy).transform.position;
			}
			val = Flat(_orderPos - position);
			float magnitude2 = val.magnitude;
			if (magnitude2 < 2.5f && Mathf.Abs(_orderPos.y - position.y) < 2f)
			{
				_order = OrderKind.Hold;
				_orderPos = position;
				_orderUntil = Time.time + 60f;
				Say(Line("arrived"), 3);
				return Vector3.zero;
			}
			sprint = magnitude2 > 8f;
			return Steer(position, _orderPos) * (sprint ? 5.6f : 3.2f);
		}
		case OrderKind.GoTo:
		case OrderKind.Truck:
		{
			val = Flat(_orderPos - position);
			float magnitude = val.magnitude;
			if (magnitude < ((_order == OrderKind.Truck) ? 2.5f : 1.2f) && Mathf.Abs(_orderPos.y - position.y) < 2f)
			{
				if (!_orderArrived)
				{
					_orderArrived = true;
					if (_order == OrderKind.GoTo)
					{
						Say(Line("arrived"), 1);
					}
				}
				LookAroundWhileWaiting(ref lookAt);
				return Vector3.zero;
			}
			sprint = magnitude > 15f;
			return Steer(position, _orderPos) * (sprint ? 5.6f : 3.2f);
		}
		case OrderKind.Hold:
			val = Flat(_orderPos - position);
			if (val.magnitude > 1.5f)
			{
				return Steer(position, _orderPos) * 3.2f;
			}
			LookAroundWhileWaiting(ref lookAt);
			return Vector3.zero;
		default:
			{
				return Vector3.zero;
			}
			IL_00d2:
			if (_orderLostSince < 0f)
			{
				_orderLostSince = Time.time;
				Say(Line("lost", null, null, null, null, _orderBy.playerName), 1);
			}
			else if (Time.time - _orderLostSince > 30f)
			{
				Say(Pick("lost you, going back to work", "cant find you, ill go loot", "where did you go, ok im going back to work"), 1);
				ClearOrder(say: false);
			}
			else
			{
				LookAroundWhileWaiting(ref lookAt);
			}
			return result;
		}
	}

	private void LookAroundWhileWaiting(ref Vector3? lookAt)
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_orderBy != (Object)null && SeesPerson(_orderBy) && Time.time % 6f < 2f)
		{
			lookAt = ((Component)_orderBy).transform.position + Vector3.up * 1.4f;
			return;
		}
		if (Time.time >= _scanAt)
		{
			_scanAt = Time.time + Random.Range(1.5f, 3.5f);
			_scanYaw = _bot.Yaw + Random.Range(-120f, 120f);
		}
		lookAt = Eye() + Quaternion.Euler(0f, _scanYaw, 0f) * Vector3.forward * 5f;
	}

	private string OrderDoing()
	{
		switch (_order)
		{
		case OrderKind.Follow:
			if (!((Object)(object)_orderBy != (Object)null))
			{
				return "Following you";
			}
			return "Following " + _orderBy.playerName + ", like I was told";
		case OrderKind.Come:
			if (!((Object)(object)_orderBy != (Object)null))
			{
				return "Coming to you";
			}
			return "Coming to " + _orderBy.playerName;
		case OrderKind.Hold:
			return "Waiting here, like you said";
		case OrderKind.GoTo:
			if (!_orderArrived)
			{
				return "Going where you sent me";
			}
			return "Waiting where you sent me";
		case OrderKind.Truck:
			return "Waiting at the truck";
		default:
			return "Nothing much";
		}
	}

	public void Answer(BotChat.Ask ask, PlayerAvatar asker, string subject)
	{
		string text = AnswerText(ask, asker, subject);
		if (text != null)
		{
			Say(text, 3);
		}
	}

	private string AnswerText(BotChat.Ask ask, PlayerAvatar asker, string subject)
	{
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_037f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0551: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = _bot.Avatar;
		switch (ask)
		{
		case BotChat.Ask.WhereAreYou:
		{
			if (avatar.isDisabled)
			{
				if (!((Object)(object)avatar.playerDeathHead != (Object)null) || !((Object)(object)avatar.playerDeathHead.physGrabObject != (Object)null))
				{
					return "I'm dead.";
				}
				return "I'm dead. My head's " + Where(avatar.playerDeathHead.physGrabObject.centerPoint, forListener: true, asker) + ".";
			}
			if ((Object)(object)asker != (Object)null && Vector3.Distance(((Component)asker).transform.position, _bot.Position) < 4f)
			{
				return Pick("Right here.", "Right next to you.");
			}
			string text = RoomName(_bot.Position);
			string text2 = (((Object)(object)asker != (Object)null) ? Where(_bot.Position, forListener: true, asker) : null);
			if (text2 != null && text != null && !text2.Contains(text))
			{
				return "im in " + text + ", " + text2;
			}
			if (text2 == null)
			{
				if (text == null)
				{
					return "somewhere in here";
				}
				return "im in " + text;
			}
			return "im " + text2;
		}
		case BotChat.Ask.Doing:
			return DoingNow() + ".";
		case BotChat.Ask.Health:
		{
			if (avatar.isDisabled)
			{
				return "Dead. Not great.";
			}
			int health = avatar.playerHealth.health;
			int maxHealth = avatar.playerHealth.maxHealth;
			if (health >= maxHealth)
			{
				return Pick("Full health.", "Never better.", "All " + Words(health) + " health.");
			}
			if (health >= 35)
			{
				return "I'm at " + Words(health) + " out of " + Words(maxHealth) + ".";
			}
			return "Not good, " + Words(health) + " health.";
		}
		case BotChat.Ask.Money:
		{
			if (SemiFunc.RunIsShop() && (Object)(object)ShopManager.instance != (Object)null)
			{
				return ShopMoneyAnswer();
			}
			StringBuilder stringBuilder = new StringBuilder();
			PhysGrabCart val2 = NearestKnownCart();
			if ((Object)(object)val2 != (Object)null)
			{
				stringBuilder.Append((val2.haulCurrent > 0) ? ("Cart's got " + Money(val2.haulCurrent) + ". ") : "Cart's empty. ");
			}
			RoundDirector instance = RoundDirector.instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)ActiveExtraction() != (Object)null && instance.extractionHaulGoal > 0)
			{
				stringBuilder.Append("Extraction needs " + Money(Mathf.Max(0, instance.extractionHaulGoal - instance.currentHaul)) + " more.");
			}
			else if ((Object)(object)instance != (Object)null)
			{
				stringBuilder.Append(Words(instance.extractionPointsCompleted) + " of " + Words(instance.extractionPoints) + " extractions done.");
			}
			return stringBuilder.ToString().Trim();
		}
		case BotChat.Ask.Monsters:
		{
			List<string> list = new List<string>();
			foreach (Threat item in KnownThreats())
			{
				if (item.Rule != ThreatRule.Harmless || item.Aggressive)
				{
					list.Add(MonsterName(item.Kind) + " " + Where(item.Position, forListener: true, asker));
				}
			}
			if (list.Count == 0)
			{
				return Pick("Nothing right now.", "Haven't seen anything lately.", "All clear, as far as I know.");
			}
			return string.Join(". ", list.GetRange(0, Mathf.Min(3, list.Count))) + ".";
		}
		case BotChat.Ask.Loot:
		{
			ValuableObject val = null;
			int num = 0;
			foreach (ValuableObject item2 in Seen)
			{
				if (!((Object)(object)item2 == (Object)null) && !((Object)(object)item2.physGrabObject == (Object)null) && !item2.physGrabObject.dead && !InSomeCart(item2.physGrabObject) && (!((Object)(object)item2.physGrabObject.roomVolumeCheck != (Object)null) || !item2.physGrabObject.roomVolumeCheck.inExtractionPoint))
				{
					num++;
					if ((Object)(object)val == (Object)null || item2.dollarValueCurrent > val.dollarValueCurrent)
					{
						val = item2;
					}
				}
			}
			if ((Object)(object)val == (Object)null)
			{
				return Pick("Haven't found anything left.", "Nothing I know of that isn't already in.");
			}
			_thingPos.TryGetValue(val.physGrabObject, out var value);
			return "I know " + Words(num) + " things. Best is a " + ItemName(val.physGrabObject) + ", " + Money(val.dollarValueCurrent) + ", " + Where(value, forListener: true, asker) + ".";
		}
		case BotChat.Ask.WhereThing:
			return WhereIs(subject, asker);
		case BotChat.Ask.WhoAreYou:
			if (Hist("levels") <= 1)
			{
				return "I'm " + _bot.Name + ". People say I'm " + P.Describe() + ".";
			}
			return "I'm " + _bot.Name + ". People say I'm " + P.Describe() + ". " + HistoryText() + ".";
		case BotChat.Ask.Stats:
			return HistoryText();
		case BotChat.Ask.Hello:
			return Line("hello", null, null, null, null, asker?.playerName);
		case BotChat.Ask.Thanks:
			return Line("thanks");
		case BotChat.Ask.Insult:
			return Line("insult");
		case BotChat.Ask.Sorry:
			return Line("sorry");
		case BotChat.Ask.Laugh:
			if (!(P.Chatty > 0.4f))
			{
				return null;
			}
			return Line("laugh");
		case BotChat.Ask.Unknown:
			return Line("unknown");
		default:
			return null;
		}
	}

	private string WhereIs(string subject, PlayerAvatar asker)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_037b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_027c: Unknown result type (might be due to invalid IL or missing references)
		switch (subject)
		{
		case "cart":
		{
			PhysGrabCart val3 = NearestKnownCart();
			if ((Object)(object)val3 == (Object)null || !_thingPos.TryGetValue(val3.physGrabObject, out var value2))
			{
				return "Haven't seen the cart.";
			}
			return "Cart's " + Where(value2, forListener: true, asker) + ".";
		}
		case "extraction":
		case "truck":
		{
			Vector3? val4 = null;
			if (subject == "truck")
			{
				if ((Object)(object)TruckSafetySpawnPoint.instance != (Object)null)
				{
					val4 = ((Component)TruckSafetySpawnPoint.instance).transform.position;
				}
			}
			else
			{
				ExtractionPoint val5 = ActiveExtraction();
				if ((Object)(object)val5 == (Object)null)
				{
					return "none's turned on right now";
				}
				val4 = ExtractionSpot(val5);
			}
			if (!val4.HasValue)
			{
				return "no idea";
			}
			if (!Remembers(RoomAt(val4.Value)))
			{
				PlayerAvatar who = asker;
				string what = subject;
				if (CheckMap("asked where the " + subject + " is", () =>
				{
					Say(Casual(WhereIs(what, who)), 3);
				}))
				{
					return Line("mapcheck");
				}
				return "not sure, i think its " + Where(val4.Value, forListener: true, asker);
			}
			return ((subject == "truck") ? "truck's " : "extraction's ") + Where(val4.Value, forListener: true, asker);
		}
		case "monster":
			return AnswerText(BotChat.Ask.Monsters, asker, null);
		case "loot":
			return AnswerText(BotChat.Ask.Loot, asker, null);
		default:
			foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
			{
				if ((Object)(object)player == (Object)null || player.playerName == null || player.playerName.ToLowerInvariant() != subject)
				{
					continue;
				}
				if ((Object)(object)player == (Object)(object)_bot.Avatar)
				{
					return AnswerText(BotChat.Ask.WhereAreYou, asker, null);
				}
				if (player.isDisabled)
				{
					PhysGrabObject val = (((Object)(object)player.playerDeathHead != (Object)null) ? player.playerDeathHead.physGrabObject : null);
					if ((Object)(object)val != (Object)null && _thingPos.TryGetValue(val, out var value))
					{
						return player.playerName + " is dead. Head's " + Where(value, forListener: true, asker) + ".";
					}
					return player.playerName + " is dead, I don't know where the head is.";
				}
				if (BotRegistry.IsBot(player))
				{
					BotInfo botInfo = BotRegistry.Get(player);
					if (botInfo != null && Vector3.Distance(botInfo.Position, _bot.Position) < 25f)
					{
						return player.playerName + " is " + Where(botInfo.Position, forListener: true, asker) + ".";
					}
					return "Haven't seen " + player.playerName + " in a bit.";
				}
				Vector3? val2 = PersonPos(player);
				return val2.HasValue ? ("Last I saw, " + player.playerName + " was " + Where(val2.Value, forListener: true, asker) + ".") : ("No idea where " + player.playerName + " is.");
			}
			return "Not sure.";
		}
	}

	private void PitGuard(ref Vector3 desired)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = _bot.Position;
		Vector3 val = Flat(desired);
		Vector3 val2 = (((Object)(object)_body != (Object)null && (Object)(object)_body.Rb != (Object)null) ? Flat(_body.Rb.velocity) : Flat(_bot.Velocity));
		if ((Object)(object)_body != (Object)null && !_body.Grounded)
		{
			return;
		}
		if (val.magnitude < 0.05f && val2.magnitude < 0.3f)
		{
			_pitBlockedSince = -1f;
		}
		else
		{
			if (BotPits.IsPit(position))
			{
				return;
			}
			bool flag = val2.magnitude > 0.3f && PitAhead(position, val2.normalized, val2.magnitude);
			bool flag2 = val.magnitude >= 0.05f && PitAhead(position, val.normalized, Mathf.Max(val.magnitude, val2.magnitude));
			if (!flag && !flag2)
			{
				_pitBlockedSince = -1f;
				return;
			}
			if (_pitBlockedSince < 0f)
			{
				_pitBlockedSince = Time.time;
			}
			if (Time.time - _pitLogAt > 5f)
			{
				_pitLogAt = Time.time;
				Plugin.Log.LogInfo((object)$"{_bot.Name} stopped at the edge of a pit ({_job}, at {position.x:F1},{position.y:F1},{position.z:F1}: {BotPits.LastWhy})");
			}
			float num = Mathf.Max(val.magnitude, 0.5f);
			if (flag2 && val.magnitude >= 0.05f)
			{
				Vector3 normalized = val.normalized;
				float[] array = new float[6] { 35f, -35f, 65f, -65f, 95f, -95f };
				foreach (float num2 in array)
				{
					Vector3 val3 = Quaternion.Euler(0f, num2, 0f) * normalized;
					if (!PitAhead(position, val3, num) && (!flag || Vector3.Dot(val3, val2.normalized) < 0.7f))
					{
						desired = val3 * Mathf.Min(num, 3.2f) * 0.7f;
						return;
					}
				}
			}
			Vector3 val4 = (flag ? (-val2.normalized) : (-val.normalized));
			desired = val4 * Mathf.Min(num, 3.2f) * 0.6f;
			_sidestepUntil = 0f;
			_corners.Clear();
		}
	}

	private static bool PitAhead(Vector3 pos, Vector3 dir, float speed)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		float num = Mathf.Clamp(0.55f + speed * 0.22f, 0.7f, 1.6f);
		if (!BotPits.IsPit(pos + dir * 0.45f))
		{
			return BotPits.IsPit(pos + dir * num);
		}
		return true;
	}

	private static float SafeBumpSpeed(PhysGrabObject o)
	{
		PhysGrabObjectImpactDetector val = (((Object)(object)o != (Object)null) ? o.impactDetector : null);
		if ((Object)(object)val == (Object)null || val.isIndestructible || (Object)(object)((Component)o).GetComponent<ValuableObject>() == (Object)null)
		{
			return 99f;
		}
		float num = val.fragility * val.fragilityMultiplier;
		if (!(num <= 1f))
		{
			return 37.5f / num;
		}
		return 99f;
	}

	private bool WantsGentle(PhysGrabObject held)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)held == (Object)null || held.dead || _dropNow || (Object)(object)((Component)held).GetComponent<ValuableObject>() == (Object)null)
		{
			return false;
		}
		if ((Object)(object)held.impactDetector != (Object)null && (held.impactDetector.inCart || held.impactDetector.isIndestructible))
		{
			return false;
		}
		if (Vector3.Distance(held.centerPoint, HoldPoint(withLift: false)) < 2.2f)
		{
			return !IsHeavy(held, HeavyLimit);
		}
		return false;
	}

	private void BeginSetDown(PhysGrabObject held, string why)
	{
		if (!((Object)(object)_setDown == (Object)(object)held))
		{
			_setDown = held;
			_setDownStarted = Time.time;
			_setDownRestingSince = -1f;
			_setDownWhy = why;
		}
	}

	private bool SetDownTick(out Vector3? lookAt)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		if ((Object)(object)_setDown == (Object)null)
		{
			return false;
		}
		PhysGrabObject val = BotGrab.Held(_bot);
		if ((Object)(object)val != (Object)(object)_setDown || (Object)(object)val == (Object)null || val.dead || _bot.HeldView == 0)
		{
			_setDown = null;
			return false;
		}
		lookAt = val.centerPoint;
		bool flag = Time.time - _bot.LastHurtTime < 1f;
		if (!flag)
		{
			foreach (Threat item in KnownThreats())
			{
				if (item.Rule != ThreatRule.Harmless && Vector3.Distance(item.Position, _bot.Position) < 5f)
				{
					flag = true;
					break;
				}
			}
		}
		bool flag2 = _setDownRestingSince > 0f && Time.time - _setDownRestingSince > 0.15f;
		if ((flag | flag2) || Time.time - _setDownStarted > 2.5f)
		{
			BotBrain botBrain = this;
			PhysGrabObject held = val;
			string how;
			if (flag)
			{
				how = "hurry";
			}
			else
			{
				how = (flag2 ? "set down" : "timed out");
			}
			botBrain.FinishSetDown(held, how);
		}
		return true;
	}

	private void FinishSetDown(PhysGrabObject held, string how)
	{
		_setDown = null;
		BotCareLog.Released(_bot, held, _setDownWhy + ", " + how);
		BotGrab.Release(_bot);
	}

	private void SetDownPuller()
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabObject setDown = _setDown;
		Vector3 position = _bot.Avatar.physGrabber.physGrabPoint.position;
		float num = 2f;
		RaycastHit val = default;
		if ((Object)(object)setDown.rb != (Object)null && setDown.rb.SweepTest(Vector3.down, out val, 2f, (QueryTriggerInteraction)1))
		{
			num = val.distance;
		}
		float num2;
		if (!((Object)(object)setDown.rb != (Object)null))
		{
			num2 = 0f;
		}
		else
		{
			Vector3 velocity = setDown.rb.velocity;
			num2 = velocity.magnitude;
		}
		float num3 = num2;
		if (num < 0.03f && num3 < 0.15f)
		{
			if (_setDownRestingSince < 0f)
			{
				_setDownRestingSince = Time.time;
			}
			_bot.Puller = position;
		}
		else
		{
			_setDownRestingSince = -1f;
			float num4 = Mathf.Clamp(num * 0.5f, 0.03f, 0.25f);
			_bot.Puller = position + Vector3.down * Mathf.Min(num4, num + 0.01f);
		}
	}

	internal string CareInfo(PhysGrabObject o)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = Flat(_bot.Velocity);
		float magnitude = val.magnitude;
		return string.Format("{0}/{1}{2}, walking {3:0.0} m/s, {4:0.0}m from hands, {5:0.0}s after grabbing", _job, _carry, ((Object)(object)_setDown != (Object)null) ? " setting down" : "", magnitude, Vector3.Distance(o.centerPoint, HoldPoint(withLift: false)), Time.time - _grabbedAt);
	}

	internal static void ShopStopNow()
	{
		_stopVisit = (SemiFunc.RunIsShop() ? _shopVisitNo : (_shopVisitNo + 1));
		_wantVisit = -1;
	}

	internal static void ShopWantNow(string what)
	{
		_wantVisit = (SemiFunc.RunIsShop() ? _shopVisitNo : (_shopVisitNo + 1));
		_shopWant = what;
		_stopVisit = -1;
	}

	private static ExtractionPoint ShopZone()
	{
		Transform val = (((Object)(object)ShopManager.instance != (Object)null) ? ShopManager.instance.extractionPoint : null);
		if (!((Object)(object)val != (Object)null))
		{
			return null;
		}
		return ((Component)val).GetComponent<ExtractionPoint>();
	}

	private static bool ZoneReady(ExtractionPoint z)
	{
		if (!z.StateIs((ExtractionPoint.State)2))
		{
			return z.StateIs((ExtractionPoint.State)1);
		}
		return true;
	}

	private static bool InZone(ItemAttributes ia)
	{
		if ((Object)(object)ia != (Object)null && (Object)(object)ia.roomVolumeCheck != (Object)null)
		{
			return ia.roomVolumeCheck.inExtractionPoint;
		}
		return false;
	}

	private static void ShopTeam()
	{
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		if (_shopVisit != (Object)(object)LevelGenerator.Instance)
		{
			_shopVisit = (Object)(object)LevelGenerator.Instance;
			_shopVisitNo++;
			_shopSince = Time.time;
			ShopStock.Clear();
			ShopBought.Clear();
			NewShopWants();
			ShelfSpot.Clear();
			BadShelves.Clear();
			WentShopping.Clear();
			_budgetSaid = (_doneSaid = (_buttonSaid = false));
			_stockAt = 0f;
			Plugin.Log.LogInfo((object)$"Shop visit {_shopVisitNo}: ${Currency}k to spend");
		}
		if (Time.time >= _stockAt)
		{
			_stockAt = Time.time + 2f;
			ShopStock.Clear();
			ItemAttributes[] array = Object.FindObjectsOfType<ItemAttributes>();
			foreach (ItemAttributes val in array)
			{
				if ((Object)(object)val == (Object)null || (Object)(object)val.item == (Object)null || !val.shopItem || val.value <= 0)
				{
					continue;
				}
				PhysGrabObject component = ((Component)val).GetComponent<PhysGrabObject>();
				if (!((Object)(object)component == (Object)null) && !component.dead)
				{
					ShopStock.Add(val);
					if (!ShelfSpot.ContainsKey(val) && !InZone(val) && component.playerGrabbing.Count == 0)
					{
						ShelfSpot[val] = component.centerPoint;
					}
				}
			}
			List<ItemAttributes> list = new List<ItemAttributes>();
			foreach (KeyValuePair<ItemAttributes, BotBrain> item in ShopBought)
			{
				if ((Object)(object)item.Key == (Object)null || item.Value == null)
				{
					list.Add(item.Key);
				}
			}
			foreach (ItemAttributes item2 in list)
			{
				ShopBought.Remove(item2);
			}
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && !BotRegistry.IsBot(player) && !player.isDisabled && (Object)(object)player.RoomVolumeCheck != (Object)null && !player.RoomVolumeCheck.inTruck)
			{
				WentShopping.Add(player);
			}
		}
	}

	private static bool ShopTimeUp()
	{
		bool flag = false;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && !BotRegistry.IsBot(player) && !player.isDisabled)
			{
				flag = true;
				if ((Object)(object)player.RoomVolumeCheck == (Object)null || !player.RoomVolumeCheck.inTruck)
				{
					return false;
				}
			}
		}
		if (flag)
		{
			if (WentShopping.Count <= 0)
			{
				return Time.time - _shopSince > 120f;
			}
			return true;
		}
		return false;
	}

	private static int ChargeTotal()
	{
		if ((Object)(object)StatsManager.instance != (Object)null && StatsManager.instance.runStats.TryGetValue("chargingStationChargeTotal", out var value))
		{
			return value;
		}
		return 100;
	}

	private static string ShopKind(ItemAttributes ia)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected I4, but got Unknown
		var itemType = ia.item.itemType;
		switch ((int)itemType - 2)
		{
		case 6:
			return "health";
		case 3:
			return "crystal";
		case 1:
		case 2:
			return "upgrade";
		case 4:
		case 5:
		case 7:
		case 9:
		case 13:
			return "weapon";
		case 0:
		case 10:
		case 12:
			return "cart";
		default:
			return "gadget";
		}
	}

	internal static string ShopName(ItemAttributes ia)
	{
		return Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace((((Object)(object)ia != (Object)null && (Object)(object)ia.item != (Object)null) ? ia.item.itemName : "thing") ?? "thing", "\\(\\s*\\d+\\s*\\)|\\[\\s*\\d+\\s*\\]", " "), "[()\\[\\]]", " "), "^\\s*item\\s+", "", RegexOptions.IgnoreCase), "\\s+", " ").Trim().ToLowerInvariant();
	}

	private static List<PlayerAvatar> Hurt()
	{
		List<PlayerAvatar> list = new List<PlayerAvatar>();
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if (!((Object)(object)player == (Object)null) && !player.isDisabled && !((Object)(object)player.playerHealth == (Object)null) && player.playerHealth.maxHealth > 0 && (float)player.playerHealth.health < (float)player.playerHealth.maxHealth * 0.7f)
			{
				list.Add(player);
			}
		}
		list.Sort((PlayerAvatar a, PlayerAvatar b) => a.playerHealth.health.CompareTo(b.playerHealth.health));
		return list;
	}

	private static void Queued(out Dictionary<string, int> kinds, out HashSet<string> names, out int botSpent)
	{
		kinds = new Dictionary<string, int>();
		names = new HashSet<string>();
		botSpent = 0;
		foreach (ItemAttributes item in ShopStock)
		{
			bool flag = InZone(item);
			PhysGrabObject component = ((Component)item).GetComponent<PhysGrabObject>();
			bool flag2 = (Object)(object)component != (Object)null && Claims.TryGetValue(component, out var value) && (Object)(object)value._shopItem == (Object)(object)item && !value._shopPutBack;
			if (flag || flag2)
			{
				string key = ShopKind(item);
				kinds.TryGetValue(key, out var value2);
				kinds[key] = value2 + 1;
				names.Add(((Object)item.item).name);
				if (flag2 || ShopBought.ContainsKey(item))
				{
					botSpent += item.value;
				}
			}
		}
	}

	private int BotBudget()
	{
		int num = 0;
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && !BotRegistry.IsBot(player))
			{
				num++;
			}
		}
		int num2 = Mathf.Max(1, BotRegistry.Bots.Count);
		float num3 = Mathf.Clamp((float)num2 / (float)(num2 + Mathf.Max(1, num)), 0.25f, 0.5f);
		if (ShopWant != null)
		{
			num3 = 1f;
		}
		return Mathf.FloorToInt((float)Currency * num3 * Mathf.Lerp(1.1f, 0.7f, P.Greed));
	}

	private float ShopScore(ItemAttributes ia, Dictionary<string, int> kinds, HashSet<string> names, int hurt, int left)
	{
		string text = ShopKind(ia);
		kinds.TryGetValue(text, out var value);
		float num;
		switch (text)
		{
		case "health":
			if (value < hurt)
			{
				num = 100f;
			}
			else
			{
				num = ((value == 0 && left >= ia.value * 4) ? 35f : 0f);
			}
			break;
		case "crystal":
		{
			int num2 = ChargeTotal() + 17 * value;
			if (num2 < 40)
			{
				num = 85f;
			}
			else
			{
				num = ((num2 < 60 && left >= ia.value * 3) ? 25f : 0f);
			}
			break;
		}
		case "upgrade":
			num = (names.Contains(((Object)ia.item).name) ? 10f : 55f);
			break;
		case "weapon":
			num = ((value >= 2) ? 0f : (25f + (1f - P.Brave) * 25f));
			break;
		case "cart":
			num = 0f;
			break;
		default:
			num = (names.Contains(((Object)ia.item).name) ? 0f : (15f + ((P.Greed < 0.3f) ? 10f : 0f)));
			break;
		}
		string shopWant = ShopWant;
		if (shopWant != null && shopWant != "any")
		{
			num = ((text == shopWant) ? (Mathf.Max(num, 60f) + 100f) : 0f);
		}
		if (num <= 0f)
		{
			return 0f;
		}
		if (num < 50f && ia.value * 2 > left)
		{
			return 0f;
		}
		return num - (float)ia.value * 1.5f;
	}

	private static bool BadShelf(ItemAttributes ia)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)ia).transform.position;
		foreach (Vector3 badShelf in BadShelves)
		{
			Vector3 val = badShelf - position;
			if (val.sqrMagnitude < 6.25f)
			{
				return true;
			}
		}
		return false;
	}

	private void ShopJobEnded(string who)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)_shopItem == (Object)null) && !_shopPutBack && _bot.HeldView == 0 && _carry == Carry.Approach && (!(who != "CheckStuck") || NoWayThere) && !InZone(_shopItem))
		{
			BadShelves.Add(((Component)_shopItem).transform.position);
			Plugin.Log.LogInfo((object)(_bot.Name + " couldn't get to the " + _shopItem.item.itemName + " (" + who + "), skipping that shelf"));
		}
	}

	private bool TryStartShop()
	{
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Unknown result type (might be due to invalid IL or missing references)
		if (!SemiFunc.RunIsShop() || (Object)(object)ShopManager.instance == (Object)null || Time.time < _shopCheckAt)
		{
			return false;
		}
		_shopCheckAt = Time.time + 1.5f;
		if (!Plugin.BotsShop.Value || _bot.HeldView != 0 || !Alive || (Object)(object)LevelGenerator.Instance == (Object)null || !LevelGenerator.Instance.Generated)
		{
			return false;
		}
		ShopTeam();
		ExtractionPoint val = ShopZone();
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		if (TryPutBack(val))
		{
			return true;
		}
		if (ShopStopped || ShopTimeUp() || !ZoneReady(val))
		{
			return false;
		}
		Queued(out var kinds, out var names, out var botSpent);
		int num = BotBudget();
		int num2 = Mathf.Min(num - botSpent, Currency - ZoneCost);
		if (!_budgetSaid && Time.time - _shopSince > 4f)
		{
			_budgetSaid = true;
			Say(Line((Currency > 0) ? "shopbudget" : "shopbroke", null, null, null, Money((float)Currency * 1000f)), 1, "shopbudget", 999f);
		}
		List<PlayerAvatar> list = Hurt();
		ItemAttributes val2 = null;
		float num3 = 0f;
		foreach (ItemAttributes item2 in ShopStock)
		{
			if (item2.value > Currency - ZoneCost || InZone(item2) || BadShelf(item2))
			{
				continue;
			}
			PhysGrabObject component = ((Component)item2).GetComponent<PhysGrabObject>();
			if (!Available(component) || component.rb.isKinematic || ((Object)(object)item2.roomVolumeCheck != (Object)null && item2.roomVolumeCheck.inTruck))
			{
				continue;
			}
			Vector3 centerPoint = component.centerPoint;
			float num4 = Vector3.Distance(centerPoint, _bot.Position);
			if (num4 > 70f || !OnNavmesh(centerPoint))
			{
				continue;
			}
			float num5 = ShopScore(item2, kinds, names, list.Count, num2);
			if (!(num5 <= 0f) && item2.value <= ((num5 >= 80f) ? (Currency - ZoneCost) : num2))
			{
				num5 -= num4 * 0.3f;
				if (num5 > num3)
				{
					num3 = num5;
					val2 = item2;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			if (Time.time >= _shopNothingLogAt)
			{
				_shopNothingLogAt = Time.time + 30f;
				Plugin.Log.LogInfo((object)$"{_bot.Name} has nothing to buy (stock {ShopStock.Count}, ${num2}k left of ${num}k bot budget, zone ${ZoneCost}k of ${Currency}k)");
			}
			ShopDoneTalk(val);
			return false;
		}
		PhysGrabObject component2 = ((Component)val2).GetComponent<PhysGrabObject>();
		DropJob("TryStartShop", 344);
		StartCarry(Job.Haul, component2, null);
		_shopItem = val2;
		_shopPutBack = false;
		_thingPos[component2] = component2.centerPoint;
		Plugin.Log.LogInfo((object)$"{_bot.Name} is buying {val2.item.itemName} for ${val2.value}k (team has ${Currency}k, zone ${ZoneCost}k, bot budget ${num}k, bots spent ${botSpent}k)");
		string item = ShopName(val2);
		if (ShopKind(val2) == "health" && list.Count > 0 && (Object)(object)list[0] != (Object)(object)_bot.Avatar)
		{
			Say(Line("shopheal", null, null, item, null, list[0].playerName), 1, "shopgrab:" + _bot.Name, 15f);
		}
		else
		{
			Say(Line("shopgrab", null, null, item, Money((float)val2.value * 1000f)), 1, "shopgrab:" + _bot.Name, 15f);
		}
		return true;
	}

	private bool TryPutBack(ExtractionPoint zone)
	{
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		int num = ZoneCost - Currency;
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain != this && (Object)(object)brain._shopItem != (Object)null && brain._shopPutBack && InZone(brain._shopItem))
			{
				num -= brain._shopItem.value;
			}
		}
		bool flag = num > 0;
		if (!flag && !ShopStopped)
		{
			return false;
		}
		if (flag && Time.time - _overSaidAt > 25f)
		{
			_overSaidAt = Time.time;
			Say(Line("shopover", null, null, null, Money((float)(ZoneCost - Currency) * 1000f)), 1, "shopover", 25f);
		}
		ItemAttributes val = null;
		float num2 = float.MaxValue;
		Queued(out var kinds, out var names, out var _);
		foreach (KeyValuePair<ItemAttributes, BotBrain> item in ShopBought)
		{
			if (item.Value != this || (Object)(object)item.Key == (Object)null || !InZone(item.Key))
			{
				continue;
			}
			PhysGrabObject component = ((Component)item.Key).GetComponent<PhysGrabObject>();
			if (!((Object)(object)component == (Object)null) && !component.dead && !HumanHolds(component) && (!Claims.TryGetValue(component, out var value) || value == this))
			{
				float num3 = ShopScore(item.Key, kinds, names, Hurt().Count, int.MaxValue);
				if (num3 < num2)
				{
					num2 = num3;
					val = item.Key;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		PhysGrabObject component2 = ((Component)val).GetComponent<PhysGrabObject>();
		DropJob("TryPutBack", 395);
		StartCarry(Job.Haul, component2, null);
		_shopItem = val;
		_shopPutBack = true;
		_thingPos[component2] = component2.centerPoint;
		Blacklist.Remove(component2);
		Plugin.Log.LogInfo((object)(_bot.Name + " is putting " + val.item.itemName + " back (" + (flag ? $"zone ${ZoneCost}k > ${Currency}k" : "told not to buy") + ")"));
		Say(Line("shopputback", null, null, ShopName(val)), 1, "shopputback:" + _bot.Name, 10f);
		return true;
	}

	private void ShopDoneTalk(ExtractionPoint zone)
	{
		bool flag = false;
		foreach (KeyValuePair<ItemAttributes, BotBrain> item in ShopBought)
		{
			if ((Object)(object)item.Key != (Object)null && InZone(item.Key))
			{
				flag = true;
			}
		}
		if (!_doneSaid & flag)
		{
			foreach (BotBrain brain in BotManager.Brains)
			{
				if (brain != null && (Object)(object)brain._shopItem != (Object)null)
				{
					return;
				}
			}
			_doneSaid = true;
			Say(Line("shopdone", null, null, null, Money((float)Mathf.Max(0, Currency - ZoneCost) * 1000f)), 1, "shopdone", 999f);
		}
		else if (((_doneSaid && !_buttonSaid) & flag) && Time.time - _shopSince > 60f && ZoneReady(zone))
		{
			_buttonSaid = true;
			Say(Line("shopbutton"), 1, "shopbutton", 999f);
		}
	}

	private bool ShopCarry(PhysGrabObject t, Vector3 pos, out Vector3 dest, out bool arrived)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_038c: Unknown result type (might be due to invalid IL or missing references)
		//IL_038d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_039b: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_0344: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0361: Unknown result type (might be due to invalid IL or missing references)
		//IL_0362: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0370: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_037f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		dest = pos;
		arrived = false;
		ItemAttributes shopItem = _shopItem;
		ExtractionPoint val = ShopZone();
		if ((Object)(object)shopItem == (Object)null || (Object)(object)val == (Object)null || !SemiFunc.RunIsShop())
		{
			DropJob("ShopCarry", 437);
			return false;
		}
		ShopTeam();
		bool flag = InZone(shopItem);
		Vector3 val2 = ExtractionSpot(val);
		if (ShopTimeUp() && !_shopPutBack)
		{
			ReleaseHeld("ShopCarry", 446);
			DropJob("ShopCarry", 447);
			return false;
		}
		Vector3 val4;
		if (_shopPutBack)
		{
			Vector3 val3 = Flat(pos - val2);
			dest = (ShelfSpot.TryGetValue(shopItem, out var value) ? value : (val2 + ((val3.sqrMagnitude > 0.01f) ? val3.normalized : Vector3.forward) * 6f));
			val4 = Flat(dest - pos);
			arrived = val4.magnitude < 2f;
			if (!flag)
			{
				if (!arrived)
				{
					val4 = Flat(t.centerPoint - val2);
					if (!(val4.magnitude > 6f))
					{
						goto IL_01ae;
					}
				}
				Plugin.Log.LogInfo((object)(_bot.Name + " put " + shopItem.item.itemName + " back"));
				ShopBought.Remove(shopItem);
				ForgetWant(shopItem);
				ReleaseHeld("ShopCarry", 461);
				Blacklist[t] = Time.time + 9999f;
				DropJob("ShopCarry", 463);
				return false;
			}
			goto IL_01ae;
		}
		if (flag)
		{
			Plugin.Log.LogInfo((object)$"{_bot.Name} put {shopItem.item.itemName} (${shopItem.value}k) in the buy zone, zone now ${ZoneCost}k of ${Currency}k");
			ShopBought[shopItem] = this;
			RememberWant(shopItem);
			ReleaseHeld("ShopCarry", 473);
			Blacklist[t] = Time.time + 9999f;
			Say(Line("shopin", null, null, ShopName(shopItem), Money((float)Mathf.Max(0, Currency - ZoneCost - shopItem.value) * 1000f)), 0, "shopin", 12f);
			DropJob("ShopCarry", 476);
			return false;
		}
		if (ZoneCost + shopItem.value > Currency || ShopStopped)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + ": " + shopItem.item.itemName + " doesn't fit the money any more"));
			Say(Line("shopnomoney", null, null, ShopName(shopItem)), 1, "shopnomoney:" + _bot.Name, 15f);
			_shopPutBack = true;
			return true;
		}
		if (!ZoneReady(val))
		{
			val4 = Flat(val2 - pos);
			if (val4.magnitude < 4f)
			{
				val4 = Flat(pos - val2);
				dest = pos + val4.normalized * 2f;
			}
			return true;
		}
		dest = val2;
		val4 = Flat(dest - pos);
		arrived = val4.magnitude < 2.2f && Mathf.Abs(dest.y - pos.y) < 2f;
		return true;
		IL_01ae:
		return true;
	}

	private Vector3 ShopDropPoint()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		ExtractionPoint val = ShopZone();
		if (_shopPutBack || !((Object)(object)val != (Object)null))
		{
			return HoldPoint();
		}
		return ExtractionSpot(val) + Vector3.up * 0.6f;
	}

	internal string ShopMoneyAnswer()
	{
		int currency = Currency;
		int zoneCost = ZoneCost;
		if (zoneCost <= 0)
		{
			return "We've got " + Money((float)currency * 1000f) + " and nothing in the buy zone yet.";
		}
		if (zoneCost > currency)
		{
			return "We've got " + Money((float)currency * 1000f) + " but there's " + Money((float)zoneCost * 1000f) + " in the buy zone, take something out.";
		}
		return "We've got " + Money((float)currency * 1000f) + ", " + Money((float)zoneCost * 1000f) + " is in the buy zone, so " + Money((float)(currency - zoneCost) * 1000f) + " left.";
	}

	private List<string> MakeVoices()
	{
		List<string> list = BotLines.VoicesOf(P);
		if (Plugin.BotSlang.Value && Slang.Level > 0.55f)
		{
			list.Add("slang");
		}
		Plugin.Log.LogInfo((object)$"{_bot.Name} talks with slang {Slang.Level:0.00}, says '{Slang.Pet}' a lot");
		return list;
	}

	private static void ResetTalkStatics()
	{
		TeamSaid.Clear();
		Conversation.Clear();
		_anyTalkAt = 0f;
		WasDead.Clear();
		NeckOf.Clear();
		LastHp.Clear();
		_extractionsSeen = -1;
	}

	internal static void Remember(string who, string text)
	{
		Conversation.Add(who + ": " + text);
		if (Conversation.Count > 12)
		{
			Conversation.RemoveAt(0);
		}
	}

	private static string Casual(string text)
	{
		text = text.Trim().ToLowerInvariant();
		text = Regex.Replace(text, "\\.(\\s|$)", "$1");
		text = text.Replace("!", "").Replace("?", "");
		return Regex.Replace(text, "\\s+", " ").Trim().TrimEnd(',');
	}

	private string Line(string ev, string monster = null, string where = null, string item = null, string money = null, string person = null, string hp = null, string num = null)
	{
		return BotLines.Get(ev, Voices)?.Replace("{monster}", monster ?? "something").Replace("{where}", where ?? "over there").Replace("{item}", item ?? "thing")
			.Replace("{money}", money ?? "a lot")
			.Replace("{person}", person ?? "dude")
			.Replace("{hp}", hp ?? "low")
			.Replace("{n}", num ?? "a few");
	}

	private bool Say(string text, int prio, string topic = null, float topicCooldown = 30f, bool canReply = true, Action after = null)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Invalid comparison between Unknown and I4
		if (!Plugin.BotsTalk.Value || string.IsNullOrEmpty(text) || (prio == 3 && MuteReplies))
		{
			return false;
		}
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null || (Object)(object)GameDirector.instance == (Object)null || (int)GameDirector.instance.currentState != 2)
		{
			return false;
		}
		float time = Time.time;
		if (_job == Job.Freeze || _quiet)
		{
			return false;
		}
		if (prio < 3 && time < _talkAt)
		{
			return false;
		}
		if (prio < 2 && time < _anyTalkAt)
		{
			return false;
		}
		if (topic != null && TeamSaid.TryGetValue(topic, out var value) && time - value < topicCooldown)
		{
			return false;
		}
		if (prio == 0 && Random.value > 0.15f + P.Chatty)
		{
			return false;
		}
		if (prio == 1 && Random.value > 0.45f + 0.7f * P.Chatty)
		{
			return false;
		}
		if (topic != null)
		{
			TeamSaid[topic] = time;
		}
		text = Casual(text);
		if (text.Length == 0)
		{
			return false;
		}
		int num = text.Split(' ').Length;
		float num2 = (avatar.isDisabled ? 0.3f : TypeTime(text));
		_talkAt = time + num2 + 2f + (float)num * 0.35f;
		_anyTalkAt = time + num2 + 0.8f + (float)num * 0.3f;
		bool flag = prio >= 1 && (topic == null || (!topic.StartsWith("flee:") && !topic.StartsWith("ow:") && !topic.StartsWith("knocked:") && !(topic == "noise")));
		if (!avatar.isDisabled && TalkBlocked())
		{
			if (flag && (_deferred == null || prio >= _deferredPrio))
			{
				_deferred = text;
				_deferredPrio = prio;
				_deferredAt = time;
				_deferredReply = canReply;
				_deferredTopic = topic;
				_deferredAfter = after;
			}
			return false;
		}
		if (_typing != null)
		{
			_pending.Add(new Pending
			{
				At = time + 0.4f,
				Text = text,
				Prio = 3,
				CanReply = canReply,
				After = after
			});
			return true;
		}
		StartTyping(text, prio, canReply, topic, after, num2);
		return true;
	}

	private float TypeTime(string text)
	{
		return 0.35f + (float)text.Length / CharsPerSecond * Random.Range(0.85f, 1.2f);
	}

	private bool InDanger()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		if (_job == Job.Flee || _job == Job.Freeze || _job == Job.Guard || Time.time < _escapeUntil)
		{
			return true;
		}
		if (_job == Job.Hide)
		{
			Vector3 val = Flat(_hideSpot - _bot.Position);
			if (val.magnitude > 0.6f)
			{
				return true;
			}
		}
		if (Time.time - _bot.LastHurtTime < 2f)
		{
			return true;
		}
		foreach (Threat item in KnownThreats())
		{
			if (item.Rule != ThreatRule.Harmless || item.Aggressive)
			{
				float num = Vector3.Distance(item.Position, _bot.Position);
				if (num < 5f || (item.Aggressive && ((Object)(object)item.Target == (Object)(object)_bot.Avatar || num < 12f)))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool TalkBlocked()
	{
		if (!InDanger() && !_quiet && _job != Job.Hide && _job != Job.Assist)
		{
			return _slackerRun;
		}
		return true;
	}

	private void StartTyping(string text, int prio, bool canReply, string topic, Action after, float typeTime)
	{
		bool urgent = prio == 2 || (topic != null && (topic.StartsWith("flee:") || topic.StartsWith("ow:") || topic.StartsWith("knocked:")));
		string fix = null;
		_typingPlain = text;
		if (Plugin.BotSlang.Value)
		{
			text = BotSlang.Apply(text, Slang, urgent, out fix);
		}
		_typingFix = fix;
		_typing = text;
		_typingPrio = prio;
		_typingDone = Time.time + typeTime;
		_typingReply = canReply;
		_typingTopic = topic;
		_typingAfter = after;
	}

	private void TypingTick()
	{
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Invalid comparison between Unknown and I4
		float time = Time.time;
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null)
		{
			return;
		}
		if (_typing != null)
		{
			if (!avatar.isDisabled && TalkBlocked())
			{
				Plugin.Log.LogInfo((object)(_bot.Name + " stopped typing to get away"));
				if (_typingPrio >= 1 && (_typingTopic == null || (!_typingTopic.StartsWith("flee:") && !_typingTopic.StartsWith("ow:") && !_typingTopic.StartsWith("knocked:"))))
				{
					_deferred = _typingPlain ?? _typing;
					_deferredPrio = _typingPrio;
					_deferredAt = time;
					_deferredReply = _typingReply;
					_deferredTopic = _typingTopic;
					_deferredAfter = _typingAfter;
				}
				_typing = null;
			}
			else
			{
				if (time < _typingDone)
				{
					return;
				}
				string typing = _typing;
				_typing = null;
				if (!((Object)(object)GameDirector.instance == (Object)null) && (int)GameDirector.instance.currentState == 2)
				{
					avatar.ChatMessageSend(typing);
					Remember(_bot.Name, typing);
					Plugin.Log.LogInfo((object)(_bot.Name + " says: " + typing));
					if (_typingFix != null)
					{
						SayLater(Random.Range(0.5f, 1.3f), _typingFix, 3, canReply: false);
					}
					_typingFix = null;
					if (_typingReply)
					{
						OthersReact(typing, _typingPrio, _typingTopic);
					}
					_typingAfter?.Invoke();
				}
			}
		}
		else if (_deferred != null)
		{
			if (time - _deferredAt > 10f)
			{
				_deferred = null;
			}
			else if (!avatar.isDisabled && !TalkBlocked())
			{
				string deferred = _deferred;
				_deferred = null;
				StartTyping(deferred, _deferredPrio, _deferredReply, _deferredTopic, _deferredAfter, TypeTime(deferred));
			}
		}
	}

	private void CancelTyping(string topic)
	{
		if (_typing != null && _typingTopic == topic)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " deleted \"" + _typing + "\" without sending it"));
			_typing = null;
			_typingFix = null;
		}
		if (_deferred != null && _deferredTopic == topic)
		{
			_deferred = null;
		}
	}

	private void SayLater(float delay, string text, int prio, bool canReply = true, Action after = null)
	{
		_pending.Add(new Pending
		{
			At = Time.time + delay,
			Text = text,
			Prio = prio,
			CanReply = canReply,
			After = after
		});
	}

	private BotBrain NearbyBot(float range)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		List<BotBrain> list = new List<BotBrain>();
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain != this && brain.Alive && Vector3.Distance(brain.Position, _bot.Position) < range)
			{
				list.Add(brain);
			}
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Random.Range(0, list.Count)];
	}

	private void OthersReact(string text, int prio, string topic)
	{
		BotBrain botBrain = NearbyBot(18f);
		if (botBrain == null)
		{
			return;
		}
		if (topic != null && topic.StartsWith("spot:"))
		{
			if (Random.value < 0.3f + 0.3f * botBrain.P.Chatty)
			{
				botBrain.SayLater(Random.Range(1.2f, 2.2f), botBrain.Line("spotreact", null, null, null, null, Name), 1, canReply: false);
			}
		}
		else if (prio == 0 && Random.value < 0.25f + 0.4f * botBrain.P.Chatty)
		{
			string text2 = botBrain.Line("botreply", null, null, null, null, Name);
			if (text2 != null && Random.value < 0.3f)
			{
				text2 = text2 + " " + Name;
			}
			botBrain.SayLater(Random.Range(1.6f, 3f), text2, 0, canReply: false);
		}
	}

	private bool AskTeammate()
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		BotBrain other = NearbyBot(25f);
		if (other == null || other._job == Job.Hide || other._job == Job.Flee || other._job == Job.Freeze)
		{
			return false;
		}
		List<BotChat.Ask> list = new List<BotChat.Ask>
		{
			BotChat.Ask.Doing,
			BotChat.Ask.Loot,
			BotChat.Ask.Monsters,
			BotChat.Ask.Health
		};
		if (Vector3.Distance(other.Position, _bot.Position) > 10f)
		{
			list.Add(BotChat.Ask.WhereAreYou);
		}
		if ((Object)(object)ActiveExtraction() != (Object)null)
		{
			list.Add(BotChat.Ask.Money);
		}
		BotChat.Ask ask = list[Random.Range(0, list.Count)];
		if (!Say(Line(ask switch
		{
			BotChat.Ask.WhereAreYou => "askwhere", 
			BotChat.Ask.Health => "askhealth", 
			BotChat.Ask.Monsters => "askmonsters", 
			BotChat.Ask.Loot => "askloot", 
			BotChat.Ask.Doing => "askdoing", 
			_ => "askmoney", 
		}, null, null, null, null, other.Name), 0, "ask", 25f, canReply: false))
		{
			return false;
		}
		PlayerAvatar avatar = _bot.Avatar;
		BotBrain asker = this;
		other.SayLater(Random.Range(1.8f, 3.2f), other.AnswerText(ask, avatar, null), 3, canReply: false, () =>
		{
			if (Random.value < 0.55f)
			{
				asker.SayLater(Random.Range(1.5f, 2.5f), asker.Line("askthanks", null, null, null, null, other.Name), 3, canReply: false);
			}
		});
		return true;
	}

	private void TalkTick()
	{
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		float time = Time.time;
		for (int num = _pending.Count - 1; num >= 0; num--)
		{
			if (!(time < _pending[num].At))
			{
				Pending pending = _pending[num];
				_pending.RemoveAt(num);
				Say(pending.Text, pending.Prio, null, 30f, pending.CanReply, pending.After);
			}
		}
		TypingTick();
		TeamWatch();
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null)
		{
			return;
		}
		if (avatar.isDisabled)
		{
			if (_diedAt < 0f)
			{
				_diedAt = time;
			}
			if (!_deadLineSaid && time - _diedAt > 3f && (Object)(object)avatar.playerDeathHead != (Object)null && (Object)(object)avatar.playerDeathHead.physGrabObject != (Object)null)
			{
				_deadLineSaid = true;
				Say(Line("died", null, Where(avatar.playerDeathHead.physGrabObject.centerPoint, forListener: false)), 2);
			}
			return;
		}
		_diedAt = -1f;
		_deadLineSaid = false;
		WatchHeldItem();
		if (!SemiFunc.RunIsLevel() && !SemiFunc.RunIsShop() && !SemiFunc.RunIsLobby())
		{
			return;
		}
		if (!_greeted)
		{
			if (_greetAt < 0f)
			{
				_greetAt = time + Random.Range(4f, 14f);
			}
			if (time >= _greetAt)
			{
				_greeted = true;
				string text = (SemiFunc.RunIsShop() ? Line("shop") : (((Random.value < 0.4f) ? HistoryGreet() : null) ?? Line("greet")));
				Say(text, 0, "greet", 5f);
			}
		}
		if (time >= _banterAt)
		{
			_banterAt = time + Random.Range(35f, 80f) * (1.4f - P.Chatty);
			if ((_job == Job.Follow || _job == Job.Explore || _job == Job.Order || _job == Job.Truck || _job == Job.HangOut) && (Time.timeSinceLevelLoad > 45f || _job == Job.HangOut) && (!SemiFunc.RunIsLevel() || !(Random.value < 0.4f) || !AskTeammate()))
			{
				Say(Banter(), 0, "banter", 25f);
			}
		}
	}

	private string Banter()
	{
		if (SemiFunc.RunIsLobby())
		{
			return Line("trucktalk", null, null, null, null, null, null, Words(Mathf.Max(1, Hist("levels"))));
		}
		if (SemiFunc.RunIsShop())
		{
			return Line("shoptalk");
		}
		if (Random.value < 0.3f)
		{
			List<string> list = new List<string>();
			RoundDirector instance = RoundDirector.instance;
			PhysGrabCart val = NearestKnownCart();
			if ((Object)(object)val != (Object)null && val.haulCurrent > 0)
			{
				list.Add("carts got " + Money(val.haulCurrent) + " in it");
			}
			if ((Object)(object)instance != (Object)null && instance.extractionHaulGoal > 0 && (Object)(object)ActiveExtraction() != (Object)null)
			{
				list.Add("we need " + Money(Mathf.Max(0, instance.extractionHaulGoal - instance.currentHaul)) + " more for this extraction");
			}
			int num = 0;
			foreach (ValuableObject item in Seen)
			{
				if ((Object)(object)item != (Object)null && (Object)(object)item.physGrabObject != (Object)null && !item.physGrabObject.dead && !InSomeCart(item.physGrabObject))
				{
					num++;
				}
			}
			if (num > 3)
			{
				list.Add("i know where " + Words(num) + " more things are");
			}
			if (list.Count > 0)
			{
				return list[Random.Range(0, list.Count)];
			}
		}
		return Line("banter");
	}

	private void WatchHeldItem()
	{
		PhysGrabObject val = ((_bot.HeldView != 0) ? BotGrab.Held(_bot) : null);
		if (_heldTalk != null && ((Object)(object)_heldTalk == (Object)null || _heldTalk.dead) && (Object)(object)val == (Object)null)
		{
			Say(Line("itembroke", null, null, _heldName ?? "thing"), 1, "broke:" + _bot.Name, 8f);
			_heldTalk = null;
			return;
		}
		ValuableObject val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<ValuableObject>() : null);
		if ((Object)(object)val2 == (Object)null)
		{
			_heldTalk = null;
			return;
		}
		if ((Object)(object)val == (Object)(object)_heldTalk && val2.dollarValueCurrent < _heldValue - Mathf.Max(30f, _heldValue * 0.05f))
		{
			Say(Line("itemdamaged", null, null, ItemName(val), Money(_heldValue - val2.dollarValueCurrent)), 1, "broke:" + _bot.Name, 8f);
		}
		_heldName = ItemName(val);
		_heldTalk = val;
		_heldValue = val2.dollarValueCurrent;
	}

	private static void TeamWatch()
	{
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c1: Unknown result type (might be due to invalid IL or missing references)
		if (_watchFrame == Time.frameCount || (Object)(object)GameDirector.instance == (Object)null)
		{
			return;
		}
		_watchFrame = Time.frameCount;
		RoundDirector instance = RoundDirector.instance;
		if ((Object)(object)instance != (Object)null && SemiFunc.RunIsLevel())
		{
			if (_extractionsSeen < 0)
			{
				_extractionsSeen = instance.extractionPointsCompleted;
			}
			if (instance.extractionPointsCompleted > _extractionsSeen)
			{
				_extractionsSeen = instance.extractionPointsCompleted;
				BotBrain botBrain = AnyAliveBot();
				botBrain?.SayLater(1f, botBrain.Line("extractdone"), 1);
			}
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player == (Object)null)
			{
				continue;
			}
			bool isDisabled = player.isDisabled;
			if (WasDead.TryGetValue(player, out var value) && value != isDisabled)
			{
				Vector3 val = BotRegistry.Get(player)?.Position ?? ((Component)player).transform.position;
				BotBrain botBrain2 = Witness(player, val, 22f);
				if (botBrain2 != null)
				{
					string playerName = player.playerName;
					if (isDisabled && Vector3.Distance(botBrain2.Position, val) < 18f)
					{
						float delay = Random.Range(0.6f, 1.5f);
						string person = playerName;
						botBrain2.SayLater(delay, botBrain2.Line("persondiedwhere", null, botBrain2.Where(val, forListener: false), null, null, person), 2);
					}
					else if (isDisabled)
					{
						botBrain2.SayLater(Random.Range(0.6f, 1.5f), botBrain2.Line("persondied", null, null, null, null, playerName), 2);
					}
					else
					{
						botBrain2.SayLater(Random.Range(1f, 2f), botBrain2.Line("personrevived", null, null, null, null, playerName), 1);
					}
				}
			}
			WasDead[player] = isDisabled;
			if (isDisabled || BotRegistry.IsBot(player) || !((Object)(object)player.playerHealth != (Object)null))
			{
				continue;
			}
			int health = player.playerHealth.health;
			if (LastHp.TryGetValue(player, out var value2) && health <= value2 - 10 && !IsHealingSomeone(player))
			{
				BotBrain botBrain3 = Witness(player, ((Component)player).transform.position, 12f);
				botBrain3?.Say(botBrain3.Line("humanhurt", null, null, null, null, player.playerName), 0, "humanhurt:" + player.playerName);
				foreach (BotBrain brain in BotManager.Brains)
				{
					if (brain != null && brain.Alive && (brain.SeesPerson(player) || Vector3.Distance(brain.Position, ((Component)player).transform.position) < 8f))
					{
						brain.LearnHurt(player);
					}
				}
			}
			LastHp[player] = health;
		}
	}

	private static BotBrain AnyAliveBot()
	{
		List<BotBrain> list = new List<BotBrain>();
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain.Alive)
			{
				list.Add(brain);
			}
		}
		if (list.Count <= 0)
		{
			return null;
		}
		return list[Random.Range(0, list.Count)];
	}

	private static BotBrain Witness(PlayerAvatar p, Vector3 at, float range)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		BotBrain result = null;
		float num = range;
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain.Alive && !((Object)(object)brain._bot.Avatar == (Object)(object)p))
			{
				float num2 = Vector3.Distance(brain.Position, at);
				if (brain.SeesPerson(p))
				{
					num2 *= 0.5f;
				}
				if (num2 < num)
				{
					num = num2;
					result = brain;
				}
			}
		}
		return result;
	}

	private void OnSpotted(Threat t)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		float num = Vector3.Distance(t.Position, _bot.Position);
		string monster = MonsterName(t.Kind).ToLowerInvariant();
		string text = Where(t.Position, forListener: false);
		string topic = "spot:" + (((Object)(object)t.Parent != (Object)null) ? ((Object)t.Parent).GetInstanceID().ToString() : t.Kind);
		switch (t.Rule)
		{
		case ThreatRule.Freeze:
			if (num > 26f)
			{
				Say(Line("spothuntsman", null, text), 2, topic, 60f);
			}
			break;
		case ThreatRule.DontLook:
			Say((t.Kind == "EnemyCeilingEye") ? Line("spotpeeper", null, text) : Line("spotdontlook", monster, text), 2, topic, 60f);
			break;
		case ThreatRule.Harmless:
			Say(Line("spotharmless", monster, text), 1, topic, 90f);
			break;
		case ThreatRule.Explosive:
			Say(Line("spotbanger", null, text), 2, topic, 45f);
			break;
		default:
		{
			int prio = ((!t.Aggressive && !(num < 12f)) ? 1 : 2);
			Say((t.Kind == "EnemyRobe") ? Line("spotrobe", null, text) : Line("spot", monster, text), prio, topic, 45f);
			break;
		}
		}
	}

	private void OnFleeStart(Threat t)
	{
		if (!((Object)(object)t.Target != (Object)(object)_bot.Avatar))
		{
			Say(Line("flee", MonsterName(t.Kind).ToLowerInvariant()), 2, "flee:" + _bot.Name, 12f);
		}
	}

	public void OnHurt(int damage)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar == (Object)null || (Object)(object)avatar.playerHealth == (Object)null || avatar.isDisabled)
		{
			return;
		}
		int health = avatar.playerHealth.health;
		if (health > 0)
		{
			if (health < 35)
			{
				Say(Line("hurtbad", null, null, null, null, null, Words(health)), 1, "hurt:" + _bot.Name, 25f);
				TeamLearnsHurt(_bot.Avatar, _bot.Position, 25f);
			}
			if (health < 70)
			{
				TeamLearnsHurt(_bot.Avatar, _bot.Position, 8f);
			}
			else if (damage >= 10)
			{
				Say(Line("hurtsmall"), 0, "ow:" + _bot.Name, 6f);
			}
		}
	}

	private void OnRevived()
	{
		SayLater(1.5f, Line("revived"), 1);
	}

	private void OnKnockedOver()
	{
		Say(Line("knocked"), 0, "knocked:" + _bot.Name, 15f);
	}

	private void OnCameOutOfHiding()
	{
		SayLater(0.8f, Line("unhide"), 0);
	}

	private void OnHeardNoise()
	{
		Say(Line("heardnoise"), 0, "noise", 40f);
	}

	private string ItemName(PhysGrabObject o)
	{
		if ((Object)(object)o == (Object)null)
		{
			return "thing";
		}
		string input = ((Object)o).name.Replace("(Clone)", "");
		input = Regex.Replace(input, "^(Valuable|Item)\\s*", "", RegexOptions.IgnoreCase);
		input = Regex.Replace(input, "[_\\-]+", " ");
		input = Regex.Replace(input, "\\s*\\d+$", "");
		input = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
		input = input.Trim().ToLowerInvariant();
		if (input.Length != 0)
		{
			return input;
		}
		return "thing";
	}

	private void OnHaulStart(PhysGrabObject o)
	{
		ValuableObject val = (((Object)(object)o != (Object)null) ? ((Component)o).GetComponent<ValuableObject>() : null);
		if (!((Object)(object)val == (Object)null))
		{
			float dollarValueCurrent = val.dollarValueCurrent;
			float num = Mathf.Lerp(4000f, 1200f, P.Greed);
			if (dollarValueCurrent >= num)
			{
				Say(Line("haulbrag", null, null, ItemName(o), Money(dollarValueCurrent)), 1, "haul:" + ((Object)o).GetInstanceID(), 999f);
			}
		}
	}

	private void OnNeedHelp(PhysGrabObject o)
	{
		Say(Line("needhelp", null, null, ItemName(o)), 2, "help:" + ((Object)o).GetInstanceID(), 20f);
		CallForHelp(o);
	}

	internal static string RoomName(Vector3 p)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		int num = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.5f, 0.25f, RoomHits, RoomMask, (QueryTriggerInteraction)2);
		for (int i = 0; i < num; i++)
		{
			RoomVolume val = (((Object)(object)RoomHits[i] != (Object)null) ? ((Component)RoomHits[i]).GetComponentInParent<RoomVolume>() : null);
			if (!((Object)(object)val == (Object)null))
			{
				if (val.Truck)
				{
					return "the truck";
				}
				if (val.Extraction)
				{
					return "the extraction room";
				}
				string key = ((Object)val).name + "|" + (((Object)(object)val.Module != (Object)null) ? ((Object)val.Module).name : "");
				if (!RoomNames.TryGetValue(key, out var value))
				{
					value = (((Object)(object)val.Module != (Object)null && ((Object)val.Module).name.StartsWith("Start Room")) ? "start room" : (CleanRoom(((Object)val).name) ?? (((Object)(object)val.Module != (Object)null) ? CleanRoom(((Object)val.Module).name) : null)));
					RoomNames[key] = value;
					Plugin.Log.LogInfo((object)("Room '" + ((Object)val).name + "' (module '" + (((Object)(object)val.Module != (Object)null) ? ((Object)val.Module).name : "-") + "') is called: " + (value ?? "(no name)")));
				}
				if (value != null)
				{
					return "the " + value;
				}
			}
		}
		return null;
	}

	private static string CleanRoom(string raw)
	{
		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}
		string text = raw.Replace("(Clone)", "");
		int num = text.LastIndexOf(" - ");
		if (num >= 0)
		{
			text = text.Substring(num + 3);
		}
		text = Regex.Replace(text, "\\(.*?\\)", "");
		text = Regex.Replace(text, "[_\\-]+", " ");
		text = Regex.Replace(text, "\\d+", "");
		text = Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
		text = text.Trim().ToLowerInvariant();
		if (text.Length < 3 || text.Contains("volume") || text.StartsWith("module") || Generic.Contains(text))
		{
			return null;
		}
		return text;
	}

	private string Where(Vector3 p, bool forListener, PlayerAvatar listener = null)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = _bot.Position;
		float num = _bot.Yaw;
		string text = "me";
		string text2 = "my";
		if (forListener && (Object)(object)listener != (Object)null && !listener.isDisabled)
		{
			position = ((Component)listener).transform.position;
			num = (((Object)(object)listener.localCamera != (Object)null) ? ((Component)listener.localCamera).transform.eulerAngles.y : ((Component)listener).transform.eulerAngles.y);
			text = "you";
			text2 = "your";
		}
		Vector3 val = Flat(p - position);
		float magnitude = val.magnitude;
		float num2 = p.y - position.y;
		string text3;
		if (num2 > 2.5f)
		{
			text3 = "upstairs";
		}
		else
		{
			text3 = ((num2 < -2.5f) ? "downstairs" : null);
		}
		string text4 = RoomName(p);
		string text5 = RoomName(position);
		if (magnitude < 3.5f && text3 == null)
		{
			return "right next to " + text;
		}
		float num3 = Vector3.SignedAngle(Quaternion.Euler(0f, num, 0f) * Vector3.forward, val, Vector3.up);
		string text6;
		if (Mathf.Abs(num3) < 40f)
		{
			text6 = "ahead of " + text;
		}
		else if (Mathf.Abs(num3) > 140f)
		{
			text6 = "behind " + text;
		}
		else
		{
			text6 = ((num3 > 0f) ? ("to " + text2 + " right") : ("to " + text2 + " left"));
		}
		string text7 = ((magnitude > 30f) ? ", way over there" : "");
		if (text4 != null && text4 != text5)
		{
			return ((text3 != null) ? (text3 + " in ") : "in ") + text4 + ((text4 == "the truck") ? "" : text7);
		}
		if (text3 != null)
		{
			return text3 + ", " + text6;
		}
		return text6 + text7;
	}

	internal static string MonsterName(string kind)
	{
		switch (kind)
		{
		case "EnemyHead":
			return "Headman";
		case "EnemyRunner":
			return "Reaper";
		case "EnemyThinMan":
			return "Shadow Child";
		case "EnemyCeilingEye":
			return "Peeper";
		case "EnemyHunter":
			return "Huntsman";
		case "EnemyRobe":
			return "Robe";
		case "EnemyDuck":
			return "Apex Predator";
		case "EnemyBang":
			return "Banger";
		case "EnemySlowWalker":
			return "Trudge";
		case "EnemyAnimal":
			return "Animal";
		case "EnemyBeamer":
			return "Clown";
		case "EnemyBowtie":
			return "Bowtie";
		case "EnemyFloater":
			return "Mentalist";
		case "EnemyGnome":
			return "Gnome";
		case "EnemyHidden":
			return "Hidden";
		case "EnemySlowMouth":
			return "Spewer";
		case "EnemyTumbler":
			return "Chef";
		case "EnemyUpscream":
			return "Upscream";
		case "EnemyValuableThrower":
			return "Rugrat";
		case "EnemyBirthdayBoy":
			return "Birthday Boy";
		case "EnemyElsa":
			return "Elsa";
		case "EnemyTick":
			return "Tick";
		case "EnemySpinny":
			return "roulette monster";
		case "EnemyHeartHugger":
			return "Heart Hugger";
		case "EnemyOogly":
			return "Oogly";
		case "EnemyBombThrower":
			return "bomb thrower";
		case "EnemyBombThrowerHead":
			return "bomb";
		case "EnemyTricycle":
			return "tricycle kid";
		case "EnemyShadow":
			return "shadow monster";
		default:
			if (kind.StartsWith("EnemyHead"))
			{
				return "Headman";
			}
			break;
		case null:
			break;
		}
		if (kind != null && kind.StartsWith("Enemy") && kind.Length > 5)
		{
			return Regex.Replace(kind.Substring(5), "([a-z])([A-Z])", "$1 $2");
		}
		return "monster";
	}

	private int Hist(string key)
	{
		return BotSave.Get(_bot.SteamId, key);
	}

	internal string HistoryText()
	{
		int num = Hist("levels");
		int num2 = Hist("deaths");
		int num3 = Hist("money");
		int num4 = Hist("rescues");
		int num5 = Hist("healed");
		int num6 = Hist("opened");
		if (num <= 1)
		{
			return "this is my first level on this save";
		}
		List<string> list = new List<string> { "been on " + Words(num) + " levels" };
		list.Add(num2 switch
		{
			1 => "died once", 
			0 => "never died", 
			_ => "died " + Words(num2) + " times", 
		});
		if (num3 > 0)
		{
			list.Add("hauled " + Money(num3));
		}
		if (num4 > 0)
		{
			list.Add((num4 == 1) ? "brought one head back" : ("brought " + Words(num4) + " heads back"));
		}
		if (num5 > 0)
		{
			list.Add("gave away " + Words(num5) + " health");
		}
		if (num6 > 0)
		{
			list.Add("opened " + Words(num6) + " cabinets and chests");
		}
		return string.Join(", ", list);
	}

	private string HistoryGreet()
	{
		int num = Hist("levels");
		int num2 = Hist("deaths");
		if (num < 3)
		{
			return null;
		}
		float value = Random.value;
		if (num2 == 0 && value < 0.4f)
		{
			return Line("greetclean", null, null, null, null, null, null, Words(num));
		}
		if (num2 >= 3 && value < 0.5f)
		{
			return Line("greetdeaths", null, null, null, null, null, null, Words(num2));
		}
		return Line("greetsave", null, null, null, null, null, null, Words(num));
	}

	internal static string Words(int n)
	{
		if (n < 0)
		{
			return "minus " + Words(-n);
		}
		string[] array = new string[20]
		{
			"zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
			"ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
		};
		string[] array2 = new string[10] { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
		if (n < 20)
		{
			return array[n];
		}
		if (n < 100)
		{
			return array2[n / 10] + ((n % 10 != 0) ? (" " + array[n % 10]) : "");
		}
		if (n < 1000)
		{
			return array[n / 100] + " hundred" + ((n % 100 != 0) ? (" " + Words(n % 100)) : "");
		}
		if (n < 1000000)
		{
			return Words(n / 1000) + " thousand" + ((n % 1000 != 0) ? (" " + Words(n % 1000)) : "");
		}
		return Words(n / 1000000) + " million";
	}

	internal static string Money(float dollars)
	{
		int num = Mathf.RoundToInt(dollars);
		if (num >= 1000)
		{
			return Words(Mathf.RoundToInt((float)num / 1000f)) + " grand";
		}
		if (num >= 100)
		{
			return Words(Mathf.RoundToInt((float)num / 50f) * 50) + " bucks";
		}
		return Words(num) + " bucks";
	}

	private static string Pick(params string[] lines)
	{
		return lines[Random.Range(0, lines.Length)];
	}

	internal string DoingNow()
	{
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		PlayerAvatar avatar = _bot.Avatar;
		if ((Object)(object)avatar != (Object)null && avatar.isDisabled)
		{
			return "I'm dead, waiting for someone to bring my head back";
		}
		switch (_job)
		{
		case Job.Freeze:
			return "Holding still, Huntsman's close";
		case Job.Flee:
			if (_danger == null)
			{
				return "Running";
			}
			return "Running from a " + MonsterName(_danger.Kind);
		case Job.Hide:
			if (_danger == null)
			{
				return "Hiding";
			}
			return "Hiding from a " + MonsterName(_danger.Kind);
		case Job.Revive:
			if (!((Object)(object)_reviveOf != (Object)null))
			{
				return "Bringing a head back";
			}
			return "Bringing " + _reviveOf.playerName + "'s head back";
		case Job.Haul:
		{
			if ((Object)(object)_shopItem != (Object)null)
			{
				if (!_shopPutBack)
				{
					return "Taking this " + ShopName(_shopItem) + " to the buy zone";
				}
				return "Putting this " + ShopName(_shopItem) + " back";
			}
			if (!((Object)(object)_target != (Object)null))
			{
				return "Carrying stuff";
			}
			string text2 = ItemName(_target);
			string text3;
			if ((Object)(object)_bringTo != (Object)null)
			{
				text3 = _bringTo.playerName;
			}
			else
			{
				text3 = (((Object)(object)_cart != (Object)null) ? "the cart" : "extraction");
			}
			return "Taking this " + text2 + " to " + text3;
		}
		case Job.Cart:
			if (_cartGoal != CartGoal.Extraction)
			{
				if (_cartGoal != CartGoal.OutOfExtraction)
				{
					return "Moving the cart over to the loot";
				}
				return "Getting the cart back out of extraction";
			}
			return "Taking the cart to extraction";
		case Job.Activate:
			return "Turning on an extraction point";
		case Job.Heal:
			if (!((Object)(object)_healing != (Object)null))
			{
				return "Healing someone";
			}
			return "Healing " + _healing.playerName;
		case Job.Open:
			if (_openBox == null)
			{
				return "Looking in the furniture for loot";
			}
			return "Checking a " + _openBox.Kind + " for loot";
		case Job.Explore:
		{
			string text = (((Object)(object)_explore != (Object)null) ? RoomName(((Component)_explore).transform.position) : null);
			if (text == null)
			{
				return "Looking around for loot";
			}
			return "Checking out " + text;
		}
		case Job.Truck:
			return "Heading to the truck";
		case Job.HangOut:
			if (!SemiFunc.RunIsShop())
			{
				return "Just hanging out in the truck";
			}
			return "Just hanging out in the shop";
		case Job.Gnome:
			return "Smashing a gnome";
		case Job.Use:
			if (!((Object)(object)_useItem != (Object)null))
			{
				return "Using something I bought";
			}
			return "Using my " + ShopName(_useItem);
		case Job.Assist:
			return "Helping carry something heavy";
		case Job.Escort:
			return "Sticking with the cart, filling it up";
		case Job.Guard:
			return "Watching the cart, there's a monster near it";
		case Job.Order:
			return OrderDoing();
		default:
			if (!((Object)(object)_leader != (Object)null))
			{
				return "Looking for you guys";
			}
			return "Tagging along with " + _leader.playerName;
		}
	}

	public void StartScreen()
	{
		if (_job != Job.Truck)
		{
			DropJob("StartScreen", 26);
			_job = Job.Truck;
			_jobStarted = Time.time;
		}
		_screenMode = true;
		_screenStarted = Time.time;
		_corners.Clear();
	}

	private static StaticGrabObject Screen()
	{
		if (!((Object)(object)TruckScreenText.instance != (Object)null))
		{
			return null;
		}
		return ((Component)TruckScreenText.instance).GetComponent<StaticGrabObject>();
	}

	private static Vector3 ScreenPoint(StaticGrabObject sgo)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabObjectGrabArea val = ((Component)sgo).GetComponent<PhysGrabObjectGrabArea>() ?? ((Component)sgo).GetComponentInChildren<PhysGrabObjectGrabArea>();
		Bounds bounds;
		if ((Object)(object)val != (Object)null)
		{
			foreach (PhysGrabObjectGrabArea.GrabArea grabArea in val.grabAreas)
			{
				foreach (Collider grabAreaCollider in grabArea.grabAreaColliders)
				{
					if ((Object)(object)grabAreaCollider != (Object)null)
					{
						bounds = grabAreaCollider.bounds;
						return bounds.center;
					}
				}
			}
		}
		if ((Object)(object)sgo.colliderTransform != (Object)null)
		{
			Collider component = ((Component)sgo.colliderTransform).GetComponent<Collider>();
			if (!((Object)(object)component != (Object)null))
			{
				return sgo.colliderTransform.position;
			}
			bounds = component.bounds;
			return bounds.center;
		}
		return ((Component)sgo).transform.position;
	}

	public void StopScreen()
	{
		if (_bot.HoldingStatic)
		{
			BotGrab.ReleaseStatic(_bot, Screen());
		}
		_screenMode = false;
	}

	private Vector3 DoScreen(out Vector3? lookAt)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0220: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		StaticGrabObject val = Screen();
		TruckScreenText instance = TruckScreenText.instance;
		if ((Object)(object)val == (Object)null || (Object)(object)instance == (Object)null)
		{
			StopScreen();
			return Vector3.zero;
		}
		Vector3 val2 = ScreenPoint(val);
		lookAt = val2;
		if ((int)instance.playerChatBoxState != 0 || Time.time - _screenStarted > 30f)
		{
			StopScreen();
			return Vector3.zero;
		}
		foreach (PhysGrabber item in val.playerGrabbing)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.playerAvatar != (Object)(object)_bot.Avatar && !_bot.HoldingStatic)
			{
				StopScreen();
				return Vector3.zero;
			}
		}
		if (_bot.HoldingStatic)
		{
			_bot.Puller = val2;
			if (!instance.chatActive && Time.time - _screenGrabbedAt > 0.6f && Time.time >= _screenNudgeAt)
			{
				_screenNudgeAt = Time.time + 1f;
				instance.StartChat();
			}
			return Vector3.zero;
		}
		Vector3 val3 = Eye();
		Vector3 val4 = val2 - val3;
		if (val4.magnitude < 3.5f && BotGrab.GrabLine(_bot, val3, val2, ((Component)instance).transform))
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " grabbed the truck screen"));
			BotGrab.GrabStatic(_bot, val, val2);
			_screenGrabbedAt = Time.time;
			return Vector3.zero;
		}
		Vector3 val5 = val2;
		NavMeshHit val6 = default;
		if (NavMesh.SamplePosition(val2, out val6, 2.5f, -1))
		{
			val5 = val6.position;
		}
		val4 = Flat(val5 - _bot.Position);
		if (val4.magnitude < 0.4f)
		{
			val4 = Flat(val2 - _bot.Position);
			return val4.normalized * 0.8f;
		}
		return Steer(_bot.Position, val5) * 3.2f;
	}

	private static void ResetTeamStatics()
	{
		Helpers.Clear();
	}

	private void ReleaseHeld([CallerMemberName] string who = "", [CallerLineNumber] int line = 0)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabObject val = BotGrab.Held(_bot);
		if ((Object)(object)val != (Object)null && !val.dead)
		{
			_thingPos[val] = val.centerPoint;
		}
		if (!((Object)(object)_setDown != (Object)null) || !((Object)(object)_setDown == (Object)(object)val))
		{
			if (Plugin.GentleSetDown.Value && WantsGentle(val))
			{
				BeginSetDown(val, $"{who}:{line}");
				return;
			}
			BotCareLog.Released(_bot, val, $"{who}:{line}");
			BotGrab.Release(_bot);
		}
	}

	private static int HelperCount(PhysGrabObject o)
	{
		if (!Helpers.TryGetValue(o, out var value))
		{
			return 0;
		}
		return value.Count;
	}

	private static int HelpersNeeded(PhysGrabObject o, float limit)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Invalid comparison between Unknown and I4
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Invalid comparison between Unknown and I4
		if ((Object)(object)o == (Object)null || (Object)(object)o.rb == (Object)null)
		{
			return 0;
		}
		int num = ((o.rb.mass > limit) ? Mathf.Clamp(Mathf.CeilToInt(o.rb.mass / limit) - 1, 1, 2) : 0);
		int num2 = 0;
		ValuableObject component = ((Component)o).GetComponent<ValuableObject>();
		if ((Object)(object)component != (Object)null)
		{
			if ((int)component.volumeType == 6)
			{
				num2 = 2;
			}
			else if ((int)component.volumeType >= 3)
			{
				num2 = 1;
			}
		}
		return Mathf.Max(num, num2);
	}

	public static bool IsHeavy(PhysGrabObject o, float limit)
	{
		return HelpersNeeded(o, limit) > 0;
	}

	public static bool TruckTime()
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Invalid comparison between Unknown and I4
		if ((Object)(object)TruckSafetySpawnPoint.instance == (Object)null)
		{
			return false;
		}
		if (SemiFunc.RunIsShop())
		{
			if ((Object)(object)ShopManager.instance == (Object)null || (Object)(object)LevelGenerator.Instance == (Object)null || !LevelGenerator.Instance.Generated)
			{
				return false;
			}
			ShopTeam();
			if (!ShopTimeUp())
			{
				if ((Object)(object)TruckScreenText.instance != (Object)null)
				{
					return (int)TruckScreenText.instance.playerChatBoxState > 0;
				}
				return false;
			}
			return true;
		}
		if (SemiFunc.RunIsLevel() && (Object)(object)RoundDirector.instance != (Object)null)
		{
			return RoundDirector.instance.allExtractionPointsCompleted;
		}
		return false;
	}

	public void ForceTruck()
	{
		_slackerRun = true;
		DropJob("ForceTruck", 190);
		_job = Job.Truck;
		_jobStarted = Time.time;
		_corners.Clear();
	}

	private bool FinalExtractionFinishing()
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		if (_finalSeen)
		{
			return true;
		}
		RoundDirector instance = RoundDirector.instance;
		if (!SemiFunc.RunIsLevel() || (Object)(object)instance == (Object)null || (Object)(object)instance.extractionPointCurrent == (Object)null)
		{
			return false;
		}
		if (instance.extractionPointsCompleted < instance.extractionPoints - 1)
		{
			return false;
		}
		ExtractionPoint extractionPointCurrent = instance.extractionPointCurrent;
		if (!extractionPointCurrent.StateIs((ExtractionPoint.State)3) && !extractionPointCurrent.StateIs((ExtractionPoint.State)6))
		{
			return false;
		}
		Vector3 val = ExtractionSpot(extractionPointCurrent);
		if (Vector3.Distance(val, _bot.Position) > 20f && !CanSee(val + Vector3.up, 30f, ((Component)extractionPointCurrent).transform))
		{
			return false;
		}
		Plugin.Log.LogInfo((object)(_bot.Name + " sees the last extraction finishing"));
		_finalSeen = true;
		return true;
	}

	private bool TryStartTruck()
	{
		if (!TruckTime() && !_slackerRun && !FinalExtractionFinishing())
		{
			return false;
		}
		if (_job != Job.Truck)
		{
			Plugin.Log.LogInfo((object)(_bot.Name + " is heading to the truck"));
			if (SemiFunc.RunIsLevel())
			{
				Say(_slackerRun ? Line("slacker") : Line("truck"), (!_slackerRun) ? 1 : 2, _slackerRun ? null : "truck", 120f);
			}
			DropJob("TryStartTruck", 229);
			_job = Job.Truck;
			_jobStarted = Time.time;
			_corners.Clear();
		}
		return true;
	}

	private Vector3 DoTruck(out bool sprint, out Vector3? lookAt)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		sprint = _slackerRun;
		lookAt = null;
		if (_screenMode)
		{
			return DoScreen(out lookAt);
		}
		Vector3 position = ((Component)TruckSafetySpawnPoint.instance).transform.position;
		bool num = (Object)(object)_bot.Avatar.RoomVolumeCheck != (Object)null && _bot.Avatar.RoomVolumeCheck.inTruck;
		Vector3 val = Flat(position - _bot.Position);
		float magnitude = val.magnitude;
		if ((num && magnitude < 2.5f) || (SemiFunc.RunIsShop() && magnitude < 4f))
		{
			lookAt = HangLook();
			return Vector3.zero;
		}
		if (magnitude > 15f)
		{
			sprint = true;
		}
		return Steer(_bot.Position, position) * (sprint ? 5.6f : 3.2f);
	}

	private bool TryHelpPerson()
	{
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		if (_bot.HeldView != 0 || _job == Job.Assist || (Object)(object)ValuableDirector.instance == (Object)null)
		{
			return false;
		}
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			PhysGrabObject val = (((Object)(object)valuable != (Object)null) ? valuable.physGrabObject : null);
			if ((Object)(object)val == (Object)null || val.dead || !IsHeavy(val, HeavyLimit) || HelperCount(val) >= HelpersNeeded(val, HeavyLimit))
			{
				continue;
			}
			bool flag = false;
			foreach (PhysGrabber item in val.playerGrabbing)
			{
				if ((Object)(object)item != (Object)null && !BotRegistry.IsBot(item.playerAvatar))
				{
					flag = true;
				}
			}
			if (flag && Vector3.Distance(val.centerPoint, _bot.Position) < Mathf.Lerp(8f, 25f, P.Helpful) && (CanSee(val.centerPoint, 22f, ((Component)val).transform) || Vector3.Distance(val.centerPoint, _bot.Position) < 6f))
			{
				return TryStartAssist();
			}
		}
		return false;
	}

	private bool TryStartAssist()
	{
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		if (!Plugin.BotsHaul.Value || (Object)(object)ValuableDirector.instance == (Object)null)
		{
			return false;
		}
		PhysGrabObject val = null;
		float num = float.MaxValue;
		foreach (ValuableObject valuable in ValuableDirector.instance.valuableList)
		{
			if ((Object)(object)valuable == (Object)null || (Object)(object)valuable.physGrabObject == (Object)null)
			{
				continue;
			}
			PhysGrabObject physGrabObject = valuable.physGrabObject;
			if (physGrabObject.dead || (Object)(object)physGrabObject.rb == (Object)null || !IsHeavy(physGrabObject, HeavyLimit) || physGrabObject.playerGrabbing.Count == 0 || HelperCount(physGrabObject) >= HelpersNeeded(physGrabObject, HeavyLimit))
			{
				continue;
			}
			bool flag = false;
			foreach (PhysGrabber item in physGrabObject.playerGrabbing)
			{
				if ((Object)(object)item != (Object)null && (Object)(object)item.playerAvatar != (Object)(object)_bot.Avatar)
				{
					flag = true;
				}
			}
			if (flag && (CanSee(physGrabObject.centerPoint, 22f, ((Component)physGrabObject).transform) || !(Vector3.Distance(physGrabObject.centerPoint, _bot.Position) > 6f)))
			{
				float num2 = Vector3.Distance(physGrabObject.centerPoint, _bot.Position);
				if (num2 < 25f && num2 < num && SafeSpot(physGrabObject.centerPoint))
				{
					num = num2;
					val = physGrabObject;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		StartAssist(val);
		return true;
	}

	private void StartAssist(PhysGrabObject o)
	{
		DropJob("StartAssist", 322);
		_job = Job.Assist;
		_assist = o;
		_carry = Carry.Approach;
		_jobStarted = Time.time;
		if (!Helpers.TryGetValue(o, out var value))
		{
			value = (Helpers[o] = new List<BotBrain>());
		}
		value.Add(this);
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " is helping carry " + ((Object)o).name));
		Say(Line("assist"), 0, "assist:" + ((Object)o).GetInstanceID());
	}

	private bool CallForHelp(PhysGrabObject o)
	{
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)o == (Object)null || o.dead || HelperCount(o) >= HelpersNeeded(o, HeavyLimit))
		{
			return false;
		}
		BotBrain botBrain = null;
		float num = 35f;
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && brain != this && brain.Alive && brain._bot.HeldView == 0 && !brain._bot.HoldingStatic && brain._order == OrderKind.None && (brain._job == Job.Follow || brain._job == Job.Explore || (brain._job == Job.Open && brain._openStep == OpenStep.Approach)))
			{
				float num2 = Vector3.Distance(brain.Position, o.centerPoint);
				if (num2 < num && brain.SafeSpot(o.centerPoint))
				{
					num = num2;
					botBrain = brain;
				}
			}
		}
		if (botBrain == null)
		{
			return false;
		}
		Plugin.Log.LogInfo((object)(botBrain.Name + " heard " + _bot.Name + " ask for a hand"));
		botBrain.StartAssist(o);
		return true;
	}

	private bool AssistStillNeeded()
	{
		PhysGrabObject assist = _assist;
		if ((Object)(object)assist == (Object)null || assist.dead || Time.time - _jobStarted > 120f)
		{
			return false;
		}
		foreach (PhysGrabber item in assist.playerGrabbing)
		{
			if ((Object)(object)item != (Object)null && (Object)(object)item.playerAvatar != (Object)(object)_bot.Avatar)
			{
				return true;
			}
		}
		return false;
	}

	private bool TakeOverCarry(PhysGrabObject o)
	{
		if ((Object)(object)o == (Object)null || o.dead || _bot.HeldView == 0 || (Object)(object)BotGrab.Held(_bot) != (Object)(object)o || InDanger() || !SemiFunc.RunIsLevel())
		{
			return false;
		}
		ExtractionPoint val = ActiveExtraction();
		PhysGrabCart val2 = (((Object)(object)val == (Object)null) ? KnownCart() : null);
		if ((Object)(object)val == (Object)null && (Object)(object)val2 == (Object)null)
		{
			return false;
		}
		StopAssist();
		_job = Job.Haul;
		_target = o;
		_cart = val2;
		_carry = Carry.Hold;
		_carryProblemSince = -1f;
		_jobStarted = Time.time;
		_grabbedAt = Time.time - 2f;
		_helpAskedAt = Time.time;
		_wedgedCount = 0;
		_haulCheckAt = 0f;
		_haulMoved = true;
		Claims[o] = this;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " took over carrying " + ((Object)o).name));
		return true;
	}

	private void StopAssist()
	{
		if ((Object)(object)_assist != (Object)null && Helpers.TryGetValue(_assist, out var value))
		{
			value.Remove(this);
			if (value.Count == 0)
			{
				Helpers.Remove(_assist);
			}
		}
		_assist = null;
	}

	private PhysGrabber AssistLeader()
	{
		if ((Object)(object)_assist == (Object)null)
		{
			return null;
		}
		PhysGrabber val = null;
		foreach (PhysGrabber item in _assist.playerGrabbing)
		{
			if (!((Object)(object)item == (Object)null) && !((Object)(object)item.playerAvatar == (Object)(object)_bot.Avatar) && ((Object)(object)val == (Object)null || (!BotRegistry.IsBot(item.playerAvatar) && BotRegistry.IsBot(val.playerAvatar))))
			{
				val = item;
			}
		}
		return val;
	}

	private Vector3 DoAssist(out Vector3? lookAt)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_024b: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0180: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_028a: Unknown result type (might be due to invalid IL or missing references)
		//IL_028c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		PhysGrabObject assist = _assist;
		if (!AssistStillNeeded())
		{
			if (TakeOverCarry(assist))
			{
				return Vector3.zero;
			}
			DropJob("DoAssist", 443);
			return Vector3.zero;
		}
		Vector3 centerPoint = assist.centerPoint;
		lookAt = centerPoint;
		Vector3 eye = Eye();
		if (_carry == Carry.Approach)
		{
			if (BotGrab.CanGrab(_bot, assist, eye, null, 4f))
			{
				if (BotGrab.Grab(_bot, assist, eye, null))
				{
					_carry = Carry.Hold;
					_grabbedAt = Time.time;
					return Vector3.zero;
				}
				DropJob("DoAssist", 459);
				return Vector3.zero;
			}
			return Steer(_bot.Position, centerPoint) * 3.2f;
		}
		if (_bot.HeldView == 0 || (Object)(object)BotGrab.Held(_bot) != (Object)(object)assist)
		{
			_carry = Carry.Approach;
			return Vector3.zero;
		}
		Vector3 val = Flat(_bot.Position - centerPoint);
		if (val.sqrMagnitude < 0.01f)
		{
			val = Vector3.right;
		}
		PhysGrabber val2 = AssistLeader();
		Vector3 val3 = Vector3.zero;
		if ((Object)(object)val2 != (Object)null && (Object)(object)val2.physGrabPoint != (Object)null)
		{
			Vector3 val4 = Flat(val2.physGrabPointPullerPosition - val2.physGrabPoint.position);
			Vector3 val5 = Flat(((Component)val2.playerAvatar).transform.position - centerPoint);
			if (val4.sqrMagnitude > 0.04f)
			{
				val3 = val4.normalized;
			}
			else
			{
				val3 = ((val5.sqrMagnitude > 0.04f) ? val5.normalized : Vector3.zero);
			}
		}
		Vector3 val6 = centerPoint + val.normalized * 1.8f;
		if (val3 != Vector3.zero && (!OnNavmesh(val6) || Physics.Linecast(centerPoint, val6 + Vector3.up * 0.2f, LayerMask.GetMask(new string[1] { "Default" }), (QueryTriggerInteraction)1)))
		{
			val6 = centerPoint - val3 * 1.8f;
		}
		val6 += val3 * 0.8f;
		Vector3 val7 = Flat(val6 - _bot.Position);
		float magnitude = val7.magnitude;
		if (magnitude < 0.4f)
		{
			return Vector3.zero;
		}
		return Steer(_bot.Position, val6) * Mathf.Lerp(2.2f, 3.2f, Mathf.Clamp01((magnitude - 0.4f) / 1.5f));
	}

	private void AssistPuller()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		PhysGrabber val = AssistLeader();
		PhysGrabber physGrabber = _bot.Avatar.physGrabber;
		if (!((Object)(object)val == (Object)null) && !((Object)(object)physGrabber == (Object)null))
		{
			Vector3 val2 = val.physGrabPointPullerPosition - val.physGrabPoint.position;
			Vector3 position = physGrabber.physGrabPoint.position;
			float num = _bot.Position.y + 1.5f - 0.55f;
			val2.y = Mathf.Max(val2.y, (num - position.y) * 0.6f);
			_bot.Puller = position + Vector3.ClampMagnitude(val2, 1.2f);
		}
	}

	private static bool IsUpgrade(ItemAttributes ia)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		if ((Object)(object)ia != (Object)null && (Object)(object)ia.item != (Object)null)
		{
			if ((int)ia.item.itemType != 4)
			{
				return ((Component)ia).GetComponent<ItemUpgrade>()?.isPlayerUpgrade ?? false;
			}
			return true;
		}
		return false;
	}

	private static bool IsHealthPack(ItemAttributes ia)
	{
		if ((Object)(object)ia != (Object)null)
		{
			return (Object)(object)((Component)ia).GetComponent<ItemHealthPack>() != (Object)null;
		}
		return false;
	}

	private void RememberWant(ItemAttributes ia)
	{
		if (!((Object)(object)ia == (Object)null) && !((Object)(object)ia.item == (Object)null))
		{
			string text = null;
			if (IsUpgrade(ia))
			{
				text = _bot.SteamId;
			}
			else if (IsHealthPack(ia))
			{
				List<PlayerAvatar> list = Hurt();
				PlayerAvatar a = ((list.Count > 0) ? list[0] : _bot.Avatar);
				text = ((!BotRegistry.IsBot(a)) ? null : BotRegistry.Get(a)?.SteamId);
			}
			if (text != null)
			{
				Wants.Add(new Want
				{
					Item = ((Object)ia.item).name,
					Owner = text
				});
				Plugin.Log.LogInfo((object)(_bot.Name + " wants " + ia.item.itemName + " used by " + ((text == _bot.SteamId) ? "itself" : text)));
			}
		}
	}

	private static void ForgetWant(ItemAttributes ia)
	{
		if (!((Object)(object)ia == (Object)null) && !((Object)(object)ia.item == (Object)null))
		{
			int num = Wants.FindLastIndex((Want w) => w.Item == ((Object)ia.item).name);
			if (num >= 0)
			{
				Wants.RemoveAt(num);
			}
		}
	}

	private static void NewShopWants()
	{
		Wants.Clear();
	}

	private bool HumanHurt()
	{
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && !player.isDisabled && !BotRegistry.IsBot(player) && (Object)(object)player.playerHealth != (Object)null && player.playerHealth.health < player.playerHealth.maxHealth)
			{
				return true;
			}
		}
		return false;
	}

	private bool TryStartUseItem()
	{
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		if (!SemiFunc.RunIsLobby() || Time.time < _useCheckAt || _bot.HeldView != 0 || _bot.HoldingStatic || InDanger())
		{
			return false;
		}
		_useCheckAt = Time.time + 1.5f;
		PlayerHealth playerHealth = _bot.Avatar.playerHealth;
		bool flag = (Object)(object)playerHealth != (Object)null && playerHealth.health < playerHealth.maxHealth;
		ItemAttributes val = null;
		float num = float.MaxValue;
		ItemAttributes[] array = Object.FindObjectsOfType<ItemAttributes>();
		foreach (ItemAttributes ia in array)
		{
			if ((Object)(object)ia == (Object)null || (Object)(object)ia.item == (Object)null)
			{
				continue;
			}
			PhysGrabObject component = ((Component)ia).GetComponent<PhysGrabObject>();
			if ((Object)(object)component == (Object)null || component.dead || !Available(component) || TakenForUse(ia))
			{
				continue;
			}
			bool flag2 = Wants.Exists((Want w) => w.Item == ((Object)ia.item).name && w.Owner == _bot.SteamId);
			bool flag3;
			if (IsUpgrade(ia))
			{
				flag3 = flag2;
			}
			else if (IsHealthPack(ia))
			{
				ItemHealthPack component2 = ((Component)ia).GetComponent<ItemHealthPack>();
				flag3 = flag && !component2.used && (flag2 || !HumanHurt());
			}
			else
			{
				flag3 = false;
			}
			if (flag3)
			{
				float num2 = Vector3.Distance(component.centerPoint, _bot.Position);
				if (num2 < 25f && num2 < num)
				{
					num = num2;
					val = ia;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		DropJob("TryStartUseItem", 112);
		_job = Job.Use;
		_useItem = val;
		_useName = ShopName(val);
		_useKey = ((Object)val.item).name;
		_useStarted = Time.time;
		_usePressedAt = -1f;
		PhysGrabObject component3 = ((Component)val).GetComponent<PhysGrabObject>();
		Claims[component3] = this;
		_target = component3;
		_thingPos[component3] = component3.centerPoint;
		_carry = Carry.Approach;
		_corners.Clear();
		ResetProgress();
		Plugin.Log.LogInfo((object)(_bot.Name + " is going to use " + val.item.itemName));
		return true;
	}

	private static bool TakenForUse(ItemAttributes ia)
	{
		foreach (BotBrain brain in BotManager.Brains)
		{
			if (brain != null && (Object)(object)brain._useItem == (Object)(object)ia)
			{
				return true;
			}
		}
		return false;
	}

	private bool UseStillGoing()
	{
		if ((Object)(object)_useItem != (Object)null)
		{
			return Time.time - _useStarted < 25f;
		}
		return false;
	}

	private Vector3 DoUse(out Vector3? lookAt)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		lookAt = null;
		ItemAttributes useItem = _useItem;
		PhysGrabObject val = (((Object)(object)useItem != (Object)null) ? ((Component)useItem).GetComponent<PhysGrabObject>() : null);
		if ((Object)(object)useItem == (Object)null || (Object)(object)val == (Object)null || val.dead || (IsHealthPack(useItem) && ((Component)useItem).GetComponent<ItemHealthPack>().used))
		{
			if (_usePressedAt > 0f)
			{
				FinishUse(useItem);
			}
			_useItem = null;
			DropJob("DoUse", 152);
			return Vector3.zero;
		}
		Vector3 val2 = Eye();
		lookAt = val.centerPoint;
		if (_bot.HeldView == 0 || (Object)(object)BotGrab.Held(_bot) != (Object)(object)val)
		{
			if (_usePressedAt > 0f)
			{
				StopUse();
				return Vector3.zero;
			}
			Vector3 val3 = val.centerPoint - val2;
			if (val3.magnitude <= 3.5f && BotGrab.CanGrab(_bot, val, val2, null, 3.5f) && BotGrab.Grab(_bot, val, val2, null))
			{
				_grabbedAt = Time.time;
				return Vector3.zero;
			}
			val3 = Flat(val.centerPoint - _bot.Position);
			if (val3.magnitude < 0.8f)
			{
				return Vector3.zero;
			}
			return Steer(_bot.Position, val.centerPoint) * 3.2f;
		}
		if (_usePressedAt < 0f && Time.time - _grabbedAt > 0.8f)
		{
			ItemToggle component = ((Component)useItem).GetComponent<ItemToggle>();
			if ((Object)(object)component == (Object)null || component.disabled)
			{
				StopUse();
				return Vector3.zero;
			}
			_usePressedAt = Time.time;
			component.ToggleItem(true, _bot.Avatar.photonView.ViewID);
			Plugin.Log.LogInfo((object)(_bot.Name + " used " + useItem.item.itemName));
		}
		if (_usePressedAt > 0f && Time.time - _usePressedAt > 3f)
		{
			StopUse();
		}
		return Vector3.zero;
	}

	private void FinishUse(ItemAttributes ia)
	{
		string item = (((Object)(object)ia != (Object)null && (Object)(object)ia.item != (Object)null) ? ShopName(ia) : (_useName ?? "thing"));
		int num = Wants.FindIndex((Want w) => w.Item == _useKey && w.Owner == _bot.SteamId);
		if (num < 0)
		{
			num = Wants.FindIndex((Want w) => w.Item == _useKey);
		}
		if (num >= 0)
		{
			Wants.RemoveAt(num);
		}
		BotSave.Add(_bot.SteamId, "used");
		bool flag = ((Object)(object)ia != (Object)null && IsHealthPack(ia)) || (_useKey != null && _useKey.ToLowerInvariant().Contains("health"));
		Say(flag ? Line("usedheal", null, null, item) : Line("usedupgrade", null, null, item), 0, "used:" + _bot.Name, 8f);
		if (!flag && (Object)(object)ia != (Object)null && (Object)(object)((Component)ia).GetComponent<ItemUpgradePlayerHealth>() != (Object)null)
		{
			BotHealth.Heal(_bot.Avatar.playerHealth, 20, effect: false);
		}
	}

	private void StopUse()
	{
		Plugin.Log.LogInfo((object)(_bot.Name + " put the " + (((Object)(object)_useItem != (Object)null && (Object)(object)_useItem.item != (Object)null) ? _useItem.item.itemName : "item") + " down"));
		ItemAttributes useItem = _useItem;
		_useItem = null;
		if (_bot.HeldView != 0)
		{
			ReleaseHeld("StopUse", 218);
		}
		if ((Object)(object)useItem != (Object)null)
		{
			PhysGrabObject component = ((Component)useItem).GetComponent<PhysGrabObject>();
			if ((Object)(object)component != (Object)null)
			{
				Blacklist[component] = Time.time + 30f;
			}
		}
		DropJob("StopUse", 225);
	}

	private void LearnTeamHealthInTruck()
	{
		if (!SemiFunc.RunIsLobby())
		{
			return;
		}
		foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
		{
			if ((Object)(object)player != (Object)null && (Object)(object)player != (Object)(object)_bot.Avatar && !player.isDisabled && BotRegistry.IsBot(player) && (Object)(object)player.playerHealth != (Object)null && (float)player.playerHealth.health < (float)player.playerHealth.maxHealth * 0.5f)
			{
				LearnHurt(player);
			}
		}
	}
}
