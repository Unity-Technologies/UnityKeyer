using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Editor
{
    // We temporarily disable the editor tool and solely edit the polygon via the custom window.
    // This is due to the mapping geometry in use, which may not be a quad.
    // [EditorTool("Garbage Mask 2d Tool", typeof(Keyer))]
    class PolygonTool : EditorTool, IDrawSelectedHandles
    {
        static class Content
        {
            public static readonly GUIContent EnterTool = new("Entering Polygon Tool");
            public static readonly GUIContent ExitTool = new("Exiting Polygon Tool");
        }

        readonly struct ActiveControl
        {
            public readonly Keyer Keyer;
            public readonly PolygonInfo PolygonInfo;

            public ActiveControl(Keyer keyer, PolygonInfo polygonInfo)
            {
                Keyer = keyer;
                PolygonInfo = polygonInfo;
            }
        }

        // Stores all required data of currently selected polygons.
        readonly Dictionary<int, ActiveControl> m_ActiveControls = new();
        readonly HandlesAndSerializedObjectsCache m_Cache = new();
        readonly StringBuilder m_StringBuilder = new();

        void OnDisable()
        {
            m_ActiveControls.Clear();
            m_Cache.Clear();
        }

        // TODO Check shortcut. Conflict likely?
        [Shortcut("Activate Polygon Tool", typeof(SceneView), KeyCode.P)]
        static void PolygonToolShortcut()
        {
            // We activate the tool if one or more of the selected keyers is using a generated mask.
            foreach (var selected in Selection.GetFiltered<Keyer>(SelectionMode.TopLevel))
            {
                if (selected.Settings != null && selected.Settings.GarbageMask.Mode == GarbageMaskMode.Polygon)
                {
                    ToolManager.SetActiveTool<PolygonTool>();
                    return;
                }
            }
        }

        public override void OnActivated()
        {
            SceneView.lastActiveSceneView.ShowNotification(Content.EnterTool, .1f);
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        public override void OnWillBeDeactivated()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            SceneView.lastActiveSceneView.ShowNotification(Content.ExitTool, .1f);
        }

        void OnUndoRedoPerformed()
        {
            // We cannot discriminate which keyer did change.
            foreach (var (_, activeControl) in m_ActiveControls)
            {
                activeControl.Keyer.RequestGarbageMaskGeneration();
            }
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView)
            {
                return;
            }

            if (Event.current.type == EventType.Layout)
            {
                RegisterActiveControls(targets);
            }

            var useEvent = false;
            foreach (var pair in m_ActiveControls)
            {
                var controlId = pair.Key;
                var info = pair.Value.PolygonInfo;
                var keyer = pair.Value.Keyer;
                var handles = m_Cache.GetHandles(keyer);
                DoPolygonHandleGui(handles);
                useEvent |= handles.Do(Event.current, controlId, window.position, info);
            }

            if (useEvent)
            {
                Event.current.Use();
            }
        }

        void DoPolygonHandleGui(PolygonHandles polygonHandles)
        {
            m_StringBuilder.Clear();
            m_StringBuilder.AppendJoin('\n', polygonHandles.GetContextualInfo());

            if (m_StringBuilder.Length == 0)
            {
                return;
            }

            Handles.BeginGUI();
            EditorGUILayout.HelpBox(m_StringBuilder.ToString(), MessageType.Info);
            Handles.EndGUI();
        }

        // Draw selected polygon gizmos. Alternative to putting gizmos directly in MonoBehaviours.
        public void OnDrawHandles()
        {
            foreach (var obj in targets)
            {
                if (obj is Keyer keyer &&
                    keyer.Settings != null &&
                    keyer.Settings.GarbageMask.Mode == GarbageMaskMode.Polygon &&
                    keyer.Settings.GarbageMask.Points != null &&
                    m_Cache.TryGetPointsSerializedProperty(keyer, out var pointsProperty))
                {
                    var editScopeFactory = new PolygonEditScopeFactory(keyer, pointsProperty);
                    PolygonUtility.Draw(new PolygonInfo(pointsProperty, editScopeFactory, keyer.transform));
                }
            }
        }

        // Collect selected polygons and assign them control ids.
        void RegisterActiveControls(IEnumerable<Object> targets)
        {
            m_ActiveControls.Clear();
            foreach (var obj in targets)
            {
                if (obj is Keyer keyer && m_Cache.TryGetPointsSerializedProperty(keyer, out var pointsProperty))
                {
                    var id = GUIUtility.GetControlID(FocusType.Passive);
                    var editScopeFactory = new PolygonEditScopeFactory(keyer, pointsProperty);
                    var info = new PolygonInfo(pointsProperty, editScopeFactory, keyer.transform);
                    m_ActiveControls.Add(id, new ActiveControl(keyer, info));

                    var dist = PolygonUtility.DistanceToPolygon(info);
                    HandleUtility.AddControl(id, dist);
                }
            }
        }
    }
}
