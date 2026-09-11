using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

public class AdnProcessor : IPreprocessBuildWithReport {

    private static readonly string[] libraryPaths = {
        "Pods/OMSDK_Voodooio/OMSDK_Voodooio.xcframework",
        "Pods/VoodooAdn/VoodooAdn.xcframework"
    };

    public int callbackOrder => int.MinValue;


    public void OnPreprocessBuild(BuildReport report) {
#if UNITY_IOS
        if (new Version(PlayerSettings.iOS.targetOSVersionString) < new Version("14.0")) {
            PlayerSettings.iOS.targetOSVersionString = "14.0";
        }
#endif
    }

    [PostProcessBuild(int.MaxValue - 1)]
    public static void ChangeXcodePlist(BuildTarget buildTarget, string path) {
        if (buildTarget == BuildTarget.iOS) {
#if UNITY_IOS
            string plistPath = path + "/Info.plist";
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict rootDict = plist.root;

            //SKAdNetworks
            plist.root.values.TryGetValue("SKAdNetworkItems", out PlistElement SKAdNetworkItems);
            if (SKAdNetworkItems == null || SKAdNetworkItems.GetType() != typeof(PlistElementArray)) { // if the array does not exist, create it
                SKAdNetworkItems = plist.root.CreateArray("SKAdNetworkItems");
            }

            bool hasAdnSKAdNetwork = false;
            IEnumerable<PlistElement> SKAdNetworks = SKAdNetworkItems.AsArray().values.Where(plistElement => plistElement.GetType() == typeof(PlistElementDict));
            foreach (PlistElement SKAdNetwork in SKAdNetworks) { // Check if the SKAdNetwork already exists
                PlistElement current;
                SKAdNetwork.AsDict().values.TryGetValue("SKAdNetworkIdentifier", out current);
                if (current != null && current.GetType() == typeof(PlistElementString)) {
                    if (current.AsString() == "tskbem2b5g.skadnetwork") {
                        hasAdnSKAdNetwork = true;
                        break;
                    }
                }
            }

            if (hasAdnSKAdNetwork == false) {
                PlistElementDict ysonetworkSKAd = SKAdNetworkItems.AsArray().AddDict();
                ysonetworkSKAd.SetString("SKAdNetworkIdentifier", "tskbem2b5g.skadnetwork");
            }

            File.WriteAllText(plistPath, plist.WriteToString());
#endif
        }

    }

    [PostProcessBuild(91)] //91 because Applovin runs their embedding script at 90
    public static void EmbedLibrary(BuildTarget buildTarget, string path) {
#if UNITY_IOS
        var projectPath = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        var unityMainTargetGuid = project.GetUnityMainTargetGuid();
        foreach (var libraryPath in libraryPaths) {
            var fileGuid = project.AddFile(libraryPath, libraryPath);
            project.AddFileToEmbedFrameworks(unityMainTargetGuid, fileGuid);
        }
        project.WriteToFile(projectPath);
#endif
    }
}
