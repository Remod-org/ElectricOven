#region License (GPL v3)
/*
    DESCRIPTION
    Copyright (c) 2021 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License Information (GPL v3)
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ElectricOven", "RFC1920", "1.0.8")]
    [Description("Refineries, cauldrons and BBQ can use electricity instead of wood.")]
    internal class ElectricOven : RustPlugin
    {
        private ConfigData configData;

        private const string permUse = "electricoven.use";
        private const string CBTN = "oven.status";
        private const string cauldron = "cursedcauldron.deployed";
        private const string refinery = "refinery_small_deployed";
        private const string bbq = "bbq.deployed";
        public List<uint> ovens = new List<uint>();

        private bool startup;
        private readonly List<string> orDefault = new List<string>();

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));
        #endregion

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "off", "OFF" },
                { "on", "ON" },
                { "notauthorized", "You don't have permission to do that !!" },
                { "enabled", "Electric oven enabled" },
                { "disabled", "Electric oven disabled" }
            }, this);
        }

        private void DoLog(string message)
        {
            if (configData.Settings.debug) Puts(message);
        }

        [Command("cr"), Permission(permUse)]
        private void EnableDisable(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.HasPermission(permUse) && configData.Settings.requirePermission) { Message(iplayer, "notauthorized"); return; }

            bool en = configData.Settings.defaultEnabled;
            if (orDefault.Contains(iplayer.Id))
            {
                orDefault.Remove(iplayer.Id);
            }
            else
            {
                orDefault.Add(iplayer.Id);
                en = !en;
            }
            switch (en)
            {
                case true:
                    Message(iplayer, "enabled");
                    break;
                case false:
                    Message(iplayer, "disabled");
                    break;
            }
        }

        private void OnServerInitialized()
        {
            LoadData();

            List<uint> toremove = new List<uint>();
            foreach (uint pid in ovens)
            {
                DoLog("Setting up old oven");
                BaseNetworkable oven = BaseNetworkable.serverEntities.Find(pid);
                if (oven == null)
                {
                    toremove.Add(pid);
                    continue;
                }
                ElectricalBranch br = oven.gameObject.GetComponentInChildren<ElectricalBranch>();
                if (br != null) RemoveComps(br);

                SimpleLight lt = oven.gameObject.GetComponentInChildren<SimpleLight>();
                if (lt != null) RemoveComps(lt);
            }
            foreach (uint tr in toremove)
            {
                ovens.Remove(tr);
            }

            SaveData();
            startup = true;
        }

        private void Init()
        {
            LoadConfigValues();
            AddCovalenceCommand("cr", "EnableDisable");
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, CBTN);
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null) return null;
            if (container == null) return null;
            BaseEntity oven = container.GetComponentInParent<BaseEntity>();
            if (oven == null) return null;
            if (oven?.ShortPrefabName == null) return null;
            if (oven.ShortPrefabName.Equals(cauldron) && configData.Settings.handleCauldron)
            {
                SimpleLight electrified = container.GetComponentInChildren<SimpleLight>();
                if (electrified == null) return null;

                string status = Lang("off");
                if (electrified.IsPowered()) status = Lang("on");

                PowerGUI(player, "cauldron", status);
            }
            else if (oven.ShortPrefabName.Equals(bbq) && configData.Settings.handleBBQ)
            {
                SimpleLight electrified = container.GetComponentInChildren<SimpleLight>();
                if (electrified == null) return null;

                string status = Lang("off");
                if (electrified.IsPowered()) status = Lang("on");

                PowerGUI(player, "bbq", status);
            }
            else if (oven.ShortPrefabName.Equals(refinery) && configData.Settings.handleRefinery)
            {
                SimpleLight electrified = container.GetComponentInChildren<SimpleLight>();
                if (electrified == null) return null;

                string status = Lang("off");
                if (electrified.IsPowered()) status = Lang("on");

                PowerGUI(player, "refinery", status);
            }
            return null;
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null) return;
            CuiHelper.DestroyUi(player, CBTN);
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!ovens.Contains(oven.net.ID))
            {
                DoLog("Oven ID not found, skipping...");
                return null;
            }

            if (oven.IsOn())
            {
                DoLog("Toggled off");
                oven.StopCooking();
                return null;
            }

            DoLog("This is one of our ovens!");
            ElectricalBranch eb = oven.GetComponentInChildren<ElectricalBranch>();
            if (eb?.currentEnergy > 0)
            {
                DoLog("Oven has power!");
                if (!oven.IsOn())
                {
                    oven.StartCooking();
                }
            }

            return null;
        }

        private Item OnFindBurnable(BaseOven oven)
        {
            bool cooking = false;
            if (ovens.Contains(oven.net.ID))
            {
                foreach (Item current in oven.inventory.itemList)
                {
                    if (current.info.name.Contains("raw")) cooking = true;
                    if (current.info.name.Equals("crude_oil.item")) cooking = true;
                    else if (current.info.name.Contains("wood")) cooking = true;
                }

                if (configData.Settings.allowOvercooking) cooking = true;

                ElectricalBranch eb = oven.GetComponentInChildren<ElectricalBranch>();
                if (eb?.currentEnergy > 0 && cooking)
                {
                    //DoLog("Adding virtual wood");
                    return ItemManager.CreateByName("wood", 1, 0);
                }
            }
            return null;
        }

        private object OnNoPowerLightsToggle(IOEntity light)
        {
            if (ovens.Contains(light.parentEntity.uid))
            {
                return true;
            }
            return null;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            if (entity is BaseOven && entity?.net.ID > 0 && ovens.Contains(entity.net.ID))
            {
                ovens.Remove(entity.net.ID);
            }
        }

        private void OnEntitySpawned(BaseEntity oven)
        {
            if (!startup) return;
            if (oven == null) return;
            if (string.IsNullOrEmpty(oven.ShortPrefabName)) return;
            if ((oven.ShortPrefabName.Equals(cauldron) && configData.Settings.handleCauldron)
                || (oven.ShortPrefabName.Equals(bbq) && configData.Settings.handleBBQ)
                || (oven.ShortPrefabName.Equals(refinery) && configData.Settings.handleRefinery))
            {
                DoLog("Checking ownerID for oven");
                string ownerid = oven?.OwnerID.ToString();

                if (!permission.UserHasPermission(ownerid, permUse) && configData.Settings.requirePermission) return;

                DoLog($"Found ownerID {ownerid}");
                if (configData.Settings.defaultEnabled && orDefault.Contains(ownerid))
                {
                    DoLog("Plugin enabled by default, but player-disabled");
                    return;
                }
                else if (!configData.Settings.defaultEnabled && !orDefault.Contains(ownerid))
                {
                    DoLog("Plugin disabled by default, and not player-enabled");
                    return;
                }

                // Doing this here so that the hook works to prevent turning the light on by another plugin immediately upon spawn
                ovens.Add(oven.net.ID);
                SaveData();

                BaseEntity bent = oven.gameObject.GetComponentInChildren<ElectricalBranch>() as BaseEntity ?? GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab", oven.transform.position, oven.transform.rotation, true);
                ElectricalBranch branch = bent as ElectricalBranch;
                if (bent != null)
                {
                    if (oven.ShortPrefabName.Equals(cauldron))
                    {
                        bent.transform.localEulerAngles = new Vector3(0, 270, 180);
                        bent.transform.localPosition = new Vector3(-0.29f, 0.65f, 0);
                    }
                    else if (oven.ShortPrefabName.Equals(refinery))
                    {
                        bent.transform.localEulerAngles = new Vector3(180, 270, 0);
                        bent.transform.localPosition = new Vector3(0.75f, 0.65f, 0);
                    }
                    else
                    {
                        bent.transform.localEulerAngles = new Vector3(0, 180, 180);
                        bent.transform.localPosition = new Vector3(0f, 0.83f, -0.48f);
                    }

                    bent.OwnerID = oven.OwnerID;
                    bent.SetParent(oven);
                    RemoveComps(bent);
                    bent.Spawn();
                }

                BaseEntity lent = oven.gameObject.GetComponentInChildren<SimpleLight>() as BaseEntity ?? GameManager.server.CreateEntity("assets/prefabs/misc/permstore/industriallight/industrial.wall.lamp.red.deployed.prefab", oven.transform.position, oven.transform.rotation, true);
                SimpleLight lamp = lent as SimpleLight;
                if (lent != null)
                {
                    if (oven.ShortPrefabName.Equals(cauldron))
                    {
                        lent.transform.localEulerAngles = new Vector3(0, 0, 0);
                        lent.transform.localPosition = new Vector3(0, 0.5f, 0);
                    }
                    else if (oven.ShortPrefabName.Equals(refinery))
                    {
                        lent.transform.localEulerAngles = new Vector3(180, 0, 0);
                        lent.transform.localPosition = new Vector3(0, 0.5f, 0);
                    }
                    else
                    {
                        lent.transform.localEulerAngles = new Vector3(270, 90, 0);
                        lent.transform.localPosition = new Vector3(0, 0.72f, 0);
                    }
                    lent.OwnerID = oven.OwnerID;
                    lent.SetParent(oven);
                    lent.SetFlag(BaseEntity.Flags.Busy, true);
                    RemoveComps(lent);
                    lent.Spawn();
                }

                if (lamp != null && branch != null)
                {
                    Connect(lamp, branch);
                }
            }
        }

        public void RemoveComps(BaseEntity obj)
        {
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<GroundWatch>());
            foreach (MeshCollider mesh in obj.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private void Connect(SimpleLight lampIO, ElectricalBranch branchIO)
        {
            DoLog("Connecting lamp to branch");
            const int inputSlot = 0;
            const int outputSlot = 1;
            branchIO.branchAmount = 5;

            IOEntity.IOSlot branchOutput = branchIO.outputs[outputSlot];
            IOEntity.IOSlot lampInput = lampIO.inputs[inputSlot];

            lampInput.connectedTo = new IOEntity.IORef();
            lampInput.connectedTo.Set(branchIO);
            lampInput.connectedToSlot = outputSlot;
            lampInput.connectedTo.Init();
            lampInput.connectedTo.ioEnt._limitedNetworking = true;
            DoLog($"Lamp input slot {inputSlot.ToString()}:{lampInput.niceName} connected to {branchIO.ShortPrefabName}:{branchOutput.niceName}");

            branchOutput.connectedTo = new IOEntity.IORef();
            branchOutput.connectedTo.Set(lampIO);
            branchOutput.connectedToSlot = inputSlot;
            branchOutput.connectedTo.Init();
            branchOutput.connectedTo.ioEnt._limitedNetworking = true;
            branchIO.MarkDirtyForceUpdateOutputs();
            branchIO.SendNetworkUpdate();
            DoLog($"Branch output slot {outputSlot.ToString()}:{branchOutput.niceName} connected to {lampIO.ShortPrefabName}:{lampInput.niceName}");
        }

        private void LoadData()
        {
            ovens = Interface.Oxide.DataFileSystem.ReadObject<List<uint>>(Name + "/ovens") ?? new List<uint>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name + "/ovens", ovens);
        }

        private void PowerGUI(BasePlayer player, string type = "cauldron", string onoff = "")
        {
            CuiHelper.DestroyUi(player, CBTN);

            string label = "Electricity: " + onoff;
            string[] pos = new string[2];
            switch (type)
            {
                case "bbq":
                    pos[0] = "0.85 0.465";
                    pos[1] = "0.946 0.483";
                    break;
                default:
                    pos[0] = "0.85 0.385";
                    pos[1] = "0.946 0.413";
                    break;
            }

            CuiElementContainer container = UI.Container(CBTN, UI.Color("8B816B", 0.16f), pos[0], pos[1], true, "Overlay");
            UI.Label(ref container, CBTN, UI.Color("#c7c7c7", 1f), label, 12, "0 0", "1 1");

            CuiHelper.AddUi(player, container);
        }

        public class Settings
        {
            public bool defaultEnabled;
            public bool requirePermission;
            public bool allowOvercooking;
            public bool handleRefinery;
            public bool handleBBQ;
            public bool handleCauldron;
            public bool debug;
        }

        private class ConfigData
        {
            public Settings Settings;
            public VersionNumber Version;
        }

        private void LoadConfigValues()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < new VersionNumber(1, 0, 5))
            {
                configData.Settings.handleCauldron = true;
                configData.Settings.handleBBQ = true;
                configData.Settings.handleRefinery = true;
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData()
            {
                Settings = new Settings()
                {
                    defaultEnabled = true,
                    requirePermission = false,
                    allowOvercooking = false,
                    handleBBQ = true,
                    handleCauldron = true,
                    handleRefinery = true,
                    debug = false
                },
                Version = Version
            };

            SaveConfig(config);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        public static class UI
        {
            public static CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                return new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color },
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
            }

            public static void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }

            public static void Label(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                {
                    hexColor = hexColor.Substring(1);
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
    }
}
