using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YsoCorp.GameUtils {

    internal sealed class I18nCatalogPostprocessor : AssetPostprocessor {

        private const string CatalogPath = "Assets/GameUtils/ScriptableObjects/I18nCatalog.asset";
        private const string I18nFolder = "/Resources/I18n/";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        ) {
            if (!importedAssets.Contains(CatalogPath) &&
                !HasTranslation(importedAssets) && !HasTranslation(deletedAssets) &&
                !HasTranslation(movedAssets) && !HasTranslation(movedFromAssetPaths)) {
                return;
            }

            ScheduleRebuild();
        }

        private static void ScheduleRebuild() {
            EditorApplication.delayCall -= Rebuild;
            EditorApplication.delayCall += Rebuild;
        }

        private static bool HasTranslation(string[] paths) {
            return paths.Any(IsTranslation);
        }

        private static bool IsTranslation(string path) {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf(I18nFolder, StringComparison.OrdinalIgnoreCase) >= 0 &&
                   normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Rebuild() {
            string[] languages = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsTranslation)
                .Select(Path.GetFileNameWithoutExtension)
                .Distinct()
                .OrderBy(language => language)
                .ToArray();

            I18nCatalog catalog = AssetDatabase.LoadAssetAtPath<I18nCatalog>(CatalogPath);
            if (catalog != null && catalog.Languages.SequenceEqual(languages)) {
                return;
            }

            if (catalog == null) {
                catalog = ScriptableObject.CreateInstance<I18nCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetLanguages(languages);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }
    }
}
