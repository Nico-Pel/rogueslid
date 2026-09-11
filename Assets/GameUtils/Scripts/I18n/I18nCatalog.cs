using System.Collections.Generic;
using UnityEngine;

namespace YsoCorp.GameUtils {

    public sealed class I18nCatalog : ScriptableObject {

        [SerializeField, YcReadOnly] private List<string> languages = new List<string>();

        public IReadOnlyList<string> Languages => this.languages;

        public bool Contains(string language) {
            return this.languages.Contains(language);
        }

#if UNITY_EDITOR
        public void SetLanguages(IEnumerable<string> values) {
            this.languages.Clear();
            this.languages.AddRange(values);
        }
#endif
    }
}
