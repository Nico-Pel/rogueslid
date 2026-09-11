using UnityEngine;

namespace YsoCorp {

    namespace GameUtils {

        public class MmpBaseManager : BaseManager {

            internal virtual void Init() { }
            internal virtual void SendEvent(string eventName) { }
            internal virtual void SetConsent(bool consent) { }

        }

    }

}

