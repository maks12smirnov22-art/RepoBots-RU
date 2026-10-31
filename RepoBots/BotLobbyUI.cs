using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using Photon.Pun;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RepoBots;

internal class BotLobbyUI : MonoBehaviour
{
	private TextMeshProUGUI _label;

	private int _shown = -1;

	private readonly List<RectTransform> _placed = new List<RectTransform>();

	private readonly List<Vector3> _offsets = new List<Vector3>();

	private RectTransform _anchor;

	public static void Setup(MenuPageLobby page)
	{
		if (SemiFunc.IsMultiplayer() && PhotonNetwork.IsMasterClient && !((Object)(object)page.inviteButton == (Object)null) && !((Object)(object)page.customizeButton == (Object)null) && !((Object)(object)((Component)page).GetComponent<BotLobbyUI>() != (Object)null))
		{
			((Component)page).gameObject.AddComponent<BotLobbyUI>().Build(page);
		}
	}

	private void Build(MenuPageLobby page)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected Obj, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected Obj, but got Unknown
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Expected Obj, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		RectTransform val = (_anchor = ((Component)page.customizeButton).GetComponent<RectTransform>());
		Transform transform = ((Component)page).transform;
		Rect rect = val.rect;
		float num = rect.height * ((Transform)val).lossyScale.y;
		if (num <= 0f)
		{
			num = 40f * ((Transform)val).lossyScale.y;
		}
		Add(MakeButton(page.inviteButton, transform, "REMOVE BOT", RemoveBot), new Vector3(0f, num * 1.15f, 0f));
		Add(MakeButton(page.inviteButton, transform, "ADD BOT", AddBot), new Vector3(0f, num * 2.3f, 0f));
		TextMeshProUGUI componentInChildren = ((Component)page.customizeButton).GetComponentInChildren<TextMeshProUGUI>();
		GameObject val2 = new GameObject("RepoBots Label", new Type[1] { typeof(RectTransform) });
		val2.transform.SetParent(transform, false);
		val2.AddComponent<LayoutElement>().ignoreLayout = true;
		_label = val2.AddComponent<TextMeshProUGUI>();
		((TMP_Text)_label).font = ((TMP_Text)componentInChildren).font;
		((TMP_Text)_label).fontSharedMaterial = ((TMP_Text)componentInChildren).fontSharedMaterial;
		((TMP_Text)_label).fontSize = ((TMP_Text)componentInChildren).fontSize * 0.55f;
		((TMP_Text)_label).enableWordWrapping = false;
		((TMP_Text)_label).alignment = (TextAlignmentOptions)4097;
		((Graphic)_label).color = (Color)(((Object)(object)AssetManager.instance != (Object)null) ? AssetManager.instance.colorYellow : new Color(1f, 0.6f, 0f));
		((Graphic)_label).raycastTarget = false;
		RectTransform component = val2.GetComponent<RectTransform>();
		component.pivot = new Vector2(0f, 0.5f);
		component.sizeDelta = new Vector2(700f, 40f);
		((Transform)component).localScale = Vector3.one;
		rect = val.rect;
		float num2 = (0f - rect.width) * val.pivot.x * ((Transform)val).lossyScale.x;
		Add(component, new Vector3(num2, num * 3.35f, 0f));
		Refresh();
		LateUpdate();
	}

	private void Add(RectTransform rt, Vector3 worldOffset)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		_placed.Add(rt);
		_offsets.Add(worldOffset);
	}

	private void LateUpdate()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_anchor == (Object)null)
		{
			return;
		}
		for (int i = 0; i < _placed.Count; i++)
		{
			if ((Object)(object)_placed[i] != (Object)null)
			{
				((Transform)_placed[i]).position = ((Transform)_anchor).position + _offsets[i];
			}
		}
	}

	private static RectTransform MakeButton(MenuButton template, Transform parent, string text, UnityAction onClick)
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected Obj, but got Unknown
		GameObject gameObject = ((Component)template).gameObject;
		bool activeSelf = gameObject.activeSelf;
		gameObject.SetActive(false);
		GameObject val = Object.Instantiate<GameObject>(gameObject, parent);
		gameObject.SetActive(activeSelf);
		((Object)val).name = "RepoBots " + text;
		Component[] componentsInChildren = val.GetComponentsInChildren<Component>(true);
		foreach (Component val2 in componentsInChildren)
		{
			if ((Object)(object)val2 != (Object)null && ((object)val2).GetType().Name.Contains("Locali"))
			{
				Object.Destroy((Object)(object)val2);
			}
		}
		(val.GetComponent<LayoutElement>() ?? val.AddComponent<LayoutElement>()).ignoreLayout = true;
		val.GetComponent<MenuButton>().buttonTextString = text;
		TextMeshProUGUI componentInChildren = val.GetComponentInChildren<TextMeshProUGUI>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			((TMP_Text)componentInChildren).text = text;
		}
		Button component = val.GetComponent<Button>();
		component.onClick = new Button.ButtonClickedEvent();
		((UnityEvent)component.onClick).AddListener(onClick);
		val.SetActive(true);
		return val.GetComponent<RectTransform>();
	}

	private void AddBot()
	{
		if (Plugin.BotCount.Value >= 2)
		{
			MenuManager.instance.MenuEffectClick((MenuManager.MenuClickEffectType)2, (MenuPage)null, -1f, -1f, false);
			return;
		}
		ConfigEntry<int> botCount = Plugin.BotCount;
		int value = botCount.Value;
		botCount.Value = value + 1;
		Refresh();
	}

	private void RemoveBot()
	{
		if (Plugin.BotCount.Value <= 0)
		{
			MenuManager.instance.MenuEffectClick((MenuManager.MenuClickEffectType)2, (MenuPage)null, -1f, -1f, false);
			return;
		}
		ConfigEntry<int> botCount = Plugin.BotCount;
		int value = botCount.Value;
		botCount.Value = value - 1;
		Refresh();
	}

	private void Update()
	{
		if (_shown != Plugin.BotCount.Value)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		if ((Object)(object)_label == (Object)null)
		{
			return;
		}
		int num = Mathf.Clamp(Plugin.BotCount.Value, 0, 2);
		_shown = Plugin.BotCount.Value;
		if (num == 0)
		{
			((TMP_Text)_label).text = "BOTS: NONE";
			return;
		}
		StringBuilder stringBuilder = new StringBuilder("BOTS: ");
		for (int i = 0; i < num; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(", ");
			}
			stringBuilder.Append(BotManager.Names[i].ToUpperInvariant());
		}
		((TMP_Text)_label).text = stringBuilder.ToString();
	}
}
