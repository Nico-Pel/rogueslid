using System;
using UnityEngine;

namespace YsoCorp {
    namespace GameUtils {

        public class SoundElement : MonoBehaviour {

            public AudioSource audioSource;
            public float time;

            public Action onDestroyAction;

            private void OnDestroy() {
                if (this.onDestroyAction != null) {
                    this.onDestroyAction.Invoke();
                }
            }
        }

    }
}