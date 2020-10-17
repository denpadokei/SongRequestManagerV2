using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using SongRequestManagerV2.UI;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2
{
    public class RequestFlowCoordinator : FlowCoordinator
    {
        [Inject]
        private RequestBotListViewController _requestBotListViewController;
        [Inject]
        private KeyboardViewController _keyboardViewController;
        [Inject]
        private SoloFreePlayFlowCoordinator _soloFreePlayFlow;

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            base.BackButtonWasPressed(topViewController);
            // dismiss ourselves
            _soloFreePlayFlow.DismissFlowCoordinator(this);
        }

        public void SetTitle(string newTitle) => base.SetTitle(newTitle);

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation) {
                base.SetTitle("Song Request Manager");
                showBackButton = true;
                try {
                    ProvideInitialViewControllers(_requestBotListViewController, _keyboardViewController);
                }
                catch (System.Exception e) {
                    Plugin.Logger.Error(e);
                }
            }
        }
    }
}
