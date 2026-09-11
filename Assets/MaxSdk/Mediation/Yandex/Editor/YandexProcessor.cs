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
        public class YandexProcessor {

            // Must be between 40 and 50 to ensure that it's not overriden by Podfile generation (40) and
            // that it's added before "pod install" (50).
            [PostProcessBuildAttribute(45)]
            private static void PostProcessBuild_iOS(BuildTarget target, string buildPath)
            {
                if (target == BuildTarget.iOS)
                {
                    using (StreamWriter sw = File.AppendText(buildPath + "/Podfile"))
                    {
                        sw.WriteLine("post_install do |installer|\n problematic_targets = ['AppMetricaLibraryAdapter']\n installer.pods_project.targets.each do |target|\n  target.build_configurations.each do |config|\n   if problematic_targets.include?(target.name)\n    config.build_settings['BUILD_LIBRARY_FOR_DISTRIBUTION'] = 'NO'\n   end\n  end\n end\nend");
                    }
                }
            }
        }

    }

}