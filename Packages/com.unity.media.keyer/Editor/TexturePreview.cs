using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class TexturePreview : VisualElement
    {
        public struct Transform
        {
            public Vector2 Translation;
            public float Scale;

            public Transform Translate(Vector2 translation)
            {
                return new Transform
                {
                    Translation = Translation + translation,
                    Scale = Scale
                };
            }

            public Transform ScaleFromOrigin(float scale)
            {
                return new Transform
                {
                    Translation = Translation * scale,
                    Scale = Scale * scale
                };
            }

            public Transform Normalize(Vector2 size)
            {
                return new Transform
                {
                    Translation = Translation / size,
                    Scale = Scale
                };
            }

            public Transform DeNormalize(Vector2 size)
            {
                return new Transform
                {
                    Translation = Translation * size,
                    Scale = Scale
                };
            }

            public Rect Apply(Rect rect)
            {
                return new(rect.position + Translation, rect.size * Scale);
            }
        }

        const int k_DefaultMinViewerWidth = 200;
        const float k_ZoomSpeed = 0.1f;
        const float k_MinScale = .5f;
        const float k_MaxScale = 15;

        static readonly Transform k_IdentityTransform = new Transform
        {
            Translation = Vector2.zero,
            Scale = 1
        };

        Rect m_ViewportRect;
        Transform m_Transform;

        public event Action<Transform> TransformChanged = delegate { };
        public event Action ModifierPressed = delegate { };

        public Texture Texture { get; set; }

        public bool Transparent { get; set; }

        public void SetNormalizedTransform(Transform trs)
        {
            m_Transform = trs.DeNormalize(m_ViewportRect.size);
        }

        public TexturePreview()
        {
            m_Transform = k_IdentityTransform;
            style.flexGrow = 1;
            style.minWidth = k_DefaultMinViewerWidth;
            var viewPort = new IMGUIContainer(OnGUI);
            viewPort.StretchToParentSize();
            viewPort.style.flexGrow = 1;
            Add(viewPort);
        }

        void OnGUI()
        {
            // This will clip drawing to the viewport
            m_ViewportRect = new Rect(0, 0, layout.width, layout.height);
            GUI.BeginGroup(m_ViewportRect);

            var transformChanged = false;
            var evt = Event.current;

            switch (evt.type)
            {
                case EventType.MouseDrag:
                {
                    m_Transform.Translation += evt.delta;
                    transformChanged = true;
                    break;
                }
                case EventType.ScrollWheel:
                {
                    var mousePos = evt.mousePosition;
                    var zoom = -evt.delta.y * k_ZoomSpeed + 1.0f;
                    zoom = Mathf.Clamp(zoom, k_MinScale / m_Transform.Scale, k_MaxScale / m_Transform.Scale);
                    m_Transform = m_Transform.Translate(-mousePos).ScaleFromOrigin(zoom).Translate(mousePos);
                    transformChanged = true;
                    break;
                }
            }

            // Local repaint if Alt is pressed.
            // Otherwise dispatch updated transform so that it is applied to all previews.
            if (transformChanged)
            {
                // Constrain translation to keep the image on screen.
                var size = Texture == null ? Vector2.one * 512 : new Vector2(Texture.width, Texture.height);
                var imageRect = ScaleToFit(new Rect(Vector2.zero, m_ViewportRect.size * m_Transform.Scale), size);
                // Add a 12px margin so that the image does not go out of screen completely.
                const float margin = 12;
                var minTranslation = -imageRect.max + Vector2.one * margin;
                var maxTranslation = m_ViewportRect.size - imageRect.min - Vector2.one * margin;

                m_Transform.Translation = new Vector2(
                    Mathf.Clamp(m_Transform.Translation.x, minTranslation.x, maxTranslation.x),
                    Mathf.Clamp(m_Transform.Translation.y, minTranslation.y, maxTranslation.y));

                evt.Use();
                if (evt.alt)
                {
                    ModifierPressed.Invoke();
                }
                else
                {
                    TransformChanged.Invoke(m_Transform.Normalize(m_ViewportRect.size));
                }
            }

            if (Texture != null)
            {
                var rect = ScaleToFit(m_Transform.Apply(m_ViewportRect), new Vector2(Texture.width, Texture.height));

                if (Transparent)
                {
                    EditorGUI.DrawTextureTransparent(rect, Texture);
                }
                else
                {
                    EditorGUI.DrawPreviewTexture(rect, Texture);
                }
            }

            GUI.EndGroup();
        }

        static Rect ScaleToFit(Rect rect, Vector2 size)
        {
            var aspect = size.x / size.y;
            if (aspect > 1)
            {
                return new Rect(rect.x, rect.y + (rect.height - rect.width / aspect) * .5f, rect.width, rect.width / aspect);
            }

            return new Rect(rect.x + (rect.width - rect.height * aspect) * .5f, rect.y, rect.height * aspect, rect.height);
        }
    }
}
