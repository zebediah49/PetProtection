using HarmonyLib;
using System;
using UnityEngine;
using BepInEx;

namespace PetProtection
{

    [BepInPlugin("org.bepinex.plugins.pet_protection", "Pet Protection", version)]
    public class PetProtection : BaseUnityPlugin
    {
	public static float stunRecoveryTime { get; internal set; } = 900f;
	public const string version = "0.1.0";

	public static System.Timers.Timer mapSyncSaveTimer =
	    new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);

	public static Harmony harmony = new Harmony("mod.pet_protection");

	// Awake is called once when both the game and the plug-in are loaded
	void Awake()
	{
	    Logger.LogInfo("Beginning Patch");

	    harmony.PatchAll();
	}
    }

    /// <summary>
    /// Determines what happens when a tamed creature takes damage.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class Character_Damage_Patch
    {
	public static void Postfix(ref Character __instance, ref HitData hit, ref bool showDamageText, ref bool triggerEffects, ref HitData.DamageModifier mod)
	{
	    // Network & Tameable component
	    ZDO zdo = __instance.m_nview.GetZDO();
	    Tameable tamed = __instance.GetComponent<Tameable>();

	    // Is tamed, has network, has valid hit data, tamed component is present.
	    if (!__instance.IsTamed() || zdo == null || tamed == null)
		return;

	    // if killed on this hit
	    if (__instance.GetHealth() <= 5f)
	    {
		// Allow players to kill the tamed creature with ownerDamageOverride
		if(ShouldIgnoreDamage(__instance, hit, zdo)){
		    __instance.SetHealth(__instance.GetMaxHealth());
		    __instance.m_animator.SetBool("sleeping", true);
		    zdo.Set("sleeping", true);
		    zdo.Set("isRecoveringFromStun", true);
		}
	    }
	}

	private static bool ShouldIgnoreDamage(Character __instance, HitData hit, ZDO zdo)
	{
            if(hit == null)
                return true;
	    Character attacker = hit.GetAttacker();
	    if(attacker == null) {
                return true;
	    }

	    // Attacker is player
	    if (attacker == __instance.GetComponent<Tameable>().GetPlayer(attacker.GetZDOID()))
		return false;
	    return true;
	}
    }

    /// <summary>
    /// Forces a tamed creature to stay asleep if it's recovering from being stunned.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateSleep))]
    public static class MonsterAI_UpdateSleep_Patch
    {
	public static void Prefix(MonsterAI __instance, ref float dt)
	{
	    Tameable tamed = __instance.GetComponent<Tameable>();
	    if (tamed == null)
		return;

	    MonsterAI monsterAI = __instance;
	    ZDO zdo = monsterAI.m_nview.GetZDO();

	    if (zdo == null || !zdo.GetBool("isRecoveringFromStun"))
		return;

	    if (monsterAI.m_character.m_moveDir != Vector3.zero)
		monsterAI.StopMoving();

	    if (monsterAI.m_sleepTimer != 0f)
		monsterAI.m_sleepTimer = 0f;

	    float timeSinceStun = zdo.GetFloat("timeSinceStun") + dt;
	    zdo.Set("timeSinceStun", timeSinceStun);

	    if (timeSinceStun >= PetProtection.stunRecoveryTime)
	    {
		zdo.Set("timeSinceStun", 0f);
		monsterAI.m_sleepTimer = 0.5f;
		monsterAI.m_character.m_animator.SetBool("sleeping", false);
		zdo.Set("sleeping", false);
		zdo.Set("isRecoveringFromStun", false);
	    }

	    dt = 0f;
	}
    }

    /// <summary>
    /// Adds a text indicator so player's know when an animal they've tamed has been stunned.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    public static class Tameable_GetHoverText_Patch
    {
	public static void Postfix(Tameable __instance, ref string __result)
	{
	    Tameable tameable = __instance;

	    // If tamed creature is recovering from a stun, then add Stunned to hover text.
	    if (tameable.m_character.m_nview.GetZDO().GetBool("isRecoveringFromStun"))
		__result = __result.Insert(__result.IndexOf(" )"), ", Stunned");
	}
    }
}
