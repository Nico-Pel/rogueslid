using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Network = AppLovinMax.Scripts.IntegrationManager.Editor.Network;

namespace YsoCorp {
    namespace GameUtils {

        public class YCMaxUpgradeWindow : YCCustomWindow {

            private struct NetworkUpdate {
                public Network network;
                public string name;
                public bool selected;

                public NetworkUpdate(Network _network, string _name, bool _selected) {
                    this.network = _network;
                    this.name = _name;
                    this.selected = _selected;
                }
            }

            private bool manuallyClosed = false;
            private NetworkUpdate mainPluginUpdate;
            private NetworkUpdate[] networkUpdates;

            public void Init(Network main, Network[] networks) {
                float height = 80;
                if (main != null) {
                    this.mainPluginUpdate = new NetworkUpdate(main, main.DisplayName, true);
                    height += 18;
                } else {
                    this.mainPluginUpdate = new NetworkUpdate(null, "", false);
                }
                this.networkUpdates = new NetworkUpdate[networks.Length];
                for (int i = 0; i < networks.Length; i++) {
                    string name = networks[i].DisplayName;
                    //bool ignore = name.Contains("Google");
                    bool ignore = false;
                    this.networkUpdates[i] = new NetworkUpdate(networks[i], networks[i].DisplayName, !ignore);
                    height += 18;
                }
                if (main != null || networks.Length > 0) {
                    height += 35;
                }
                this.SetSize(250, height);
                this.SetPosition(WindowPosition.MiddleCenter);
            }

            public override void OnGUI() {
                if (this.networkUpdates.Length <= 0 && this.mainPluginUpdate.network == null) {
                    YCEditorGUILayout.AddLabel("Everything is up to date.", TextAnchor.MiddleCenter);
                    YCEditorGUILayout.AddButton("Ok", () => {
                        YCUpdatesHandler.ResetMax();
                        this.manuallyClosed = true;
                        this.Close();
                    });
                } else {
                    YCEditorGUILayout.AddHelpBox("Do not modify this", UnityEditor.MessageType.Warning);
                    YCEditorGUILayout.AddLabel("Pending upgrades :");
                    this.DrawNetworks();
                    YCEditorGUILayout.AddEmptyLine();
                    this.AddCancelOk("Cancel", "Ok", () => {
                        YCUpdatesHandler.ResetMax();
                        Debug.Log("Applovin MAX upgrade canceled");
                        this.manuallyClosed = true;
                        this.Close();
                    }, () => {
                        Network main = this.mainPluginUpdate.selected ? this.mainPluginUpdate.network : null;
                        List<Network> selectedNetworks = new List<Network>();

                        for (int i = 0; i < this.networkUpdates.Length; i++) {
                            if (this.networkUpdates[i].selected) {
                                selectedNetworks.Add(this.networkUpdates[i].network);
                            }
                        }
                        if (main == null && selectedNetworks.Count <= 0) {
                            YCUpdatesHandler.ResetMax();
                            Debug.Log("Applovin MAX upgrade canceled");
                        } else {
                            Debug.Log("Upgrading Applovin MAX ...");
                            YCUpdatesHandler.ApplySelectedMaxUpgrades(main, selectedNetworks);
                        }
                        this.manuallyClosed = true;
                        this.Close();
                    });
                }
            }

            private void DrawNetworks() {
                if (this.mainPluginUpdate.network != null) {
                    this.mainPluginUpdate.selected = YCEditorGUILayout.AddToggle(this.mainPluginUpdate.name, this.mainPluginUpdate.selected);
                }
                for (int i = 0; i < this.networkUpdates.Length; i++) {
                    this.networkUpdates[i].selected = YCEditorGUILayout.AddToggle(this.networkUpdates[i].name, this.networkUpdates[i].selected);
                }
            }

            private void OnDestroy() {
                if (this.manuallyClosed == false) {
                    YCUpdatesHandler.ResetMax();
                    Debug.Log("Applovin MAX upgrade canceled");
                }
            }
        }
    }
}
