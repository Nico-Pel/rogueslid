using System.Collections.Generic;
using UnityEngine;

namespace YsoCorp {
    namespace GameUtils {

        public static class MaxSdkEventTracking {

            public enum VirtualResourceType {
                NONE,
                COINS,
                GEMS,
                ENERGY,
                HEARTS,
                XP,
                TOKENS,
                KEYS,
                MATERIALS,
                TICKETS,
                OTHER
            }

            private static void SendMaxEvent(string name, IDictionary<string, string> data = null) {
                MaxSdk.TrackEvent(name, data);
            }

            internal static void SendMaxEventAppOpen() => SendMaxEvent("app_open");

            internal static void SendMaxEventAdRewardedOpportunity() => SendMaxEvent("rewarded_ad_opportunity");
            internal static void SendMaxEventAdView() => SendMaxEvent("ad_view");
            internal static void SendMaxEventAdClicked() => SendMaxEvent("ad_clicked");

            internal static void SendMaxEventTutorialFinished() => SendMaxEvent("tutorial_complete");
            internal static void SendMaxEventLevelStart(int level) => SendMaxEvent("level_start", new Dictionary<string, string>() { { "value", level.ToString() } });
            internal static void SendMaxEventLevelFinished(int level) => SendMaxEvent("level_complete", new Dictionary<string, string>() { { "value", level.ToString() } });

            internal static void SendMaxEventVirtualResource(int value, VirtualResourceType resourceType = VirtualResourceType.NONE) {
                var data = new Dictionary<string, string>() { { "value", value.ToString() } };
                if (resourceType != VirtualResourceType.NONE) {
                    data.Add("resource_type", resourceType.ToString());
                }
                SendMaxEvent("virtual_resource_transaction", data);
            }
            internal static void SendMaxEventShopOpen() => SendMaxEvent("game_shop_enter");
        }
    
    }
}