using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YsoCorp {
    namespace GameUtils {

        public class YCCustomWindow : EditorWindow {

            public enum WindowPosition {
                UpperLeft,
                UpperCenter,
                UpperRight,
                MiddleLeft,
                MiddleCenter,
                MiddleRight,
                LowerLeft,
                LowerCenter,
                LowerRight
            }

            public virtual void OnGUI() {
                this.ShortcutHandling();
            }

            private Rect GetMainWindowRect() {
#if UNITY_2020_1_OR_NEWER
                return EditorGUIUtility.GetMainWindowPosition();
#else
                return new Rect(0, 0, 1920, 1080);
#endif
            }

            #region Size / Position
            public void SetMinSize(float width, float height) {
                width = Mathf.Min(width, this.GetMainWindowRect().width * 0.9f);
                height = Mathf.Min(height, this.GetMainWindowRect().height * 0.9f);
                this.minSize = new Vector2(width, height);
            }

            public void SetMaxSize(float width, float height) {
                width = Mathf.Min(width, this.GetMainWindowRect().width * 0.9f);
                height = Mathf.Min(height, this.GetMainWindowRect().height * 0.9f);
                this.maxSize = new Vector2(width, height);
            }

            public void SetSize(float width, float height) {
                this.SetMinSize(width, height);
                this.SetMaxSize(width, height);
            }

            public void SetPosition(int posX, int posY) {
                Rect r = this.position;
                r.x = posX;
                r.y = posY;
                this.position = r;
            }

            public void SetPosition(WindowPosition windowPosition) {
                Rect r = this.position;
                float centerX = this.GetMainWindowRect().center.x - this.position.width / 2;
                float centerY = this.GetMainWindowRect().center.y - this.position.height / 2;
                float rightX = this.GetMainWindowRect().width - this.position.width;
                float downY = this.GetMainWindowRect().height - this.position.height;
                switch (windowPosition) {
                    case WindowPosition.UpperLeft:
                        r.x = 0;
                        r.y = 0;
                        break;

                    case WindowPosition.UpperCenter:
                        r.x = centerX;
                        r.y = 0;
                        break;

                    case WindowPosition.UpperRight:
                        r.x = rightX;
                        r.y = 0;
                        break;

                    case WindowPosition.MiddleLeft:
                        r.x = 0;
                        r.y = centerY;
                        break;

                    case WindowPosition.MiddleCenter:
                        r.x = centerX;
                        r.y = centerY;
                        break;

                    case WindowPosition.MiddleRight:
                        r.x = rightX;
                        r.y = centerY;
                        break;

                    case WindowPosition.LowerLeft:
                        r.x = 0;
                        r.y = downY;
                        break;

                    case WindowPosition.LowerCenter:
                        r.x = centerX;
                        r.y = downY;
                        break;

                    case WindowPosition.LowerRight:
                        r.x = rightX;
                        r.y = downY;
                        break;
                }
                this.position = r;
            }
            #endregion

            protected virtual void ShortcutHandling() {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.S && (e.control || e.command)) { // Ctrl + S 
                    this.SaveChanges();
                }
            }

            protected void AddButtonValidation(GUIContent text, Action action) {
                if (GUILayout.Button(text) || (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))) {
                    action();
                }
            }
            protected void AddButtonValidation(string text, Action action) {
                this.AddButtonValidation(new GUIContent(text), action);
            }

            protected void AddButtonClose(GUIContent text = null, Action closeAction = null) {
                if (text == null) {
                    text = new GUIContent("Close");
                }
                if (GUILayout.Button(text) || (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Escape))) {
                    closeAction?.Invoke();
                    this.Close();
                }
            }
            protected void AddButtonClose(string text = "", Action closeAction = null) {
                if (text == "") {
                    GUIContent c = null;
                    this.AddButtonClose(c, closeAction);
                } else {
                    this.AddButtonClose(new GUIContent(text), closeAction);
                }
            }

            protected void AddCancelOk(GUIContent cancel, GUIContent ok, Action cancelAction, Action okAction) {
                GUILayout.BeginHorizontal();
                this.AddButtonClose(cancel, cancelAction);
                this.AddButtonValidation(ok, okAction);
                GUILayout.EndHorizontal();
            }
            protected void AddCancelOk(string cancel, string ok, Action cancelAction, Action okAction) {
                this.AddCancelOk(new GUIContent(cancel), new GUIContent(ok), cancelAction, okAction);
            }
        }
    }
}
