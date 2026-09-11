using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Linq;

#if IN_APP_PURCHASING
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine.Purchasing;
#endif

namespace YsoCorp {
    namespace GameUtils {

        [DefaultExecutionOrder(-15)]
        public sealed class InAppManager : BaseManager {

#if IN_APP_PURCHASING
            private Dictionary<string, UnityEvent> _onPurchased = new Dictionary<string, UnityEvent>();
            private Dictionary<string, UnityEvent<PurchaseFailureDescription>> _onPurchaseFailed = new Dictionary<string, UnityEvent<PurchaseFailureDescription>>();
            private List<string> _missingKeyPurchases = new List<string>();

            [HideInInspector, Obsolete("Please use OnIAPInitHandling instead to handle both pre-init and post-init", true)] public UnityEvent OnIAPInit = new UnityEvent(); //2.8.0
            private event Action<bool> _onIAPInitTried;

            private StoreController _storeController = null;
            private InitState _initState = InitState.NotInit;

            private enum InitState {
                NotInit = 1,
                InitSuccessful = 2,
                InitFailed = 3
            }
#endif

            private void Awake() {
                this.ycManager.inAppManager = this;
            }

#if IN_APP_PURCHASING
            async void Start() {
                this.Init();
            }

            private async void Init() {
                if (this.IsInitialized()) {
                    return;
                }

                this._storeController = UnityIAPServices.StoreController();
                this._storeController.ProcessPendingOrdersOnPurchasesFetched(true);
                this._storeController.OnPurchasePending += this.ProcessPurchase;
                this._storeController.OnPurchaseFailed += this.OnPurchaseFailed;
                try {
                    await this._storeController.Connect();
                } catch (Exception exception) {
                    string error = $"Connection to store failed with error: {exception.Message}. Inner Exception : {exception.InnerException}";
                    this.OnInitializeFailed(error);
                }

                this._storeController.OnProductsFetchFailed += OnProductsFetchFailed;
                this._storeController.OnProductsFetched += OnProductsFetched;
                this._storeController.OnPurchasesFetchFailed += this.OnPurchasesFetchedFailed;
                this._storeController.OnPurchasesFetched += this.OnPurchasesFetched;
                this._storeController.FetchProducts(GetProductsList());
            }

            private List<ProductDefinition> GetProductsList() {
                List<ProductDefinition> productsList = new List<ProductDefinition>();
                if (this.ycManager.ycConfig.InAppRemoveAds != "") {
                    if (this.ycManager.ycConfig.InAppRemoveAds.Any(char.IsUpper)) {
                        this.DebugLogError($"IAP key {this.ycManager.ycConfig.InAppRemoveAds} must be all lower case. Skipping its init");
                    } else {
                        productsList.Add(new ProductDefinition(this.ycManager.ycConfig.InAppRemoveAds, ProductType.NonConsumable));
                    }
                }
                foreach (CustomInapp inapp in this.ycManager.ycConfig.CustomInapps) {
                    if (inapp.inappKey.Any(char.IsUpper)) {
                        this.DebugLogError($"IAP key {this.ycManager.ycConfig.InAppRemoveAds} must be all lower case. Skipping its init");
                    } else {
                        ProductType type = inapp.isConsumable ? ProductType.Consumable : ProductType.NonConsumable;
                        productsList.Add(new ProductDefinition(inapp.inappKey, type));
                    }
                }
                return productsList;
            }

            private void OnProductsFetchFailed(ProductFetchFailed productsFectchFailed) {
                if (this.GetProductsList().Count == productsFectchFailed.FailedFetchProducts.Count) {
                    this.OnInitializeFailed("Could not fetch any product.");
                } else {
                    this.OnInitialized();
                }
                string error = $"Fetching IAP products failed with error : {productsFectchFailed.FailureReason}.";
                string failedProductsError = "";
                foreach (ProductDefinition product in productsFectchFailed.FailedFetchProducts) {
                    if (failedProductsError != "") {
                        failedProductsError += ", ";
                    }
                    failedProductsError += product.id;
                }
                if (failedProductsError != "") {
                    error += " The following products could not be fetched: " + failedProductsError;
                }
                this.DebugLogError(error);
            }

