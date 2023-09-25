using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Keyer.Editor
{
    [Serializable]
    class PolygonEditor : EditorWindow
    {
        readonly struct HandlesMatrixOverride : IDisposable
        {
            readonly Matrix4x4 m_PrevTransform;

            public HandlesMatrixOverride(Matrix4x4 transform)
            {
                m_PrevTransform = Handles.matrix;
                Handles.matrix = transform;
            }

            public void Dispose() => Handles.matrix = m_PrevTransform;
        }

        static class Content
        {
            public static readonly GUIContent Title = new("2D Garbage Mask Editor");
            public static readonly GUIContent Instance = new("Keyer Instance");
            public static readonly string ErrorNoKeyer = "Select a Keyer instance to edit its Garbage Mask.";
            public static readonly string ErrorNoSettings = "The selected Keyer instance has no Settings assigned.";
            public static readonly string ErrorNoGenerator = "To edit the Settings of the selected Keyer instance, activate the procedural Garbage Mask generator. ";
#if DEBUG_ADVANCED
            public static readonly GUIContent RenderDocCapture = new("RenderDoc Capture");
#endif
        }

        static readonly MethodInfo k_GetHelpIcon = typeof(EditorGUIUtility).GetMethod("GetHelpIcon", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly StringBuilder s_StringBuilder = new();
        static readonly GUILayoutOption[] s_GUILayoutOptions = new GUILayoutOption[1];

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            if (k_GetHelpIcon == null)
            {
                throw new MissingMemberException(typeof(EditorGUIUtility).FullName, "GetHelpIcon");
            }
        }

        static Texture2D GetHelpIcon(MessageType type)
        {
            if (k_GetHelpIcon != null)
            {
                return (Texture2D)k_GetHelpIcon.Invoke(null, new object[] { type });
            }

            return null;
        }

        [SerializeField]
        int m_KeyerInstanceId;

        Keyer m_Keyer;
        PolygonInfo m_PolygonInfo;
        int m_ControlId;
        Rect m_PolygonRect;
        Matrix4x4 m_HandlesTransform;
        Matrix4x4 m_ImageTransform;

        readonly HandlesAndSerializedObjectsCache m_Cache = new();
        readonly GUIContent m_HelpGuiContent = new();

#if DEBUG_ADVANCED
        bool m_PendingCapture;
#endif

        public Keyer Keyer
        {
            set
            {
                m_Keyer = value;
                m_KeyerInstanceId = m_Keyer.GetInstanceID();
            }
        }

        [MenuItem("Window/Virtual Production/2D Garbage Mask Editor")]
        public static void ShowWindow()
        {
            GetWindow<PolygonEditor>().Show();
        }

        void OnEnable()
        {
            titleContent = Content.Title;
            wantsMouseMove = true;
            wantsLessLayoutEvents = true;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.playModeStateChanged += OnPlaymodeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            m_Cache.Clear();
        }

        void OnPlaymodeChanged(PlayModeStateChange state)
        {
            if (m_KeyerInstanceId != 0)
            {
                m_Keyer = EditorUtility.InstanceIDToObject(m_KeyerInstanceId) as Keyer;
                Repaint();
            }
        }

        void OnGUI()
        {
#if DEBUG_ADVANCED
            if (GUILayout.Button(Content.RenderDocCapture))
            {
                m_PendingCapture = true;
                UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(this);
            }

            var useCapture = m_PendingCapture && Event.current.type == EventType.Repaint;
            if (useCapture)
            {
                m_PendingCapture = false;
            }
#endif
            var newKeyer = KeyerSelector.Popup(m_Keyer);
            if (newKeyer != m_Keyer)
            {
                m_Keyer = newKeyer;
                m_KeyerInstanceId = m_Keyer == null ? 0 : m_Keyer.GetInstanceID();
            }

            if (CanEditPolygon(m_Keyer, out var errorMessage))
            {
                DoPolygonEditor(GetAspect(m_Keyer.Foreground), m_Keyer.Foreground);
            }
            else
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }

#if DEBUG_ADVANCED
            if (useCapture)
            {
                UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(this);
            }
#endif
        }

        void OnUndoRedoPerformed()
        {
            // We cannot discriminate which keyer did change.
            if (m_Keyer != null)
            {
                m_Keyer.RequestGarbageMaskGeneration();
            }
        }

        void DoPolygonEditor(float aspect, Texture background)
        {
            // Note: do not move the statement below inside the if.
            var polygonRect = GUILayoutUtility.GetAspectRect(aspect);
            if (Event.current.type == EventType.Repaint)
            {
                m_PolygonRect = polygonRect;
                m_PolygonRect = new RectOffset(-5, -5, -5, -5).Add(m_PolygonRect);
                GetHandlesTransform(m_PolygonRect, out m_HandlesTransform, out m_ImageTransform);
                GUI.DrawTexture(m_PolygonRect, background);
            }

            DoPolygonHandles();
            DoHelpGui(m_Cache.GetHandles(m_Keyer));
        }

        void DoHelpGui(PolygonHandles polygonHandles)
        {
            s_StringBuilder.Clear();
            s_StringBuilder.AppendJoin('\n', polygonHandles.GetContextualInfo());

            if (s_StringBuilder.Length == 0)
            {
                return;
            }

            m_HelpGuiContent.text = s_StringBuilder.ToString();
            m_HelpGuiContent.image = GetHelpIcon(MessageType.Info);

            s_GUILayoutOptions[0] = GUILayout.MaxWidth(m_PolygonRect.width);
            var rect = GUILayoutUtility.GetRect(m_HelpGuiContent, EditorStyles.helpBox, s_GUILayoutOptions);
            rect.y = m_PolygonRect.yMax + 5;

            GUI.Label(rect, m_HelpGuiContent, EditorStyles.miniTextField);
        }

        void DoPolygonHandles()
        {
            using var handlesCameraScope = PolygonUtility.GetHandlesCameraScope(false, m_HandlesTransform);
            using var handlesMatrixOverride = new HandlesMatrixOverride(m_HandlesTransform);

            switch (Event.current.type)
            {
                case EventType.Repaint:
                {
                    // Note that we do not store this polygon info.
                    if (m_Cache.TryGetPointsSerializedProperty(m_Keyer, out var pointsProperty))
                    {
                        m_PolygonInfo = CreatePolygonInfo(m_Keyer, pointsProperty, m_HandlesTransform, m_ImageTransform);
                        PolygonUtility.Draw(m_PolygonInfo);
                    }
                }
                break;
                case EventType.Layout:
                {
                    // Register polygon.
                    if (m_Cache.TryGetPointsSerializedProperty(m_Keyer, out var pointsProperty))
                    {
                        m_ControlId = GUIUtility.GetControlID(FocusType.Keyboard);
                        m_PolygonInfo = CreatePolygonInfo(m_Keyer, pointsProperty, m_HandlesTransform, m_ImageTransform);
                        var dist = PolygonUtility.DistanceToPolygon(m_PolygonInfo);
                        HandleUtility.AddControl(m_ControlId, dist);
                    }
                }
                break;
            }

            // Actual polygon editor. Shared with Editor Tool. See PolygonTool.
            if (m_PolygonInfo.IsValid)
            {
                var handles = m_Cache.GetHandles(m_Keyer);
                if (handles.Do(Event.current, m_ControlId, m_PolygonRect, m_PolygonInfo))
                {
                    GUI.FocusControl(String.Empty);
                    Event.current.Use();
                }
            }
        }

        static bool CanEditPolygon(Keyer keyer, out string errorMessage)
        {
            if (keyer == null)
            {
                errorMessage = Content.ErrorNoKeyer;
                return false;
            }

            if (keyer.Settings == null)
            {
                errorMessage = Content.ErrorNoSettings;
                return false;
            }

            if (keyer.Settings.GarbageMask.Mode != GarbageMaskMode.Polygon)
            {
                errorMessage = Content.ErrorNoGenerator;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static void GetHandlesTransform(Rect guiRect, out Matrix4x4 handlesMatrix, out Matrix4x4 imageTransform)
        {
            {
                // Uniform scale is important otherwise handles will appear skewed.
                var translate = new Vector3(guiRect.x, guiRect.y, 0);
                var uniformScale = Mathf.Max(guiRect.width, guiRect.height);
                handlesMatrix = Matrix4x4.TRS(translate, Quaternion.identity, Vector3.one * uniformScale);
            }

            {
                var translate = Vector3.zero;
                var scale = Vector3.one;
                var aspect = guiRect.width / guiRect.height;
                if (aspect > 1)
                {
                    translate.x = 0;
                    translate.y = 1 / aspect;
                    scale.x = 1;
                    scale.y = -1 / aspect;
                }
                else
                {
                    translate.x = 0;
                    translate.y = 1;
                    scale.x = aspect;
                    scale.y = -1;
                }

                imageTransform = Matrix4x4.TRS(translate, Quaternion.identity, scale);
            }
        }

        static PolygonInfo CreatePolygonInfo(Keyer keyer, SerializedProperty pointsProperty, Matrix4x4 handlesMatrix, Matrix4x4 imageTransform)
        {
            var editScopeFactory = new PolygonEditScopeFactory(keyer, pointsProperty);
            return new(pointsProperty, editScopeFactory, handlesMatrix, imageTransform, Vector3.forward, Vector3.zero);
        }

        static float GetAspect(Texture texture) => texture.width / (float)texture.height;
    }
}
