using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace YsoCorp {
    namespace GameUtils {

        public class YCPackageManager {

            public static string REQUEST_URL_SUFFIX = "games/get-package-url";

            public enum UpdatePackageType {
                Amazon = 1,
                Firebase,
                ProdNetworks,

                RatePopup = 101,
            }

            public static Dictionary<UpdatePackageType, PackageData> PACKAGES = new Dictionary<UpdatePackageType, PackageData>() {
                {UpdatePackageType.Amazon, new PackageData("2.0.0", "AmazonAPS") },
                {UpdatePackageType.Firebase, new PackageData("13.10.0", "Firebase/Analytics") },
                {UpdatePackageType.ProdNetworks, new PackageData("", "ProdNetworks") },
                {UpdatePackageType.RatePopup, new PackageData("", "Packages/RatePopup") },
            };

            public static IEnumerator DownloadPackage(string url, string fileName, Action<bool, string> onDownload = null) {
                if (fileName.EndsWith(".unitypackage") == false) {
                    fileName += ".unitypackage";
                }
                string path = Path.Combine(Application.temporaryCachePath, fileName);
                if (File.Exists(path) == false) {
                    var downloadHandler = new DownloadHandlerFile(path);

                    UnityWebRequest webRequest = new UnityWebRequest(url) {
                        method = UnityWebRequest.kHttpVerbGET,
                        downloadHandler = downloadHandler
                    };

                    var operation = webRequest.SendWebRequest();
                    Debug.Log("Downloading " + fileName);
                    while (!operation.isDone) {
                        yield return new WaitForSeconds(0.1f);
                    }

#if UNITY_2020_1_OR_NEWER
                    if (webRequest.result != UnityWebRequest.Result.Success)
#else
                    if (webRequest.isNetworkError || webRequest.isHttpError)
#endif
                    {
                        Debug.LogError("The file " + fileName + " could not be downloaded.");
                        onDownload?.Invoke(false, null);
                        yield break;
                    }
                }
                onDownload?.Invoke(true, path);
            }

            public static void DownloadAndImportPackage(string url, string fileName, bool interactive, Action<bool, string> onDownload = null) {
                onDownload = ((downloaded, path) => {
                    if (downloaded) {
                        AssetDatabase.ImportPackage(path, interactive);
                    }
                }) + onDownload;
                YCEditorCoroutine.StartCoroutine(DownloadPackage(url, fileName, onDownload));
                    
            }

            public static IEnumerator InstallPackage(string packageName, string version = "", Action onFinished = null, bool forceUpdate = false) {
                var pack = Client.List();
                while (!pack.IsCompleted) yield return null;

                bool isInstalled = pack.Result.FirstOrDefault(q => q.name == packageName) != null;
                UnityEditor.PackageManager.Requests.AddRequest packAdd = null;
                if (!isInstalled || forceUpdate) {
                    if (!string.IsNullOrEmpty(version)) {
                        packageName += "@" + version;
                    }
                    packAdd = Client.Add(packageName);
                }

                while (packAdd != null && !packAdd.IsCompleted) yield return null;
                onFinished?.Invoke();
            }

            public static void InstallPackage(UpdatePackageType packageType, bool force = false) {
                InstallPackage(PACKAGES[packageType], force);
            }

            public static void InstallPackage(PackageData packageData, bool force) {
                YCEditorCoroutine.StartCoroutine(GetDownloadURL(packageData, force, (url) => {
                    string fileName = url.Split("/")[^1];
                    DownloadAndImportPackage(url, fileName, false);
                }));
            }

            private static IEnumerator GetDownloadURL(PackageData packageData, bool force, Action<string> onComplete) {
                string requestUrl = RequestManager.GetUrlEmptyStatic(REQUEST_URL_SUFFIX, true);
                requestUrl += $"?path={packageData.folderPath}/&version={packageData.overrideLatestVersion}&force={force}";

                using UnityWebRequest req = UnityWebRequest.Get(requestUrl);
                var op = req.SendWebRequest();
                while (!op.isDone)
                    yield return null;

                if (req.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("An error has occured when trying to find the download URL");
                    yield break;
                }

                string json = req.downloadHandler.text;
                string url = "";
#if YC_NEWTONSOFT
                DownloadUrlResponse response = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadUrlResponse>(json);
                url = response.data;
#endif
                if (!string.IsNullOrEmpty(url)) {
                    onComplete?.Invoke(url);
                }
            }

            #region Structures

            public struct PackageData {
                public Version overrideLatestVersion;
                public string folderPath;

                public PackageData(string version, string folderPath) {
                    if (string.IsNullOrEmpty(version)) {
                        this.overrideLatestVersion = new Version();
                    } else {
                        this.overrideLatestVersion = new Version(version);
                    }
                    this.folderPath = folderPath;
                }
            }

            public struct DownloadUrlResponse {
                public string data;
                public long date;
            }

            #endregion
        }
    }
}