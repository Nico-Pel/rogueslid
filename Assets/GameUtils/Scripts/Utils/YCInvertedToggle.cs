using UnityEngine.EventSystems;
using UnityEngine.UI;

public class YCInvertedToggle : Toggle
{
    //Doesn't work with "Fade" parameter

    protected override void Awake() {
        base.Awake();
        this.onValueChanged.AddListener(this.InvertGraphic);
    }

    protected override void Start() {
        base.Start();
        this.InvertGraphic(this.isOn);
    }

#if UNITY_EDITOR
    protected override void OnValidate() {
        base.OnValidate();
        this.InvertGraphic(this.isOn);
    }
#endif

    private void InvertGraphic(bool value) {
        if (this.graphic != null) {
            this.graphic.canvasRenderer.SetAlpha(value ? 0 : 1);
        }
    }
}
