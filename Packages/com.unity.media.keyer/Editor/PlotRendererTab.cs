using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class PlotRendererTab : IPreviewTab, IKeyerAccess
    {
        const int k_MinWidth = 300;
        const int k_PlotMarginY = 2;
        const int k_MaxIntermediatesResults = UIUtils.k_TotalDisplayValues;

        static readonly Color k_ColorSelected = Color.yellow;
        static readonly Color k_ColorUnselected = Color.red;

        const int k_HandleRadius = 5;
        const float k_SelectionRadius = 5.0f;

        VisualElement m_TopContainer;
        VisualElement m_KeyerEditorContainer;
        VisualElement m_InspectorScrollView;
        DropdownField m_DisplayField;
        VisualElement m_PlotsContainer;
        Keyer m_Keyer;
        Keyer.Display m_CurrentDisplay;
        bool m_NeedsRenderPlot = true;
        Label m_PlotInfoLabel;

        enum Selection
        {
            None,
            Start,
            End
        }

        Selection m_Selection;

        readonly PlotRenderer m_PlotRenderer = new();
        Vector2 m_StartPoint = new Vector2(0.25f, 0.25f);
        Vector2 m_EndPoint = new Vector2(0.75f, 0.75f);

        Rect m_GuiRect;
        Rect m_PlotRect;

        RenderTexture[] m_PlotCaptureRT = new RenderTexture[k_MaxIntermediatesResults];
        PlotRenderer.IPlot[] m_Plots = new PlotRenderer.IPlot[k_MaxIntermediatesResults];
        RenderTexture m_DisplayRT;
        RenderTexture m_PlotRenderTarget;

        UIUtils.DisplayFlags m_SelectedPlots = UIUtils.DisplayFlags.CoreMatte;

        DisplayDropdownWrapper m_DropdownWrapper;
        DisplayMaskFieldWrapper m_DisplayMaskFieldWrapper;
        Keyer IKeyerAccess.GetKeyer() => m_Keyer;

        Action m_Changed = delegate { };

        event Action IKeyerAccess.Changed
        {
            add => m_Changed += value;
            remove => m_Changed -= value;
        }

        static VisualElement CreateTopContainer()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.flexGrow = 1;
            container.style.alignContent = Align.Stretch;
            container.style.minWidth = 200;
            return container;
        }

        VisualElement CreatePlotsContainer()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Column;
            container.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            var ic = new IMGUIContainer();
            ic.StretchToParentSize();
            ic.style.flexGrow = 1;
            ic.onGUIHandler = OnGUIHandler;
            container.Add(ic);

            return container;
        }

        void UnInitializePlotsAndDisplayCapture()
        {
            UnregisterDisplayRTIfNeeded(m_Keyer, m_CurrentDisplay);
            Utilities.DeallocateIfNeeded(m_DisplayRT);
            RemoveAllPlots();
            UnregisterAllPlotCaptureRTIfNeeded(m_Keyer);
            DeallocateAllPlotCaptureRT();
        }

        void InitializePlotsAndDisplayCapture()
        {
            Utilities.AllocateIfNeeded(ref m_DisplayRT, m_Keyer.Foreground.width, m_Keyer.Foreground.height, 0, true);
            m_Keyer.AddCapture(m_CurrentDisplay, m_DisplayRT);
            AllocateAllPlotCaptureRT();
            RegisterAllPlotCaptureRT(m_Keyer);
            UpdatePlots();
        }

        void OnKeyerSettingsChanged()
        {
            // When the foreground image changes, we need to update the plot renderer geometry or
            // UnInitialize the plots and display capture when the foreground becomes null.
            if (m_Keyer.Foreground == null)
            {
                UnInitializePlotsAndDisplayCapture();
            }
            else
            {
                // When the foreground image changes from null to a valid texture
                // we check if m_DisplayRT is then we need to initialize the plots and display capture
                if (m_DisplayRT == null)
                {
                    InitializePlotsAndDisplayCapture();
                }

                UpdateViewportGeometry();
            }

            m_NeedsRenderPlot = true;
        }

        VisualElement CreateKeyerEditorContainer()
        {
            m_KeyerEditorContainer = new VisualElement();
            m_KeyerEditorContainer.style.flexDirection = FlexDirection.Column;
            m_KeyerEditorContainer.style.flexGrow = 1;
            m_KeyerEditorContainer.style.alignContent = Align.Stretch;
            m_KeyerEditorContainer.style.minWidth = k_MinWidth;

            return m_KeyerEditorContainer;
        }

        void OnPlotSelectionChanged(UIUtils.DisplayFlags selectedFlags)
        {
            m_SelectedPlots = selectedFlags;
            UpdatePlots();
        }

        Label CreatePlotInfoLabel()
        {
            var label = new Label();
            label.AddToClassList("ui-plot-info-label");
            label.style.display = DisplayStyle.None;
            return label;
        }

        public VisualElement CreateGUI()
        {
            m_TopContainer = new TwoPaneSplitView(1, k_MinWidth, TwoPaneSplitViewOrientation.Horizontal);
            m_TopContainer.style.flexGrow = 1;

            var plotUxContainer = CreateTopContainer();
            m_DisplayField = new DropdownField();
            m_PlotsContainer = CreatePlotsContainer();
            m_PlotInfoLabel = CreatePlotInfoLabel();
            m_PlotsContainer.Add(m_PlotInfoLabel);
            plotUxContainer.Add(m_DisplayField);
            var maskField = new MaskField();
            plotUxContainer.Add(maskField);
            plotUxContainer.Add(m_PlotsContainer);
            m_TopContainer.Add(plotUxContainer);
            m_TopContainer.Add(CreateKeyerEditorContainer());
            m_DropdownWrapper = new DisplayDropdownWrapper(m_DisplayField, this, m_CurrentDisplay, OnDisplayChanged);
            m_DisplayMaskFieldWrapper = new DisplayMaskFieldWrapper(maskField, "Choose Plot", this, m_SelectedPlots, OnPlotSelectionChanged);
            RegisterKeyerChangeIfNeeded();

            return m_TopContainer;
        }

        public void SetKeyer(Keyer newKeyer)
        {
            UnregisterKeyerChangeIfNeeded();
            UnInitializePlotsAndDisplayCapture();
            m_CurrentDisplay = m_DropdownWrapper.GetValue();
            m_Keyer = newKeyer;

            m_InspectorScrollView?.RemoveFromHierarchy();
            if (m_Keyer != null)
            {
                if (m_Keyer.Foreground != null)
                {
                    InitializePlotsAndDisplayCapture();
                }

                RegisterKeyerChangeIfNeeded();
                m_InspectorScrollView = UIUtils.CreateKeyerInspector(m_Keyer, m_KeyerEditorContainer);
            }
        }

        void RegisterKeyerChangeIfNeeded()
        {
            if (m_Keyer != null)
            {
                var access = (IKeyerAccess)m_Keyer;
                access.Changed += OnKeyerSettingsChanged;
                access.Changed += m_Changed.Invoke; // For the display dropdown
            }
        }

        void UnregisterKeyerChangeIfNeeded()
        {
            if (m_Keyer != null)
            {
                var access = (IKeyerAccess)m_Keyer;
                access.Changed -= OnKeyerSettingsChanged;
                access.Changed -= m_Changed.Invoke; // For the display dropdown
            }
        }

        void OnDisplayChanged(Keyer.Display display)
        {
            UnregisterDisplayRTIfNeeded(m_Keyer, m_CurrentDisplay);
            m_CurrentDisplay = display;
            if (m_Keyer == null)
            {
                return;
            }

            if (m_Keyer.Foreground != null)
            {
                Utilities.AllocateIfNeeded(ref m_DisplayRT, m_Keyer.Foreground.width, m_Keyer.Foreground.height, 0, true);
                m_Keyer.AddCapture(m_CurrentDisplay, m_DisplayRT);
            }
            else
            {
                Utilities.DeallocateIfNeeded(ref m_DisplayRT);
            }
        }

        void AllocateAllPlotCaptureRT()
        {
            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                if (m_SelectedPlots.HasFlag(UIUtils.ConvertToFlags((Keyer.Display)i)))
                {
                    Utilities.AllocateIfNeeded(ref m_PlotCaptureRT[i], m_Keyer.Foreground.width, m_Keyer.Foreground.height, 0, true);
                }
                else
                {
                    Utilities.DeallocateIfNeeded(ref m_PlotCaptureRT[i]);
                }
            }
        }

        void DeallocateAllPlotCaptureRT()
        {
            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                Utilities.DeallocateIfNeeded(ref m_PlotCaptureRT[i]);
            }
        }

        void UnregisterAllPlotCaptureRTIfNeeded(Keyer keyer)
        {
            if (keyer != null)
            {
                for (var i = 0; i < k_MaxIntermediatesResults; ++i)
                {
                    if (m_PlotCaptureRT[i] != null)
                    {
                        keyer.RemoveCapture((Keyer.Display)i, m_PlotCaptureRT[i]);
                    }
                }
            }
        }

        void RegisterAllPlotCaptureRT(Keyer keyer)
        {
            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                if (m_SelectedPlots.HasFlag(UIUtils.ConvertToFlags((Keyer.Display)i)))
                {
                    keyer.AddCapture((Keyer.Display)i, m_PlotCaptureRT[i]);
                }
            }
        }

        void UnregisterDisplayRTIfNeeded(Keyer keyer, Keyer.Display display)
        {
            if (keyer != null)
            {
                keyer.RemoveCapture(display, m_DisplayRT);
            }
        }

        static Rect ScaleToFit(Rect rect, Vector2 size)
        {
            var aspect = size.x / size.y;
            if (aspect > 1)
            {
                return new Rect(rect.x, rect.y, rect.width, rect.width / aspect);
            }

            return new Rect(rect.x + (rect.width - rect.height * aspect) * .5f, rect.y, rect.height * aspect, rect.height);
        }

        void UpdateViewportGeometry()
        {
            var subRect = new Rect(0, 0, m_PlotsContainer.layout.width, m_PlotsContainer.layout.height - m_PlotsContainer.layout.height / 4);
            if (m_Keyer != null && m_Keyer.Foreground != null)
            {
                m_GuiRect = ScaleToFit(subRect, new Vector2(m_Keyer.Foreground.width, m_Keyer.Foreground.height));

                if (m_GuiRect.height > m_PlotsContainer.layout.height - m_PlotsContainer.layout.height / 4)
                {
                    m_PlotRect = new Rect(0, m_PlotsContainer.layout.height - m_PlotsContainer.layout.height / 4, m_PlotsContainer.layout.width, m_PlotsContainer.layout.height / 4);
                }
                else
                {
                    m_PlotRect = new Rect(0, m_GuiRect.height, m_PlotsContainer.layout.width, m_PlotsContainer.layout.height - m_GuiRect.height);
                }
            }
            else
            {
                m_GuiRect = subRect;
                m_PlotRect = new Rect(0, m_GuiRect.height, m_PlotsContainer.layout.width, m_PlotsContainer.layout.height - m_GuiRect.height);
            }

            m_PlotRect.y += k_PlotMarginY;
            m_PlotRect.height -= k_PlotMarginY;
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateViewportGeometry();
            m_NeedsRenderPlot = true;
        }

        Vector2 GUISpaceToNormalizedSpace(Vector3 pos)
        {
            var x = (pos.x - m_GuiRect.x) / m_GuiRect.width;
            var y = 1.0f - ((pos.y - m_GuiRect.y) / m_GuiRect.height);
            return new Vector2(x, y);
        }

        Vector3 NormalizedSpaceToGUISpace(Vector2 pos)
        {
            var x = pos.x * m_GuiRect.width + m_GuiRect.x;
            var y = (1.0f - pos.y) * m_GuiRect.height + m_GuiRect.y;
            return new Vector3(x, y, 0);
        }

        Vector2 PlotRectSpaceToNormalizedSpace(Vector3 pos)
        {
            var x = (pos.x - m_PlotRect.x) / m_PlotRect.width;
            var y = 1.0f - (pos.y - m_PlotRect.y) / m_PlotRect.height;
            return new Vector2(x, y);
        }

        void DrawPlotUI(Vector3 p1, Vector3 p2)
        {
            Handles.BeginGUI();
            Handles.color = k_ColorUnselected;
            Handles.DrawLine(p1, p2, 0);
            Handles.color = m_Selection == Selection.Start ? k_ColorSelected : k_ColorUnselected;
            Handles.DrawSolidDisc(p1, Vector3.forward, k_HandleRadius);
            Handles.color = m_Selection == Selection.End ? k_ColorSelected : k_ColorUnselected;
            Handles.DrawSolidDisc(p2, Vector3.forward, k_HandleRadius);
            Handles.EndGUI();
        }

        void DoPlotUI()
        {
            var mousePos = Event.current.mousePosition;
            var currentEvent = Event.current;
            var mouseNormalized = GUISpaceToNormalizedSpace(mousePos);
            var mouseGUISpace = new Vector3(mousePos.x, mousePos.y, 0.0f);
            var p1 = NormalizedSpaceToGUISpace(m_StartPoint);
            var p2 = NormalizedSpaceToGUISpace(m_EndPoint);
            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    if (Vector3.Distance(mouseGUISpace, p1) < k_SelectionRadius)
                    {
                        m_Selection = Selection.Start;
                    }
                    else if (Vector3.Distance(mouseGUISpace, p2) < k_SelectionRadius)
                    {
                        m_Selection = Selection.End;
                    }
                    else
                    {
                        m_Selection = Selection.None;
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseMove:
                case EventType.MouseDrag:
                    var prevStartPoint = m_StartPoint;
                    var prevEndPoint = m_EndPoint;
                    if (m_Selection == Selection.Start)
                    {
                        m_StartPoint = mouseNormalized;
                    }
                    else if (m_Selection == Selection.End)
                    {
                        m_EndPoint = mouseNormalized;
                    }

                    m_NeedsRenderPlot |= prevEndPoint != m_EndPoint || prevStartPoint != m_StartPoint;
                    currentEvent.Use();
                    break;
                case EventType.MouseUp:
                    m_Selection = Selection.None;
                    currentEvent.Use();
                    break;
            }

            DrawPlotUI(p1, p2);
        }

        void MovePlotInfoLabelInsidePlotRectWidthIfNeeded(float plotInfoPositionX)
        {
            // If the plot info label is outside the plot rect width, we move it inside
            if (plotInfoPositionX + m_PlotInfoLabel.resolvedStyle.width > m_PlotRect.xMax)
            {
                m_PlotInfoLabel.style.left = m_PlotRect.xMax - m_PlotInfoLabel.resolvedStyle.width;
            }
            else if (plotInfoPositionX - m_PlotInfoLabel.resolvedStyle.width / 2 < m_PlotRect.xMin)
            {
                m_PlotInfoLabel.style.left = m_PlotRect.xMin;
            }
        }

        void DoPlotInfoLabelUI()
        {
            var plotInfoPosition = Event.current.mousePosition;
            var mousePosInNormalizedSpace = PlotRectSpaceToNormalizedSpace(plotInfoPosition);
            var plotInfoId = m_PlotRenderer.GetIdAtCoordinates(mousePosInNormalizedSpace);

            if (plotInfoId != 0 && m_PlotRect.Contains(plotInfoPosition))
            {
                var selectedName = (Keyer.Display)(plotInfoId - 1);
                m_PlotInfoLabel.text = selectedName.ToString();
                m_PlotInfoLabel.style.left = plotInfoPosition.x;
                MovePlotInfoLabelInsidePlotRectWidthIfNeeded(plotInfoPosition.x);
                m_PlotInfoLabel.style.bottom = m_PlotRect.height - (plotInfoPosition.y - m_PlotRect.y);
                m_PlotInfoLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_PlotInfoLabel.style.display = DisplayStyle.None;
            }
        }

        void DrawCurrentDisplayedImage()
        {
            if (m_CurrentDisplay == Keyer.Display.Result)
            {
                EditorGUI.DrawTextureTransparent(m_GuiRect, m_DisplayRT);
            }
            else
            {
                GUI.DrawTexture(m_GuiRect, m_DisplayRT);
            }
        }

        void OnGUIHandler()
        {
            if (m_Keyer == null)
            {
                return;
            }

            if (m_NeedsRenderPlot)
            {
                Render();
                m_NeedsRenderPlot = false;
            }

            if (m_DisplayRT != null)
            {
                DrawCurrentDisplayedImage();
            }

            if (m_PlotRenderTarget != null)
            {
                GUI.DrawTexture(m_PlotRect, m_PlotRenderTarget);
                DoPlotInfoLabelUI();
            }

            DoPlotUI();
        }

        void UpdatePlots()
        {
            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                if (m_SelectedPlots.HasFlag(UIUtils.ConvertToFlags((Keyer.Display)i)) && m_Keyer.Foreground != null)
                {
                    if (m_Plots[i] == null)
                    {
                        if (i == (int)Keyer.Display.Front || i == (int)Keyer.Display.Result || i == (int)Keyer.Display.Despill)
                        {
                            var rgbPlot = m_PlotRenderer.CreateRgbPlot();
                            m_Plots[i] = rgbPlot;
                            m_Plots[i].Enabled = true;

                            // Plot id is used to identify the plot in the shader and must be unique.
                            // We use the Keyer.Display + 1 to avoid using 0 as an id.
                            m_Plots[i].Id = (byte)(i + 1);
                            rgbPlot.Channels = PlotRenderer.Channels.All;
                            rgbPlot.CurveStyle = PlotRenderer.CurveStyle.Outline;
                            if (m_PlotCaptureRT[i] != null)
                            {
                                rgbPlot.Source = m_PlotCaptureRT[i];
                            }
                            else
                            {
                                Utilities.AllocateIfNeeded(ref m_PlotCaptureRT[i], m_Keyer.Foreground.width, m_Keyer.Foreground.height, 0, true);
                                m_PlotCaptureRT[i].name = ((Keyer.Display)i).ToString();
                                m_Keyer.AddCapture((Keyer.Display)i, m_PlotCaptureRT[i]);
                                rgbPlot.Source = m_PlotCaptureRT[i];
                            }
                        }
                        else
                        {
                            var plot = m_PlotRenderer.CreateSingleChannelPlot();
                            m_Plots[i] = plot;
                            m_Plots[i].Enabled = true;

                            // Plot id is used to identify the plot in the shader and must be unique.
                            // We use the Keyer.Display + 1 to avoid using 0 as an id.
                            m_Plots[i].Id = (byte)(i + 1);
                            plot.CurveStyle = PlotRenderer.CurveStyle.Outline;
                            plot.Color = Color.white;
                            if (m_PlotCaptureRT[i] != null)
                            {
                                plot.Source = m_PlotCaptureRT[i];
                            }
                            else
                            {
                                Utilities.AllocateIfNeeded(ref m_PlotCaptureRT[i], m_Keyer.Foreground.width, m_Keyer.Foreground.height, 0, true);
                                m_PlotCaptureRT[i].name = ((Keyer.Display)i).ToString();
                                m_Keyer.AddCapture((Keyer.Display)i, m_PlotCaptureRT[i]);
                                plot.Source = m_PlotCaptureRT[i];
                            }
                        }
                    }
                }
                else if (m_Plots[i] != null)
                {
                    m_PlotRenderer.RemovePlot(m_Plots[i]);
                    m_Plots[i] = null;
                    m_Keyer.RemoveCapture((Keyer.Display)i, m_PlotCaptureRT[i]);
                    Utilities.DeallocateIfNeeded(ref m_PlotCaptureRT[i]);
                }
            }

            m_NeedsRenderPlot = true;
        }

        void RemoveAllPlots()
        {
            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                if (m_Plots[i] != null)
                {
                    m_PlotRenderer.RemovePlot(m_Plots[i]);
                    m_Plots[i] = null;
                }
            }
        }

        public void OnEnable()
        {
            RegisterKeyerChangeIfNeeded();
            m_PlotRenderer.Initialize();
            m_PlotRenderer.SetPickingEnabled(true);
        }

        public void OnDisable()
        {
            UnInitializePlotsAndDisplayCapture();
            m_PlotRenderer.Dispose();
            Utilities.DeallocateIfNeeded(ref m_PlotRenderTarget);
            UnregisterKeyerChangeIfNeeded();
        }

        void Render()
        {
            m_StartPoint.x = Mathf.Clamp01(m_StartPoint.x);
            m_StartPoint.y = Mathf.Clamp01(m_StartPoint.y);
            m_EndPoint.x = Mathf.Clamp01(m_EndPoint.x);
            m_EndPoint.y = Mathf.Clamp01(m_EndPoint.y);

            for (var i = 0; i < k_MaxIntermediatesResults; ++i)
            {
                if (m_Plots[i] != null)
                {
                    if (i == (int)Keyer.Display.Front || i == (int)Keyer.Display.Result || i == (int)Keyer.Display.Despill)
                    {
                        var rgbPlot = m_Plots[i] as PlotRenderer.IRgbPlot;
                        rgbPlot.StartPoint = m_StartPoint;
                        rgbPlot.EndPoint = m_EndPoint;
                    }
                    else
                    {
                        var plot = m_Plots[i] as PlotRenderer.ISingleChannelPlot;
                        plot.StartPoint = m_StartPoint;
                        plot.EndPoint = m_EndPoint;
                    }
                }
            }

            var cmd = CommandBufferPool.Get("Plot");
            Utilities.AllocateIfNeeded(ref m_PlotRenderTarget, (int)m_PlotRect.width, (int)m_PlotRect.height);

            if (m_SelectedPlots != UIUtils.DisplayFlags.None)
            {
                m_PlotRenderer.Render(cmd, m_PlotRenderTarget, (int)m_PlotRect.width);
            }
            else
            {
                cmd.SetRenderTarget(m_PlotRenderTarget);
                cmd.ClearRenderTarget(false, true, Color.black);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }
}