            private void OnProductsFetched(List<Product> products) {
                this.OnInitialized();
                this._storeController.FetchPurchases();
            }

            private void OnPurchasesFetchedFailed(PurchasesFetchFailureDescription error) {
                DebugLogError($"PurchasesFetchedFailed failed with the error : {error.FailureReason}");
            }

            private void OnPurchasesFetched(Orders orders) {
#if UNITY_ANDROID
                foreach (var order in orders.ConfirmedOrders) {
                    string productId = order.CartOrdered.Items().FirstOrDefault().Product.definition.id;
                    this.TryGiveReward(productId);
                }
#endif
            }

            public bool IsInitialized() {
                return this._storeController != null && this._initState == InitState.InitSuccessful;
                //return this._StoreController != null && this._StoreExtensionProvider != null;
            }

            public Product GetProductById(string productId) {
                if (this.IsInitialized()) {
                    return this._storeController.GetProducts().FirstOrDefault(p => p.definition.id == productId);
                }
                return null;
            }
#endif
            /// <summary>
            /// Get the price of an IAP product as a string, including the currency symbol.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <returns>Returns the price of the product as a string</returns>
            public string GetProductPrice(string productId) {
#if IN_APP_PURCHASING
                Product p = this.GetProductById(productId);
                if (p != null) {
                    return p.metadata.localizedPriceString;
                }
#endif
                return "";
            }

#if IN_APP_PURCHASING
            [Obsolete("Obsolete since v1.48.0 and will be removed. Please use AddListener without the bool parameter or AddListenerOnFailed", true)]
            public void AddListener(string productId, UnityAction onPurchase, bool purchaseSucceeded) {
                if (purchaseSucceeded) {
                    this.AddListener(productId, onPurchase);
                } else {
                    this.AddListenerOnFailed(productId, (failure) => onPurchase?.Invoke());
                }
            }

            /// <summary>
            /// Adds an action to a product ID to be executed when the purchase is completed.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <param name="onPurchase">The action to be executed</param>
            public void AddListener(string productId, UnityAction onPurchase) {
                if (this.IsProductIdValid(productId)) {
                    if (this._onPurchased.ContainsKey(productId) == false) {
                        this._onPurchased[productId] = new UnityEvent();
                    }
                    this._onPurchased[productId].AddListener(onPurchase);
                }

                if (this._missingKeyPurchases.Contains(productId)) {
                    this._onPurchased[productId]?.Invoke();
                    this._missingKeyPurchases.Remove(productId);
                }
            }

            /// <summary>
            /// Adds an action to a product ID to be executed when the purchase failed.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <param name="onPurchaseFailed">The action to be executed</param>
            public void AddListenerOnFailed(string productId, UnityAction<PurchaseFailureDescription> onPurchaseFailed) {
                if (this.IsProductIdValid(productId)) {
                    if (this._onPurchaseFailed.ContainsKey(productId) == false) {
                        this._onPurchaseFailed[productId] = new UnityEvent<PurchaseFailureDescription>();
                    }
                    this._onPurchaseFailed[productId].AddListener(onPurchaseFailed);
                }
            }

            [Obsolete("Obsolete since v1.48.0 and will be removed. Please use RemoveListener without the bool parameter or RemoveListenerOnFailed", true)]
            public void RemoveListener(string productId, UnityAction onPurchase, bool purchaseSucceeded) {
                if (purchaseSucceeded) {
                    this.RemoveListener(productId, onPurchase);
                } else {
                    this.RemoveListenerOnFailed(productId, (failure) => onPurchase?.Invoke());
                }
            }

            /// <summary>
            /// Removes an action previously added with AddListener to a product ID.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <param name="onPurchase">The action to be removed</param>
            public void RemoveListener(string productId, UnityAction onPurchase) {
                if (this.HasListener(productId, true) == false) return;

                UnityEvent eve = this._onPurchased[productId];
                eve?.RemoveListener(onPurchase);
            }

