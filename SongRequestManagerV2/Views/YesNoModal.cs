using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using System;
using System.Reflection;
using TMPro;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class YesNoModal : BSMLAutomaticViewController
    {
        public static YesNoModal instance { get; private set; }

        private Action OnConfirm;
        private Action OnDecline;

        [UIComponent("modal")]
        internal ModalView modal;

        [UIComponent("title")]
        internal TextMeshProUGUI _title;

        [UIComponent("message")]
        internal TextMeshProUGUI _message;

        public string ResourceName => "SongRequestManagerV2.Views.YesNoModal.bsml";

        [UIAction("yes-click")]
        private void YesClick()
        {
            modal.Hide(true);
            OnConfirm?.Invoke();
            OnConfirm = null;
        }

        [UIAction("no-click")]
        private void NoClick()
        {
            modal.Hide(true);
            OnDecline?.Invoke();
            OnDecline = null;
        }

        private void Awake()
        {
            instance = this;
        }

        public void ShowDialog(string title, string message, Action onConfirm = null, Action onDecline = null)
        {
            _title.text = title;
            _message.text = message;

            OnConfirm = onConfirm;
            OnDecline = onDecline;

            modal.Show(true);
        }
    }
}
