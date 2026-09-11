using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

namespace YsoCorp {

    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class MmpManager : BaseManager {

            internal event Action OnMmpsInit;

            [SerializeField] internal TenjinManager tenjinManager;

            private List<MmpBaseManager> _mmps = new List<MmpBaseManager>();
            private bool _isInit;


            private void Awake() {
                this.ycManager.mmpManager = this;
                if (this.ycManager.ycConfig.MmpTenjin) {
                    this._mmps.Add(this.tenjinManager);
                }
            }

            internal void Init() {
                foreach (MmpBaseManager mmp in this._mmps) {
                    mmp.gameObject.SetActive(true);
                    mmp.Init();
                }
                this.OnMmpsInit?.Invoke();
                this._isInit = true;
            }

            internal void SendEvent(string eventName) {
                foreach (MmpBaseManager mmp in this._mmps) {
                    mmp.SendEvent(eventName);
                }
            }
            internal void SetConsent(bool consent) {
                foreach (MmpBaseManager mmp in this._mmps) {
                    mmp.SetConsent(consent);
                }
            }

            public bool IsInit() {
                return this._isInit;
            }

        }

    }

}

