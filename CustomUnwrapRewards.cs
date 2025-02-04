/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Unwrap Rewards", "VisEntities", "1.0.0")]
    [Description(" ")]
    public class CustomUnwrapRewards : RustPlugin
    {
        #region Fields

        private static CustomUnwrapRewards _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Unwrap Rewards")]
            public Dictionary<string, List<RewardConfig>> UnwrapRewards { get; set; }

            [JsonProperty("Rarity Weights")]
            public Dictionary<Rarity, int> RarityWeights { get; set; }
        }

        public class RewardConfig
        {
            [JsonProperty("Item Short Name")]
            public string ItemShortName { get; set; }

            [JsonProperty("Display Name")]
            public string DisplayName { get; set; }

            [JsonProperty("Skin Id")]
            public ulong SkinId { get; set; }

            [JsonProperty("Minimum Amount")]
            public int MinimumAmount { get; set; }

            [JsonProperty("Maximum Amount")]
            public int MaximumAmount { get; set; }

            [JsonProperty("Rarity")]
            [JsonConverter(typeof(StringEnumConverter))]
            public Rarity Rarity { get; set; }
        }

        public enum Rarity
        {
            Common,
            Uncommon,
            Rare,
            VeryRare
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                RarityWeights = new Dictionary<Rarity, int>
                {
                    { Rarity.Common, 60 },
                    { Rarity.Uncommon, 25 },
                    { Rarity.Rare, 10 },
                    { Rarity.VeryRare, 5 }
                },
                UnwrapRewards = new Dictionary<string, List<RewardConfig>>
                {
                    {
                        "easter.goldegg", new List<RewardConfig>
                        {
                            new RewardConfig
                            {
                                ItemShortName = "ammo.rocket.mlrs",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 1,
                                MaximumAmount = 2,
                                Rarity = Rarity.Common
                            },
                            new RewardConfig
                            {
                                ItemShortName = "explosives",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 3,
                                MaximumAmount = 7,
                                Rarity = Rarity.Uncommon
                            },
                            new RewardConfig
                            {
                                ItemShortName = "explosive.satchel",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 1,
                                MaximumAmount = 3,
                                Rarity = Rarity.Rare
                            },
                            new RewardConfig
                            {
                                ItemShortName = "metal.facemask",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 1,
                                MaximumAmount = 1,
                                Rarity = Rarity.Rare
                            },
                            new RewardConfig
                            {
                                ItemShortName = "potato",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 10,
                                MaximumAmount = 20,
                                Rarity = Rarity.VeryRare
                            },
                            new RewardConfig
                            {
                                ItemShortName = "t1_smg",
                                DisplayName = null,
                                SkinId = 0,
                                MinimumAmount = 1,
                                MaximumAmount = 1,
                                Rarity = Rarity.VeryRare
                            }
                        }
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object OnItemUnwrap(Item item, BasePlayer player, ItemModUnwrap itemUnwrap)
        {
            if (player == null || item == null || itemUnwrap == null)
                return null;

            string unwrapKey = item.info.shortname;
            if (!_config.UnwrapRewards.ContainsKey(unwrapKey))
                return null;

            item.UseItem(1);

            int numTries = Random.Range(itemUnwrap.minTries, itemUnwrap.maxTries + 1);
            List<RewardConfig> rewards = _config.UnwrapRewards[unwrapKey];

            for (int i = 0; i < numTries; i++)
            {
                RewardConfig selectedReward = SelectRewardByRarity(rewards, _config.RarityWeights);
                if (selectedReward == null)
                    continue;

                int amountToGive = Random.Range(selectedReward.MinimumAmount, selectedReward.MaximumAmount + 1);
                if (amountToGive <= 0)
                    continue;

                Item rewardItem = ItemManager.CreateByName(selectedReward.ItemShortName, amountToGive, selectedReward.SkinId);
                if (rewardItem == null)
                    continue;

                if (!string.IsNullOrEmpty(selectedReward.DisplayName))
                    rewardItem.name = selectedReward.DisplayName;

                ItemContainer container = player.inventory.containerMain;
                if (!rewardItem.MoveToContainer(container))
                {
                    if (container.playerOwner != null)
                    {
                        rewardItem.Drop(container.playerOwner.GetDropPosition(), container.playerOwner.GetDropVelocity(), default(Quaternion));
                    }
                    else
                    {
                        rewardItem.Remove(0f);
                    }
                }
            }

            if (itemUnwrap.successEffect != null && itemUnwrap.successEffect.isValid)
            {
                Effect.server.Run(itemUnwrap.successEffect.resourcePath, player.eyes.position, default(Vector3), null, false, null);
            }

            return true;
        }

        #endregion Oxide Hooks

        #region Weighted Reward Selection

        private RewardConfig SelectRewardByRarity(List<RewardConfig> rewards, Dictionary<Rarity, int> rarityWeights)
        {
            int totalWeight = 0;
            foreach (var reward in rewards)
            {
                if (rarityWeights.TryGetValue(reward.Rarity, out int weight))
                    totalWeight += weight;
            }
            if (totalWeight <= 0)
                return null;

            int randomValue = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            foreach (var reward in rewards)
            {
                if (rarityWeights.TryGetValue(reward.Rarity, out int weight))
                {
                    cumulative += weight;
                    if (randomValue < cumulative)
                    {
                        return reward;
                    }
                }
            }
            return null;
        }

        #endregion Weighted Reward Selection
    }
}