            /// <summary>
            /// Removes an action previously added with AddListenerOnFailed to a product ID.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <param name="onPurchaseFailed">The action to be removed</param>
            public void RemoveListenerOnFailed(string productId, UnityAction<PurchaseFailureDescription> onPurchaseFailed) {
                if (this.HasListener(productId, false) == false) return;

                UnityEvent<PurchaseFailureDescription> eve = this._onPurchaseFailed[productId];
                eve?.RemoveListener(onPurchaseFailed);
            }

            [Obsolete("Obsolete since v1.48.0 and will be removed. Please use RemoveListener without the bool parameter or RemoveListenerOnFailed", true)]
            public void RemoveAllListener(string productId, bool purchaseSucceeded) {
                if (purchaseSucceeded) {
                    this.RemoveAllListener(productId);
                } else {
                    this.RemoveAllListenersOnFailed(productId);
                }
            }

            /// <summary>
            /// Removes all actions previously added with AddListener to a product ID. 
            /// </summary>
            /// <param name="productId">The product ID</param>
            public void RemoveAllListener(string productId) {
                if (this.HasListener(productId, true) == false) return;
                this._onPurchased[productId].RemoveAllListeners();
            }

            /// <summary>
            /// Removes all actions previously added with AddListenerOnFailed to a product ID.
            /// </summary>
            /// <param name="productId">The product ID</param>
            public void RemoveAllListenersOnFailed(string productId) {
                if (this.HasListener(productId, false) == false) return;
                this._onPurchaseFailed[productId].RemoveAllListeners();
            }

            /// <summary>
            /// Checks if any action has been previously added via AddListener or AddListenerOnFailed. 
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <param name="purchaseSucceeded">Wheteher to check for actions from AddListener or AddListenerOnFailed</param>
            /// <returns>Returns true if any action has been previously added via AddListener or AddListenerOnFailed</returns>
            public bool HasListener(string productId, bool purchaseSucceeded = true) {
                if (this.IsProductIdValid(productId)) {
                    if (purchaseSucceeded) {
                        return this._onPurchased.ContainsKey(productId);
                    } else {
                        return this._onPurchaseFailed.ContainsKey(productId);
                    }
                }
                return false;
            }
#endif

            /// <summary>
            /// Displays the store popup to confirm the purchase.
            /// </summary>
            /// <param name="productId">The product ID</param>
            public void BuyProductID(string productId) {
#if IN_APP_PURCHASING
                if (this.IsInitialized()) {
                    Product product = this.GetProductById(productId);
                    if (product != null && product.availableToPurchase) {
                        this.DebugLog(string.Format($"Purchasing product asychronously: '{product.definition.id}'"));
                        this._storeController.PurchaseProduct(product);
                    } else {
                        this.DebugLogError("Not purchasing product, either is not found or is not available for purchase");
                    }
                } else {
                    this.DebugLogError("Purchase failed, product not initialized.");
                }
#endif
            }

            /// <summary>
            /// Restores the purchases previously made by a user.
            /// </summary>
            public void RestorePurchases() {
#if IN_APP_PURCHASING
                if (!this.IsInitialized()) {
                    this.DebugLogError("RestorePurchases FAIL. Not initialized.");
                    return;
                }

                this._storeController.RestoreTransactions((result, error) => {
                    if (result) {
                        this.DebugLog("RestorePurchases continuing. If no further messages, no purchases available to restore.");
                    } else {
                        this.DebugLog("RestorePurchases failed: " + error);
                    }
                });
#endif
            }

#if IN_APP_PURCHASING
            public void OnInitialized() {
                this.DebugLog("Initialized");
                this._initState = InitState.InitSuccessful;
                this._onIAPInitTried?.Invoke(true);
            }

            public void OnInitializeFailed(string message) {
                this.DebugLogError(message);
                this._initState = InitState.InitFailed;
                this._onIAPInitTried?.Invoke(false);
            }

