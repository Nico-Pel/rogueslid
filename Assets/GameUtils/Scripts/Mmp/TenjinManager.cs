using UnityEngine;
using System.Collections.Generic;
#if YC_NEWTONSOFT
using Newtonsoft.Json;
#endif
using System.Linq;

#if IN_APP_PURCHASING
using UnityEngine.Purchasing;
#endif

namespace YsoCorp {

    namespace GameUtils {

        [DefaultExecutionOrder(-10)]
        public sealed class TenjinManager : MmpBaseManager {

            private static string API_KEY = "BP2IBD5EPSJLBT2JYHWDGTVGXQVF6YGK";

            private BaseTenjin _tenjin;
            internal AnalyticsManager.TenjinData tenjinData = new AnalyticsManager.TenjinData();

            internal override void Init() {
                if (this._tenjin == null) {
                    this._tenjin = Tenjin.getInstance(API_KEY);
                    this._tenjin.SetCustomerUserId(this.ycManager.requestManager.GetDeviceKey());
#if UNITY_IOS
                    this._tenjin.RegisterAppForAdNetworkAttribution();
                    this._tenjin.Connect();
                    this._tenjin.GetAttributionInfo(this.DeferredDeeplinkCallback);
                    this._tenjin.SubscribeAppLovinImpressions();
#elif UNITY_ANDROID
                    this._tenjin.Connect();
                    this._tenjin.GetDeeplink(this.DeferredDeeplinkCallback);
                    this._tenjin.SubscribeAppLovinImpressions();
#endif
                }
            }

            private int GetInters() {
                int inters = this.ycManager.dataManager.GetInterstitialsNb();
                if (inters >= 50) { return 7; } // 111
                if (inters >= 25) { return 6; } // 110
                if (inters >= 20) { return 5; } // 101
                if (inters >= 15) { return 4; } // 100
                if (inters >= 10) { return 3; } // 011
                if (inters >= 5) { return 2; } // 010
                if (inters >= 1) { return 1; } // 001
                return 0; // 000
            }

            private int GetRewards() {
                int rewardes = this.ycManager.dataManager.GetRewardedsNb();
                if (rewardes >= 20) { return 56; } // 111000
                if (rewardes >= 15) { return 48; } // 110000
                if (rewardes >= 10) { return 40; } // 101000
                if (rewardes >= 5) { return 32; } // 100000
                if (rewardes >= 3) { return 24; } // 011000
                if (rewardes >= 2) { return 16; } // 010000
                if (rewardes >= 1) { return 8; } // 001000
                return 0; // 000
            }

            protected override void OnDestroy() {
#if UNITY_IOS
                if (this.ycManager.dataManager.GetDiffTimestamp() <= 60 * 60 * 24) {
                    this._tenjin.UpdateConversionValue(this.GetInters() + this.GetRewards());
                }
#endif
            }

            internal override void SendEvent(string eventName) {
                if (this._tenjin) {
                    this._tenjin.SendEvent(eventName);
                }
            }

            internal override void SetConsent(bool consent) {
                if (this._tenjin) {
                    if (consent) {
                        this._tenjin.OptIn();
                    } else {
                        this._tenjin.OptOut();
                    }
                }
            }

            private void OnApplicationPause(bool paused) {
                if (paused == false) {
                    this.Init();
                }
            }

