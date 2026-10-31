using Photon.Pun;
using UnityEngine;

namespace RepoBots;

internal static class BotHealth
{
	public static void Init(PlayerAvatar a)
	{
		PlayerHealth playerHealth = a.playerHealth;
		string steamID = a.steamID;
		int num = StatsManager.instance.GetPlayerHealth(steamID);
		if (num <= 0)
		{
			num = 1;
		}
		playerHealth.maxHealth = 100 + StatsManager.instance.GetPlayerMaxHealth(steamID);
		playerHealth.health = Mathf.Clamp(num, 1, playerHealth.maxHealth);
		playerHealth.healthSet = true;
		StatsManager.instance.SetPlayerHealth(steamID, playerHealth.health, false);
		playerHealth.photonView.RPC("UpdateHealthRPC", (RpcTarget)1, new object[4] { playerHealth.health, playerHealth.maxHealth, false, false });
	}

	public static void Hurt(PlayerHealth ph, int damage, bool savingGrace, int enemyIndex, bool hurtByHeal)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		PlayerAvatar playerAvatar = ph.playerAvatar;
		if (ph.invincibleTimer > 0f || damage <= 0 || playerAvatar.deadSet || playerAvatar.isDisabled || ph.godMode || (int)GameDirector.instance.currentState != 2)
		{
			return;
		}
		if (savingGrace && damage <= 25 && ph.health > 5 && ph.health <= 20)
		{
			ph.health -= damage;
			if (ph.health <= 0)
			{
				ph.health = Random.Range(1, 5);
			}
		}
		else
		{
			ph.health -= damage;
		}
		if (!hurtByHeal)
		{
			BotRegistry.Get(playerAvatar)?.OnHurt(damage);
			BotManager.BrainOf(playerAvatar)?.OnHurt(damage);
		}
		Plugin.Log.LogInfo((object)$"{playerAvatar.playerName} took {damage} damage ({Mathf.Max(ph.health, 0)}/{ph.maxHealth})");
		if (ph.health <= 0)
		{
			ph.health = 0;
			Plugin.Log.LogInfo((object)(playerAvatar.playerName + " died"));
			StatsManager.instance.SetPlayerHealth(playerAvatar.steamID, 0, false);
			ph.photonView.RPC("UpdateHealthRPC", (RpcTarget)1, new object[4] { 0, ph.maxHealth, true, hurtByHeal });
			playerAvatar.PlayerDeath(enemyIndex);
		}
		else
		{
			StatsManager.instance.SetPlayerHealth(playerAvatar.steamID, ph.health, false);
			int health = ph.health;
			ph.health = health + damage;
			ph.photonView.RPC("UpdateHealthRPC", (RpcTarget)0, new object[4] { health, ph.maxHealth, true, hurtByHeal });
		}
	}

	public static void Heal(PlayerHealth ph, int amount, bool effect)
	{
		PlayerAvatar playerAvatar = ph.playerAvatar;
		if (amount > 0 && ph.health != ph.maxHealth && !playerAvatar.isDisabled)
		{
			int num = Mathf.Clamp(ph.health + amount, 0, ph.maxHealth);
			StatsManager.instance.SetPlayerHealth(playerAvatar.steamID, num, false);
			ph.photonView.RPC("UpdateHealthRPC", (RpcTarget)0, new object[4] { num, ph.maxHealth, effect, false });
		}
	}
}