            /// <summary>
            /// Handles both pre-init (subscribing to an OnInit event) and post-init (directly executing the given action)
            /// </summary>
            /// <param name="action">The action to execute in either case. The bool parameter is if the init was successful or not.</param>
            /// <param name="executeIfAlreadyInit">Should the action execute if the init is already done.</param>
            public void OnIAPInitHandling(Action<bool> action, bool executeIfAlreadyInit = true) {
                if (this._initState == InitState.NotInit) {
                    this._onIAPInitTried += action;
                } else if (executeIfAlreadyInit) {
                    action?.Invoke(this._initState == InitState.InitSuccessful);
                }
            }

            public void UnsubscribeToOnIAPInit(Action<bool> action) {
                this._onIAPInitTried -= action;
            }

            public void ProcessPurchase(PendingOrder pendingOrder) {
                CartItem item = pendingOrder.CartOrdered.Items().FirstOrDefault();
                if (item == null) {
                    this.DebugLogError("First item in cart is null");
                    return;
                }

                Product product = item.Product;
                string productId = product.definition.id;
                string isoCurrencyCode = product.metadata.isoCurrencyCode;
                decimal price = product.metadata.localizedPrice;

                this.ycManager.analyticsManager.InAppBought(productId, (float)price, isoCurrencyCode);

#if !UNITY_EDITOR
                if (this.ycManager.mmpManager.IsInit()) {
                    this.ycManager.mmpManager.tenjinManager.SendTenjinPurchaseEvent(pendingOrder);
                }
#endif

                this.TryGiveReward(productId);

                this._storeController.ConfirmPurchase(pendingOrder);
            }

            public void OnPurchaseFailed(FailedOrder failedOrder) {
                this.DebugLogError($"OnPurchaseFailed: Product: '{failedOrder.CartOrdered.Items()[0].Product.definition.id}', PurchaseFailureReason: {failedOrder.FailureReason}");
#if UNITY_ANDROID
                if (failedOrder.FailureReason == PurchaseFailureReason.DuplicateTransaction) {
                    string productId = failedOrder.CartOrdered.Items().FirstOrDefault().Product.definition.id;
                    this.TryGiveReward(productId);
                }
#endif
            }
#endif

#if IN_APP_PURCHASING
            private void TryGiveReward(string productId) {
                if (this._onPurchased.ContainsKey(productId)) {
                    this._onPurchased[productId]?.Invoke();
                } else {
                    this.DebugLogError($"The given key {productId} does not have reward to give. It will be automatically given if added later.");
                    this._missingKeyPurchases.Add(productId);
                }
            }
#endif

            /// <summary>
            /// Displays the store popup to confirm the purchase of the "No Ads" product.
            /// </summary>
            public void BuyProductIDAdsRemove() {
                this.BuyProductID(this.ycManager.ycConfig.InAppRemoveAds);
            }

            /// <summary>
            /// Checks if the product ID is included in the YCConfigData file.
            /// </summary>
            /// <param name="productId">The product ID</param>
            /// <returns>Returns true if the product ID is included in the YCConfigData file</returns>
            public bool IsProductIdValid(string productId) {
#if IN_APP_PURCHASING
                if (productId.CompareTo(this.ycManager.ycConfig.InAppRemoveAds) == 0) {
                    return true;
                }
                foreach (CustomInapp inapp in this.ycManager.ycConfig.CustomInapps) {
                    if (inapp.inappKey.CompareTo(productId) == 0) {
                        return true;
                    }
                }
                this.DebugLogError("The InApp key: " + productId + " does not exist in the YCConfig list");
                return false;
#else
                return true;
#endif
            }

            private void DebugLog(object message) {
                if (this.ycManager.ycConfig.InappDebug) {
                    Debug.Log("[GameUtils - Inapps] " + message);
                }
            }
            private void DebugLogError(object message) {
                Debug.LogError("[GameUtils - Inapps] " + message);
            }

            [Serializable]
            public struct CustomInapp {
                public string inappKey;
                public bool isConsumable;

                public CustomInapp(string inappKey, bool isConsumable) {
                    this.inappKey = inappKey;
                    this.isConsumable = isConsumable;
                }
            }

        }
    }
}
