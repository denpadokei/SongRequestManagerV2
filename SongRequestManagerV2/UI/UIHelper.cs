using HMUI;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using IPA.Utilities;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using TMPro;

namespace SongRequestManagerV2.UI
{
    internal class UIHelper : MonoBehaviour
    {
        public static HoverHint AddHintText(RectTransform parent, string text)
        {
            var hoverHint = parent.gameObject.AddComponent<HoverHint>();
            hoverHint.text = text;
            var hoverHintController = Resources.FindObjectsOfTypeAll<HoverHintController>().First();
            hoverHint.SetField("_hoverHintController", hoverHintController);
            return hoverHint;
        }

        public static Button CreateUIButton(Transform parent, string buttonTemplate, Vector2 anchoredPosition, Vector2 sizeDelta, UnityAction onClick = null, string buttonText = "BUTTON", Sprite icon = null, Button origin = null)
        {

            Button btn = origin == null ? Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == buttonTemplate)), parent) : origin;
            btn.onClick = new Button.ButtonClickedEvent();
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            btn.name = "VersusUIButton";

            btn.interactable = true;
            Polyglot.LocalizedTextMeshProUGUI localizer = btn.GetComponentInChildren<Polyglot.LocalizedTextMeshProUGUI>();
            if (localizer != null)
                GameObject.Destroy(localizer);

            //CurvedTextMeshPro textMeshPro = btn.GetComponentInChildren<CurvedTextMeshPro>();
            //if (textMeshPro != null)
            //    GameObject.Destroy(textMeshPro);
            ExternalComponents externalComponents = btn.gameObject.GetComponent<ExternalComponents>();
            TextMeshProUGUI textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
            textMesh.richText = true;
            btn.SetButtonText(buttonText);
            if (!string.IsNullOrEmpty(buttonText)) {
                textMesh.text = buttonText;
            }
            externalComponents.components.Add(textMesh);
            StackLayoutGroup stackLayoutGroup = externalComponents.GetComponentInChildren<StackLayoutGroup>();
            if (stackLayoutGroup == null)
                externalComponents.components.Add(stackLayoutGroup);

            (btn.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (btn.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (btn.transform as RectTransform).anchoredPosition = anchoredPosition;
            if (sizeDelta != Vector2.zero) {
                (btn.transform as RectTransform).sizeDelta = sizeDelta;
            }
            //btn.SetButtonText(buttonText);
            //if (icon != null)
            //    btn.SetButtonIcon(icon);

            return btn;
        }

        /// <summary>
        /// Clone a Unity Button into a Button we control.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="buttonTemplate"></param>
        /// <param name="buttonInstance"></param>
        /// <returns></returns>
        static public Button CreateUIButton(Transform parent, Button buttonTemplate, string name = "")
        {
            Button btn = UnityEngine.Object.Instantiate(buttonTemplate, parent);
            //UnityEngine.Object.DestroyImmediate(btn.GetComponent<SignalOnUIButtonClick>());
            btn.onClick = new Button.ButtonClickedEvent();
            btn.name = string.IsNullOrEmpty(name) ? "CustomUIButton" : name;
            btn.interactable = true;
            Polyglot.LocalizedTextMeshProUGUI localizer = btn.GetComponentInChildren<Polyglot.LocalizedTextMeshProUGUI>();
            if (localizer != null)
                GameObject.Destroy(localizer);
            //CurvedTextMeshPro textMeshPro = btn.GetComponentInChildren<CurvedTextMeshPro>();
            //if (textMeshPro != null)
            //    GameObject.Destroy(textMeshPro);
            //ExternalComponents externalComponents = btn.gameObject.GetComponent<ExternalComponents>();
            TextMeshProUGUI textMesh = btn.GetComponentInChildren<TextMeshProUGUI>();
            textMesh.richText = true;
            if (!string.IsNullOrEmpty(name)) {
                textMesh.text = name;
            }
            //externalComponents.components.Add(textMesh);
            //StackLayoutGroup stackLayoutGroup = btn.GetComponentInChildren<StackLayoutGroup>();
            //if (stackLayoutGroup != null)
            //   externalComponents.components.Add(stackLayoutGroup);

            return btn;
        }

        private static Sprite _blankSprite = null;
        public static Sprite BlankSprite
        {
            get
            {
                if (!_blankSprite)
                    _blankSprite = Sprite.Create(Texture2D.blackTexture, new Rect(), Vector2.zero);
                return _blankSprite;
            }
        }
    }
}
