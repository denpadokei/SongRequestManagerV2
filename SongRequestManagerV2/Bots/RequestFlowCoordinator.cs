using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using SongRequestManagerV2.Views;
using System;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SongRequestManagerV2
{
    public class RequestFlowCoordinator : FlowCoordinator
    {
        [Inject]
        private RequestBotListView _requestBotListViewController;
        [Inject]
        private KeyboardViewController _keyboardViewController;

        public event Action<int, bool> PlayProcessEvent;

        public void RefreshSongList(bool obj) => _requestBotListViewController.RefreshSongQueueList(obj);

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            base.BackButtonWasPressed(topViewController);
            this.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator").DismissFlowCoordinator(this);
        }

        [Inject]
        public void Const()
        {
            _requestBotListViewController.ChangeTitle += s => this.SetTitle(s);
            _requestBotListViewController.PlayProcessEvent += (i, b) => this.PlayProcessEvent?.Invoke(i, b);
        }

        public void ChangeProgressText(double value) => this._requestBotListViewController.ChangeProgressText(value);

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation) {
                this.SetTitle("Song Request Manager");
                showBackButton = true;
                try {
                    ProvideInitialViewControllers(_requestBotListViewController, null, _keyboardViewController);
                }
                catch (System.Exception e) {
                    Logger.Error(e);
                }
            }
        }
    }
}
