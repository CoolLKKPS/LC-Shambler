using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalLib.Modules;
using Shambler.src.Soul_Devourer;
using SolidLib.Registry;
using SoulDev;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Shambler
{
    [BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("solidlib", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static void LogDebug(string text)
        {
            Plugin.Logger.LogInfo(text);
        }

        public static void LogProduction(string text)
        {
            Plugin.Logger.LogInfo(text);
        }

        private void Awake()
        {
            Plugin.Logger = base.Logger;
            this.bindVars();
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
                foreach (MethodInfo method in methods)
                {
                    object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    bool flag = attributes.Length != 0;
                    if (flag)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            UnityEngine.Random.InitState((int)DateTime.Now.Ticks);
            try
            {
                string targetAssetName = "Shambler";
                string trueDistribution = Plugin.ScaleAllIntegers(Plugin.devourerSpawnDist.Value, Plugin.soulRarity.Value);
                EnemyInitializer.Initialize("bcs_shamblerenemybundle", new List<EnemyConfig>
                {
                    new EnemyConfig
                    {
                        Name = "Shambler Enemy",
                        AssetName = targetAssetName,
                        TerminalKeywordAsset = "ShamblerTK",
                        TerminalNodeAsset = "ShamblerTN",
                        Enabled = true,
                        MaxSpawnCount = Plugin.maxCount.Value,
                        PowerLevel = 2f,
                        SpawnWeights = trueDistribution
                    }
                });
                Plugin.Assets.PopulateAssets();
                Plugin.ShamblerStakePrefab = Plugin.Assets.MainAssetBundle.LoadAsset<GameObject>("ShamblerStake");
                NetworkPrefabs.RegisterNetworkPrefab(Plugin.ShamblerStakePrefab);
                Debug.Log("Shambler Enemy loaded with the following spawn weights: " + trueDistribution);
            }
            catch (Exception e)
            {
                Debug.LogError("Error initializing the Shambler Enemy, maybe the spawn distribution you entered is malformed? : error -> " + e.ToString());
            }
            global::On.RoundManager.SpawnScrapInLevel += delegate (global::On.RoundManager.orig_SpawnScrapInLevel orig, global::RoundManager self)
            {
                orig(self);
                try
                {
                    ShamblerEnemy.stuckPlayerIds.Clear();
                }
                catch (Exception e2)
                {
                    Debug.LogError("Shambler static value reset error: " + e2.ToString());
                }
            };
            global::On.RoundManager.DespawnPropsAtEndOfRound += delegate (global::On.RoundManager.orig_DespawnPropsAtEndOfRound orig, global::RoundManager self, bool despawnAllItems)
            {
                orig(self, despawnAllItems);
                bool isHost = global::RoundManager.Instance.IsHost;
                if (isHost)
                {
                    try
                    {
                        foreach (ShamblerStake stake in UnityEngine.Object.FindObjectsOfType<ShamblerStake>())
                        {
                            bool flag2 = stake.gameObject;
                            if (flag2)
                            {
                                UnityEngine.Object.Destroy(stake.gameObject);
                            }
                        }
                    }
                    catch (Exception e3)
                    {
                        Debug.LogError("Shambler static value reset error: " + e3.ToString());
                    }
                }
                else
                {
                    this.LateCleanupClient();
                }
            };
            global::On.GameNetcodeStuff.PlayerControllerB.DamagePlayer += delegate (global::On.GameNetcodeStuff.PlayerControllerB.orig_DamagePlayer orig, global::GameNetcodeStuff.PlayerControllerB self, int damageNumber, bool hasDamageSFX, bool callRPC, CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force)
            {
                try
                {
                    if (fallDamage)
                    {
                        bool flag3 = !this.PlayerAttachedToShamblerOrStake(self);
                        if (flag3)
                        {
                            orig(self, damageNumber, hasDamageSFX, callRPC, causeOfDeath, deathAnimation, fallDamage, force);
                        }
                        else
                        {
                            Debug.Log("Shambler FallDmg Cancel: = 0");
                            orig(self, 0, hasDamageSFX, callRPC, causeOfDeath, deathAnimation, fallDamage, force);
                        }
                    }
                    else
                    {
                        orig(self, damageNumber, hasDamageSFX, callRPC, causeOfDeath, deathAnimation, fallDamage, force);
                    }
                    return;
                }
                catch (Exception e4)
                {
                    Debug.LogException(e4);
                }
                orig(self, damageNumber, hasDamageSFX, callRPC, causeOfDeath, deathAnimation, fallDamage, force);
            };
            global::On.GameNetcodeStuff.PlayerControllerB.KillPlayer += delegate (global::On.GameNetcodeStuff.PlayerControllerB.orig_KillPlayer orig, global::GameNetcodeStuff.PlayerControllerB self, Vector3 bodyVelocity, bool spawnBody, CauseOfDeath causeOfDeath, int deathAnimation, Vector3 positionOffset)
            {
                try
                {
                    bool flag4 = causeOfDeath == CauseOfDeath.Gravity;
                    if (flag4)
                    {
                        bool flag5 = !this.PlayerAttachedToShamblerOrStake(self);
                        if (flag5)
                        {
                            orig(self, bodyVelocity, spawnBody, causeOfDeath, deathAnimation, positionOffset);
                        }
                        else
                        {
                            Debug.Log("Shambler FallDmgKill Cancel: = 0");
                        }
                    }
                    else
                    {
                        orig(self, bodyVelocity, spawnBody, causeOfDeath, deathAnimation, positionOffset);
                    }
                    return;
                }
                catch (Exception e5)
                {
                    Debug.LogException(e5);
                }
                orig(self, bodyVelocity, spawnBody, causeOfDeath, deathAnimation, positionOffset);
            };
            Plugin.Logger.LogInfo("Plugin Shambler is loaded!");
        }

        public bool PlayerAttachedToShamblerOrStake(global::GameNetcodeStuff.PlayerControllerB ply)
        {
            ShamblerEnemy[] shamblers = UnityEngine.Object.FindObjectsOfType<ShamblerEnemy>();
            ShamblerEnemy[] array = shamblers;
            int i = 0;
            while (i < array.Length)
            {
                ShamblerEnemy shambler = array[i];
                bool flag = shambler.capturedPlayer && shambler.capturedPlayer.NetworkObject.NetworkObjectId == ply.NetworkObject.NetworkObjectId;
                bool flag2;
                if (flag)
                {
                    flag2 = true;
                }
                else
                {
                    bool flag3 = shambler.stabbedPlayer && shambler.stabbedPlayer.NetworkObject.NetworkObjectId == ply.NetworkObject.NetworkObjectId;
                    if (!flag3)
                    {
                        i++;
                        continue;
                    }
                    flag2 = true;
                }
                return flag2;
            }
            ShamblerStake[] stakes = UnityEngine.Object.FindObjectsOfType<ShamblerStake>();
            foreach (ShamblerStake stake in stakes)
            {
                bool flag4 = stake.victim && stake.victim.NetworkObject.NetworkObjectId == ply.NetworkObject.NetworkObjectId;
                if (flag4)
                {
                    return true;
                }
            }
            return false;
        }

        public async void LateCleanupClient()
        {
            await Task.Delay(5000);
            foreach (ShamblerStake stake in UnityEngine.Object.FindObjectsOfType<ShamblerStake>())
            {
                if (stake.gameObject)
                {
                    UnityEngine.Object.Destroy(stake.gameObject);
                }

            }

        }

        public static string ScaleAllIntegers(string input, float multiplier)
        {
            return Regex.Replace(input, "\\d+", delegate (Match match)
            {
                int original = int.Parse(match.Value);
                return ((int)Math.Round((double)((float)original * multiplier))).ToString();
            });
        }

        public void bindVars()
        {
            Plugin.soulRarity = base.Config.Bind<float>("Spawning", "Enemy Spawnrate Multiplier", 1f, "Changes the spawnrate of the Shambler across ALL planets. Decimals are accepted, 2.0 = approximately double the spawnrate. 0.5 is half the spawnrate.");
            Plugin.devourerSpawnDist = base.Config.Bind<string>("Spawning", "Enemy Spawn Weights", "ExperimentationLevel:8,AssuranceLevel:20,OffenseLevel:20,MarchLevel:33,AdamanceLevel:33,DineLevel:14,RendLevel:14,TitanLevel:14,ArtificeLevel:33,Modded:33", "The spawn weight of the Shambler (multiplied by enemy spawnrate value) and the moons that the enemy can spawn on, in the form of a comma separated list of selectable level names and a weight value (e.g. \"ExperimentationLevel:300,DineLevel:20,RendLevel:10,Modded:10\")\r\nThe following strings: \"All\", \"Vanilla\", \"Modded\" are also valid.");
            Plugin.maxCount = base.Config.Bind<int>("Spawning", "Enemy Max Count", 3, "The maximum amount of Shamblers that can spawn in one day (hard cap).");
            Plugin.moaiGlobalMusicVol = base.Config.Bind<float>("Modifiers", "Enemy Sound Volume", 0.6f, "Changes the volume of all Shambler sounds. May make them more sneaky as well.");
            Plugin.moaiGlobalSpeed = base.Config.Bind<float>("Modifiers", "Enemy Speed Multiplier", 1f, "Changes the speed of all Shamblers. 4x would mean they are 4 times faster, 0.5x would be 2 times slower.");
            Plugin.health = base.Config.Bind<int>("Modifiers", "Enemy Health", 6, "Changes the health of all shamblers.");
            Plugin.LOSWidth = base.Config.Bind<float>("Advanced", "Line Of Sight Width", 100f, "Line of sight width for the enemy (by degrees).");
            Plugin.canEnterIndoors = base.Config.Bind<bool>("Modifiers", "Can enter the factory", true, "If shamblers can enter the factory at their own whim. Entry is chance based. The closer a shambler is to an entrance the more likely it will decide to enter.");
            Plugin.disableColliderOnDeath = base.Config.Bind<bool>("Modifiers", "Disable Collider On Death", true, "If enabled, the Shambler's collider will be disabled when it dies.");
            FloatInputFieldConfigItem spawnRateEntry = new FloatInputFieldConfigItem(Plugin.soulRarity, new FloatInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0.01f,
                Max = 100f
            });
            FloatSliderConfigItem volumeSlider = new FloatSliderConfigItem(Plugin.moaiGlobalMusicVol, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 1f
            });
            FloatSliderConfigItem speedSlider = new FloatSliderConfigItem(Plugin.moaiGlobalSpeed, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 5f
            });
            FloatSliderConfigItem LOSSlider = new FloatSliderConfigItem(Plugin.LOSWidth, new FloatSliderOptions
            {
                RequiresRestart = false,
                Min = 0f,
                Max = 360f
            });
            TextInputFieldConfigItem distEntry = new TextInputFieldConfigItem(Plugin.devourerSpawnDist, new TextInputFieldOptions
            {
                RequiresRestart = true
            });
            IntInputFieldConfigItem maxEntry = new IntInputFieldConfigItem(Plugin.maxCount, new IntInputFieldOptions
            {
                RequiresRestart = true,
                Min = 0,
                Max = 99999
            });
            IntInputFieldConfigItem healthEntry = new IntInputFieldConfigItem(Plugin.health, new IntInputFieldOptions
            {
                RequiresRestart = false,
                Min = 1,
                Max = 99999
            });
            BoolCheckBoxConfigItem indoorsEntry = new BoolCheckBoxConfigItem(Plugin.canEnterIndoors, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            });
            BoolCheckBoxConfigItem disableColliderEntry = new BoolCheckBoxConfigItem(Plugin.disableColliderOnDeath, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(spawnRateEntry);
            LethalConfigManager.AddConfigItem(distEntry);
            LethalConfigManager.AddConfigItem(volumeSlider);
            LethalConfigManager.AddConfigItem(speedSlider);
            LethalConfigManager.AddConfigItem(LOSSlider);
            LethalConfigManager.AddConfigItem(maxEntry);
            LethalConfigManager.AddConfigItem(healthEntry);
            LethalConfigManager.AddConfigItem(indoorsEntry);
            LethalConfigManager.AddConfigItem(disableColliderEntry);
        }

        public static Harmony _harmony;

        public new static ManualLogSource Logger;

        public static float rawSpawnMultiplier;

        public static GameObject ShamblerStakePrefab;

        public static ConfigEntry<float> moaiGlobalMusicVol;

        public static ConfigEntry<float> moaiGlobalSpeed;

        public static ConfigEntry<float> soulRarity;

        public static ConfigEntry<string> devourerSpawnDist;

        public static ConfigEntry<bool> spawnsOutside;

        public static ConfigEntry<float> LOSWidth;

        public static ConfigEntry<int> maxCount;

        public static ConfigEntry<int> health;

        public static ConfigEntry<bool> canEnterIndoors;

        public static ConfigEntry<bool> disableColliderOnDeath;

        public static class Assets
        {
            public static void PopulateAssets()
            {
                string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Plugin.Assets.MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(sAssemblyLocation, "bcs_shamblerstake"));
                bool flag = Plugin.Assets.MainAssetBundle == null;
                if (flag)
                {
                    Plugin.Logger.LogError("Failed to load custom assets.");
                }
            }

            public static AssetBundle MainAssetBundle;
        }
    }
}
