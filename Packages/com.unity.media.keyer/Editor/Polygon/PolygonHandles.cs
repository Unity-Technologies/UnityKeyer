using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using GeometryUtil = Unity.Media.Keyer.Geometry.Utilities;

namespace Unity.Media.Keyer.Editor
{
    class PolygonHandles
    {
        static class Content
        {
            public static readonly string HelpZeroPoints = new("Click and drag anywhere to create a polygon.");
            public static readonly string HelpOnePoint = new("Click and drag the point to create a polygon.");
            public static readonly string HelpTwoPoints = new("Click and drag anywhere on the segment to create a polygon.");
            public static readonly string HelpMoveOrDelete = new("Drag the point to move its position, or press \"Delete\" to delete selected points.");
            public static readonly GUIContent DeletionSelfIntersect = new("Cannot delete points. The polygon cannot be self-intersecting.");
        }

        enum Mode : byte
        {
            DrawPath,
            MouseSelect,
            Insert,
            DrawSelection
        }

        enum InsertionCandidateStatus : byte
        {
            Undefined,
            Invalid,
            Valid
        }

        struct InsertionCandidate
        {
            public InsertionCandidateStatus Status;

            public int Index;
            public Vector2 Position;

            public static InsertionCandidate GetDefault()
            {
                return new InsertionCandidate
                {
                    Status = InsertionCandidateStatus.Undefined,
                    Index = -1,
                    Position = Vector2.zero
                };
            }
        }

        struct SelectionRect
        {
            public Vector2 Anchor;
            public Vector2 Tip;
        }

        enum HoverElement : byte
        {
            None,
            Point,
            Segment
        }

        struct HoverState
        {
            public HoverElement Element;
            public int Index;

            public static HoverState GetDefault()
            {
                return new HoverState
                {
                    Element = HoverElement.None,
                    Index = -1
                };
            }
        }

        static readonly Rect k_UnitRect = new(Vector2.zero, Vector2.one);

        Mode m_Mode = Mode.MouseSelect;
        InsertionCandidate m_InsertionCandidate;
        SelectionRect m_SelectionRect;
        HoverState m_HoverState;
        Rect m_ScreenSpaceBounds;
        int m_LastPointCount;
        readonly List<int> m_SelectedPointsIndices = new();

        // Caches.
        readonly Vector3[] m_InsertionPoints = new Vector3[4];

        void Reset()
        {
            m_Mode = Mode.MouseSelect;
            m_InsertionCandidate = InsertionCandidate.GetDefault();
            m_HoverState = HoverState.GetDefault();
            m_SelectedPointsIndices.Clear();
        }

        public IEnumerable<string> GetContextualInfo()
        {
            if (m_Mode == Mode.MouseSelect)
            {
                if (m_SelectedPointsIndices.Count > 0)
                {
                    yield return Content.HelpMoveOrDelete;
                }
                else
                {
                    switch (m_LastPointCount)
                    {
                        case 0:
                            yield return Content.HelpZeroPoints;
                            break;
                        case 1:
                            yield return Content.HelpOnePoint;
                            break;
                        case 2:
                            yield return Content.HelpTwoPoints;
                            break;
                    }
                }
            }
        }

        public bool Do(Event evt, int controlId, Rect guiRect, PolygonInfo info)
        {
            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.Layout:
                {
                    // Avoids exiting the tool preemptively.
                    HandleUtility.AddDefaultControl(controlId);
                    m_LastPointCount = info.GetNumPoints();
                    break;
                }

                case EventType.Repaint:
                {
                    PolygonUtility.DrawImageSpaceBounds(info);
                    DrawHover(info);

                    if (GUIUtility.hotControl == controlId || m_SelectedPointsIndices.Count > 0)
                    {
                        HandleRepaint(info);
                    }

                    break;
                }

                case EventType.MouseMove:
                {
                    // Update hover state.
                    if (guiRect.Contains(evt.mousePosition))
                    {
                        var useEvent = HandleMouseMove(info, evt.mousePosition);
                        HandleUtility.Repaint();
                        return useEvent;
                    }

                    if (HandleUtility.nearestControl == controlId)
                    {
                        HandleUtility.Repaint();
                    }

                    break;
                }

                case EventType.MouseDown:
                {
                    // Select nearest point.
                    if (HandleUtility.nearestControl == controlId)
                    {
                        UpdateScreenSpaceBounds(info);
                        if (HandleMouseDown(info, evt.mousePosition))
                        {
                            GUIUtility.hotControl = controlId;
                            return true;
                        }
                    }

                    break;
                }

                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl == controlId && evt.button == 0)
                    {
                        HandleMouseDrag(info, evt.mousePosition, evt.delta);
                        return true;
                    }