            private void DeferredDeeplinkCallback(Dictionary<string, string> data) {
                this.tenjinData.is_init = true;
                if (data.ContainsKey("advertising_id")) {
                    this.tenjinData.advertising_id = data["advertising_id"];
                }
                if (data.ContainsKey("ad_network")) {
                    this.tenjinData.ad_network = data["ad_network"];
                }
                if (data.ContainsKey("campaign_id")) {
                    this.tenjinData.campaign_id = data["campaign_id"];
                }
                if (data.ContainsKey("campaign_name")) {
                    this.tenjinData.campaign_name = data["campaign_name"];
                }
                if (data.ContainsKey("site_id")) {
                    this.tenjinData.site_id = data["site_id"];
                }
                if (data.ContainsKey("referrer")) {
                    this.tenjinData.referrer = data["referrer"];
                }
                if (data.ContainsKey("deferred_deeplink_url")) {
                    this.tenjinData.deferred_deeplink_url = data["deferred_deeplink_url"];
                }
                if (data.ContainsKey("clicked_tenjin_link")) {
                    this.tenjinData.clicked_tenjin_link = (data["clicked_tenjin_link"].ToLower() == "true");
                }
                if (data.ContainsKey("is_first_session")) {
                    this.tenjinData.is_first_session = (data["is_first_session"].ToLower() == "true");
                }
            }

#if IN_APP_PURCHASING
            internal void SendTenjinPurchaseEvent(Order order) {
                CartItem item = order.CartOrdered.Items().FirstOrDefault();
                if (item == null) return;

                Product product = item.Product;
                double lPrice = decimal.ToDouble(product.metadata.localizedPrice);
                string currencyCode = product.metadata.isoCurrencyCode;
                string productId = product.definition.id;
                IOrderInfo info = order.Info;

#if UNITY_ANDROID
                Dictionary<string, object> wrapper = null;
#if YC_NEWTONSOFT
                wrapper = JsonConvert.DeserializeObject<Dictionary<string, object>>(info.Receipt);
#endif
                if (wrapper == null) return;

                string store   = (string)wrapper["Store"];
                string payload = (string)wrapper["Payload"];

                if (store.Equals("GooglePlay")) {
                    Dictionary<string, object> googleDetails = null;
#if YC_NEWTONSOFT
                    googleDetails = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
#endif
                    if (googleDetails == null) return;
                    string googleJson = (string)googleDetails["json"];
                    string googleSig = (string)googleDetails["signature"];

                    CompletedAndroidPurchase(productId, currencyCode, 1, lPrice, googleJson, googleSig);
                }

                if (store.Equals("AmazonAppStore")) {
                    Dictionary<string, object> amazonDetails = null;
#if YC_NEWTONSOFT
                    amazonDetails = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
#endif
                    if (amazonDetails == null) return;
                    string amazonReceiptId = (string)amazonDetails["receiptId"];
                    string amazonUserId = (string)amazonDetails["userId"];

                    CompletedAmazonPurchase(productId, currencyCode, 1, lPrice, amazonReceiptId, amazonUserId);
                }
#elif UNITY_IOS
                // Try SK2 first (Unity IAP 5.1+ on iOS 15+), fall back to SK1
                string receipt = info.Apple?.jwsRepresentation;
                if (string.IsNullOrEmpty(receipt)) {
                    receipt = info.Apple?.AppReceipt;
                }

                if (string.IsNullOrEmpty(receipt)) {
                    return;
                }

                string transactionId = info.TransactionID;
                CompletedIosPurchase(productId, currencyCode, 1, lPrice, transactionId, receipt);
#endif
            }

            private void CompletedAndroidPurchase(string ProductId, string CurrencyCode, int Quantity, double UnitPrice, string Receipt, string Signature) {
                this._tenjin.Transaction(ProductId, CurrencyCode, Quantity, UnitPrice, null, Receipt, Signature);
            }

            private void CompletedAmazonPurchase(string ProductId, string CurrencyCode, int Quantity, double UnitPrice, string ReceiptId, string UserId) {
                this._tenjin.TransactionAmazon(ProductId, CurrencyCode, Quantity, UnitPrice, ReceiptId, UserId);
            }

            private void CompletedIosPurchase(string ProductId, string CurrencyCode, int Quantity, double UnitPrice, string TransactionId, string Receipt) {
                this._tenjin.Transaction(ProductId, CurrencyCode, Quantity, UnitPrice, TransactionId, Receipt, null);
            }
#endif

        }
    }
}

