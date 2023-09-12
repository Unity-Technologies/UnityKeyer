
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    interface IPreviewTab
    {
        void OnEnable();
        void OnDisable();
        void SetKeyer(Keyer newKeyer);
        VisualElement CreateGUI();
    }
}
