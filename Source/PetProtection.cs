using HarmonyLib;
using System;
using System.Collections.Generic;
using Vector3 = UnityEngine.Vector3;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace PetProtection
{

    [BepInPlugin("org.bepinex.plugins.pet_protection", "Pet Protection", version)]
    public class PetProtection : BaseUnityPlugin
    {
        public static PetProtection instance;

        public ConfigEntry<float> stunRecoveryTime;
        private ConfigEntry<string> excludedListConfig;
        public List<string> excludedList;

        public const string version = "0.2.0";
        internal static ManualLogSource Log;

        public static System.Timers.Timer mapSyncSaveTimer =
            new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);

        public static Harmony harmony = new Harmony("mod.pet_protection");

        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            instance = this;
            Log = base.Logger;
            //Log.LogInfo("Beginning Patch");

            stunRecoveryTime = Config.Bind("General", "Stun recovery time", 900.0f, "Time in seconds pet will be stuneed after receiving a killing hit.");
            excludedListConfig = Config.Bind("General", "Exclude list", "", "List of tamed creatures, which should not be proitected.");
            excludedListConfig.SettingChanged += (_, _) => 
            {
              updateExcludedList();
            };
            updateExcludedList();
            harmony.PatchAll();
        }

        void updateExcludedList()
        {
            Logger.LogInfo("Updating excluded list");
            string[] array = excludedListConfig.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            excludedList = new List<string>();
            foreach (string text in array)
            {
              excludedList.Add(text.Trim());
            }
        }
        void OnDestroy()
        {
          instance = null;
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
                if(ShouldIgnoreDamage(__instance, hit, zdo)) {
                    __instance.SetHealth(__instance.GetMaxHealth());
                    __instance.m_animator.SetBool("sleeping", true);
                    zdo.Set("sleeping", true);
                    zdo.Set("isRecoveringFromStun", true);
                }
            }
        }

        private static bool CheckExcluded(string name)
        {
          if (PetProtection.instance.excludedList.Count == 0) return false;
          foreach (string excludedPrefix in PetProtection.instance.excludedList)
          {
            if (name.StartsWith(excludedPrefix)) return true;
          }
          return false;
        }

        private static bool ShouldIgnoreDamage(Character __instance, HitData hit, ZDO zdo)
        {
            if(hit == null)
                return true;

            if (CheckExcluded(__instance.name))
              return false;

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

            if (timeSinceStun >= PetProtection.instance.stunRecoveryTime.Value)
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
            {
                int pos = __result.IndexOf(" )");
                if (pos < 0)
                {
                  __result = __result.Insert(0, "(Stunned)\n");
                }
                else
                {
                  __result = __result.Insert(pos, ", Stunned");
                }
            }
        }
    }
}
