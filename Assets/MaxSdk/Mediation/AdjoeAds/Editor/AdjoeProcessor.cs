using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEditor.Android;
using System.Collections.Generic;
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Linq;
using UnityEngine;
using UnityEditor.iOS.Xcode.Extensions;

namespace YsoCorp {

    namespace GameUtils {
        public class AdjoeProcessor {

            private static readonly string[] libraryPaths = {
                "Pods/AdjoeWaveSDK/AdjoeWaveSDK.xcframework"
            };

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

    }

}