                    break;
                }

                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        return HandleMouseUp(info);
                    }

                    break;
                }

                case EventType.KeyDown:
                {
                    if (m_SelectedPointsIndices.Count > 0 &&
                        evt.keyCode is KeyCode.Delete or KeyCode.Backspace)
                    {
                        DeleteSelectedPoints(info);
                        Reset();
                        return true;
                    }

                    break;
                }
            }

            return false;
        }

        void HandleRepaint(PolygonInfo info)
        {
            switch (m_Mode)
            {
                case Mode.DrawPath:
                    DrawPathInsertionCandidate(info);
                    break;
                case Mode.MouseSelect:
                    DrawSelectedPoints(info);
                    break;
                case Mode.Insert:
                    DrawSegmentInsertionCandidate(info);
                    break;
                case Mode.DrawSelection:
                    DrawSelectionRect();
                    DrawSelectedPoints(info);
                    break;
            }
        }

        bool HandleMouseMove(PolygonInfo info, Vector2 mousePosition)
        {
            var closestPointIndex = PolygonUtility.FindClosestPoint(
                info, mousePosition, out _, out var distance);
            if (distance < PolygonToolConsts.PointSelectionRadiusScreenSpace)
            {
                m_HoverState = new HoverState
                {
                    Element = HoverElement.Point,
                    Index = closestPointIndex
                };
                return true;
            }

            var distToSegment = PolygonUtility.DistanceToClosestSegment(info, mousePosition, out var closestOriginIndex);
            if (distToSegment < PolygonToolConsts.PointSelectionRadiusScreenSpace)
            {
                m_HoverState = new HoverState
                {
                    Element = HoverElement.Segment,
                    Index = closestOriginIndex
                };
                return true;
            }

            m_HoverState = HoverState.GetDefault();
            return false;
        }

        bool HandleMouseDown(PolygonInfo info, Vector2 mousePosition)
        {
            // Gather required data.
            var closestPointIndex = PolygonUtility.FindClosestPoint(
                info, mousePosition, out _, out var distance);

            // We're out of bounds, abort.
            if (!m_ScreenSpaceBounds.Contains(mousePosition))
            {
                return false;
            }

            // Stay in draw mode until we select an existing point.
            if (m_Mode == Mode.DrawPath &&
                distance > PolygonToolConsts.PointSelectionRadiusScreenSpace &&
                m_InsertionCandidate.Status != InsertionCandidateStatus.Invalid)
            {
                m_InsertionCandidate.Status = InsertionCandidateStatus.Undefined;
                return true;
            }

            // Stay in the current selection context.
            if (m_SelectedPointsIndices.Contains(closestPointIndex) &&
                distance < PolygonToolConsts.PointSelectionRadiusScreenSpace)
            {
                m_Mode = Mode.MouseSelect;
                return true;
            }

            Reset();
            var numPoints = info.GetNumPoints();

            // Enter draw mode when the polygon is empty.
            if (numPoints == 0)
            {
                m_InsertionCandidate.Status = InsertionCandidateStatus.Undefined;
                m_Mode = Mode.DrawPath;

                if (PolygonUtility.TryScreenToImagePosition(info, mousePosition, out var localPosition))
                {
                    using var editScope = info.CreateEditScope();
                    info.AppendPoint(localPosition);
                    return true;
                }
            }

            // Select one point.
            if (distance < PolygonToolConsts.PointSelectionRadiusScreenSpace)
            {
                // Special case, back to draw mode for the polygon needs additional points.

                if (numPoints < 3)
                {
                    // So we don't have to track the insertion index.
                    if (closestPointIndex == 0 && numPoints > 1)
                    {
                        using var editScope = info.CreateEditScope();
                        info.SwapPointsAtIndices(0, 1);
                    }

                    m_InsertionCandidate.Status = InsertionCandidateStatus.Undefined;
                    m_Mode = Mode.DrawPath;
                    return true;
                }

                m_Mode = Mode.MouseSelect;
                m_SelectedPointsIndices.Add(closestPointIndex);
                return true;
            }

            var distToSegment = PolygonUtility.DistanceToClosestSegment(info, mousePosition, out _);
            if (distToSegment < PolygonToolConsts.PointSelectionRadiusScreenSpace)
            {
                m_InsertionCandidate.Status = InsertionCandidateStatus.Undefined;
                m_Mode = Mode.Insert;
                return true;
            }

            // Draw selection.
            if (m_ScreenSpaceBounds.Contains(mousePosition))
            {
                m_Mode = Mode.DrawSelection;
                m_SelectionRect = new SelectionRect
                {
                    Anchor = mousePosition,
                    Tip = mousePosition
                };
                return true;
            }

            return false;
        }

        void HandleMouseDrag(PolygonInfo info, Vector2 mousePosition, Vector2 mouseDelta)
        {
            switch (m_Mode)
            {
                case Mode.DrawPath:
                    MoveDrawPath(info, mousePosition);
                    break;
                case Mode.MouseSelect:
                    MoveSelectedPoints(info, mousePosition, mouseDelta);
                    break;
                case Mode.Insert:
                    MoveSegmentInsertionPoint(info, mousePosition);
                    break;
                case Mode.DrawSelection:
                    UpdateSelectionRect(info, mousePosition);
                    break;
            }
        }

        bool HandleMouseUp(PolygonInfo info)
        {
            switch (m_Mode)
            {
                case Mode.MouseSelect:
                {
                    // Consume the event if a point is selected.
                    return m_SelectedPointsIndices.Count > 0;
                }
                case Mode.DrawPath:
                {
                    var useEvent = InsertPointCandidate(info);
                    Reset();
                    return useEvent;
                }
                case Mode.Insert:
                {
                    var useEvent = InsertSegmentCandidate(info);
                    Reset();
                    return useEvent;
                }
                case Mode.DrawSelection:
                {
                    m_Mode = Mode.MouseSelect;
                    return true;
                }
            }

            return false;
        }

        void MoveDrawPath(PolygonInfo info, Vector2 mousePosition)
        {
            if (PolygonUtility.TryScreenToImagePosition(info, mousePosition, out var position))
            {
                // Locally perform insertion without modifying the actual polygon.
                using var scopedList = ScopedList<Vector2>.Create();
                info.CopyPoints(scopedList.List);
                scopedList.List.Add(position);

                PolygonUtility.FindClosestPoint(info, mousePosition, out _, out var distance);

                var valid =
                    GeometryUtil.Contains(k_UnitRect, scopedList.List) &&
                    !GeometryUtil.HasSelfIntersection(scopedList.List) &&
                    distance > PolygonToolConsts.PointSelectionRadiusScreenSpace;

                m_InsertionCandidate.Status = valid ? InsertionCandidateStatus.Valid : InsertionCandidateStatus.Invalid;
                m_InsertionCandidate.Position = position;
            }
        }

        void MoveSegmentInsertionPoint(PolygonInfo info, Vector2 mousePosition)
        {
            if (PolygonUtility.TryScreenToImageInsertion(info, mousePosition, out var insertionIndex, out var position))
            {
                position = GeometryUtil.Clamp(k_UnitRect, position);

                // Locally perform insertion without modifying the actual polygon.
                using var scopedList = ScopedList<Vector2>.Create();
                info.CopyPoints(scopedList.List);
                scopedList.List.Insert(insertionIndex + 1, position);

                var valid = !GeometryUtil.HasSelfIntersection(scopedList.List);

                m_InsertionCandidate = new InsertionCandidate
                {
                    Status = valid ? InsertionCandidateStatus.Valid : InsertionCandidateStatus.Invalid,
                    Index = insertionIndex,
                    Position = position
                };
            }
            else
            {
                // Reset insertion candidate, no valid insertion.
                m_InsertionCandidate = InsertionCandidate.GetDefault();
            }
        }

        void UpdateSelectionRect(PolygonInfo info, Vector2 mousePosition)
        {
            m_SelectionRect.Tip = mousePosition;

            // Selected points highlight.
            var selectionRect = Utilities.Encompass(m_SelectionRect.Anchor, m_SelectionRect.Tip);

            // Filter points within the selection rect.
            m_SelectedPointsIndices.Clear();
            var numPoints = info.GetNumPoints();
            for (var i = 0; i != numPoints; ++i)
            {
                var worldPoint = info.GetImageToWorldPoint(info.GetPointAtIndex(i));
                var guiPoint = PolygonUtility.WorldToGUIPoint(worldPoint);
                if (selectionRect.Contains(guiPoint))
                {
                    m_SelectedPointsIndices.Add(i);
                }
            }
        }

        void MoveSelectedPoints(PolygonInfo info, Vector2 mousePosition, Vector2 mouseDelta)
        {
            if (PolygonUtility.TryScreenToImageVector(info, mousePosition, mouseDelta, out var delta))
            {
                // Locally perform change, only applied to the polygon if valid.
                using var scopedList = ScopedList<Vector2>.Create();
                info.CopyPoints(scopedList.List);

                foreach (var index in m_SelectedPointsIndices)
                {
                    scopedList.List[index] = GeometryUtil.Clamp(k_UnitRect, scopedList.List[index] + (Vector2)delta);
                }

                if (!GeometryUtil.HasSelfIntersection(scopedList.List))
                {
                    using var editScope = info.CreateEditScope();

                    // Change is valid, apply it.
                    foreach (var index in m_SelectedPointsIndices)
                    {
                        info.SetPointAtIndex(index, scopedList.List[index]);
                    }
                }
            }
        }

        void DeleteSelectedPoints(PolygonInfo info)
        {
            if (m_Mode != Mode.MouseSelect && m_Mode != Mode.DrawSelection)
            {
                return;
            }

            using var pointsToRemoveScopedList = ScopedList<Vector2>.Create();
            using var newPolygonScopedList = ScopedList<Vector2>.Create();

            // Locally perform change, only applied to the polygon if valid.
            // Do not modify polygon points directly as indices would change.
            info.CopyPoints(newPolygonScopedList.List);

            foreach (var index in m_SelectedPointsIndices)
            {
                pointsToRemoveScopedList.List.Add(info.GetPointAtIndex(index));
            }

            foreach (var point in pointsToRemoveScopedList.List)
            {
                newPolygonScopedList.List.Remove(point);
            }

            if (GeometryUtil.HasSelfIntersection(newPolygonScopedList.List))
            {
                SceneView.lastActiveSceneView.ShowNotification(Content.DeletionSelfIntersect, 2f);
            }
            else
            {
                using var editScope = info.CreateEditScope();
                m_SelectedPointsIndices.Sort();
                for (var i = m_SelectedPointsIndices.Count - 1; i != -1; --i)
                {
                    info.DeletePointAtIndex(m_SelectedPointsIndices[i]);
                }

                UpdateScreenSpaceBounds(info);
            }
        }

        bool InsertPointCandidate(PolygonInfo info)
        {
            if (m_InsertionCandidate.Status == InsertionCandidateStatus.Valid)
            {
                using var editScope = info.CreateEditScope();
                info.AppendPoint(m_InsertionCandidate.Position);
                UpdateScreenSpaceBounds(info);
                return true;
            }

            return false;
        }

        bool InsertSegmentCandidate(PolygonInfo info)
        {
            if (m_InsertionCandidate.Status == InsertionCandidateStatus.Valid)
            {
                using var editScope = info.CreateEditScope();
                info.InsertPointAtIndex(m_InsertionCandidate.Index + 1, m_InsertionCandidate.Position);
                UpdateScreenSpaceBounds(info);
                return true;
            }

            return false;
        }

        void UpdateScreenSpaceBounds(PolygonInfo info)
        {
            // We start with the bounds of the canvas, then we enlarge them to encompass the geometry as well.
            var bounds = PolygonUtility.GetScreenSpaceBounds(info);

            // Enlarge by 24px margin.
            bounds = new RectOffset(24, 24, 24, 24).Add(bounds);
            if (PolygonUtility.GetGeometryScreenSpaceBounds(info, out var geometryBounds))
            {
                geometryBounds = GeometryUtil.Expand(geometryBounds, PolygonToolConsts.PolygonBoundsMarginScreenSpace);
                bounds = Utilities.Encompass(bounds, geometryBounds);
            }

            m_ScreenSpaceBounds = bounds;
        }

        void DrawHover(PolygonInfo info)
        {
            switch (m_HoverState.Element)
            {
                case HoverElement.None:
                    return;
                case HoverElement.Point:
                {
                    using (new Handles.DrawingScope(PolygonToolPalette.Hover, info.LocalToWorld))
                    {
                        var position = info.GetImageToLocalPoint(m_HoverState.Index);
                        PolygonUtility.DrawPointHandle(position, info.Normal);
                    }
                }
                break;
                case HoverElement.Segment:
                {
                    using (new Handles.DrawingScope(PolygonToolPalette.Hover, info.LocalToWorld))
                    {
                        var positionA = info.GetImageToLocalPoint(m_HoverState.Index);
                        var indexB = (m_HoverState.Index + 1) % info.GetNumPoints();
                        var positionB = info.GetImageToLocalPoint(indexB);
                        Handles.DrawLine(positionA, positionB);
                    }
                }
                break;
            }
        }

        void DrawPathInsertionCandidate(PolygonInfo info)
        {
            if (m_InsertionCandidate.Status == InsertionCandidateStatus.Undefined || info.GetNumPoints() == 0)
            {
                return;
            }

            var color = GetInsertionCandidateColor(m_InsertionCandidate);
            using (new Handles.DrawingScope(color, info.LocalToWorld))
            {
                var origin = info.GetImageToLocalPoint(info.GetPointAtIndex(info.GetNumPoints() - 1));
                var destination = info.GetImageToLocalPoint(m_InsertionCandidate.Position);
                Handles.DrawDottedLine(origin, destination, PolygonToolConsts.DottedLineSize);
                PolygonUtility.DrawPointHandle(destination, info.Normal);
            }
        }

        void DrawSegmentInsertionCandidate(PolygonInfo info)
        {
            if (m_InsertionCandidate.Status == InsertionCandidateStatus.Undefined)
            {
                return;
            }

            if (m_InsertionCandidate.Index == -1)
            {
                return;
            }

            var color = GetInsertionCandidateColor(m_InsertionCandidate);
            using (new Handles.DrawingScope(color, info.LocalToWorld))
            {
                // Draw the insertion point as well as the two new edges.
                var origin = info.GetImageToLocalPoint(m_InsertionCandidate.Index);
                var destination = info.GetImageToLocalPoint((m_InsertionCandidate.Index + 1) % info.GetNumPoints());
                var candidatePosition = info.GetImageToLocalPoint(m_InsertionCandidate.Position);
                m_InsertionPoints[0] = origin;
                m_InsertionPoints[1] = candidatePosition;
                m_InsertionPoints[2] = candidatePosition;
                m_InsertionPoints[3] = destination;
                Handles.DrawDottedLines(m_InsertionPoints, PolygonToolConsts.DottedLineSize);
                PolygonUtility.DrawPointHandle(candidatePosition, info.Normal);
            }
        }

        void DrawSelectionRect()
        {
            Handles.BeginGUI();
            var rect = Utilities.Encompass(m_SelectionRect.Anchor, m_SelectionRect.Tip);
            Styles.selectionRect.Draw(rect, GUIContent.none, false, false, false, false);
            Handles.EndGUI();
        }

        void DrawSelectedPoints(PolygonInfo info)
        {
            using (new Handles.DrawingScope(PolygonToolPalette.Select, info.LocalToWorld))
            {
                foreach (var index in m_SelectedPointsIndices)
                {
                    PolygonUtility.DrawPointHandle(info.GetImageToLocalPoint(index), info.Normal);
                }
            }
        }

        static Color GetInsertionCandidateColor(InsertionCandidate insertionCandidate)
        {
            return insertionCandidate.Status == InsertionCandidateStatus.Valid ? PolygonToolPalette.Ok : PolygonToolPalette.Error;
        }
    }
}
