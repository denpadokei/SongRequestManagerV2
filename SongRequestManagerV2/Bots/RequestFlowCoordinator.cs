using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using SongRequestManagerV2.Views;
using System;
using Zenject;

namespace SongRequestManagerV2
{
    public class RequestFlowCoordinator : FlowCoordinator
    {
        [Inject]
        private readonly RequestBotListView _requestBotListViewController;
        [Inject]
        private readonly KeyboardViewController _keyboardViewController;

        public event Action<SongRequest, bool> PlayProcessEvent;
        public event Action QueueStatusChanged;

        public void RefreshSongList(bool obj) => this._requestBotListViewController.RefreshSongQueueList(obj);

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            try {
                var parent = this.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator");
                if (parent != null) {
                    this._keyboardViewController.DeactivateGameObject();
                    parent.DismissFlowCoordinator(this);
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            
            base.BackButtonWasPressed(topViewController);
        }

        [Inject]
        public void Const()
        {
            this._requestBotListViewController.ChangeTitle += s => this.SetTitle(s);
            this._requestBotListViewController.PlayProcessEvent += (i, b) => this.PlayProcessEvent?.Invoke(i, b);
            this._requestBotListViewController.PropertyChanged += this.OnRequestBotListViewController_PropertyChanged;
        }

        private void OnDestroy()
        {
            this._requestBotListViewController.ChangeTitle -= s => this.SetTitle(s);
            this._requestBotListViewController.PlayProcessEvent -= (i, b) => this.PlayProcessEvent?.Invoke(i, b);
            this._requestBotListViewController.PropertyChanged -= this.OnRequestBotListViewController_PropertyChanged;
        }

        private void OnRequestBotListViewController_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is RequestBotListView view && e.PropertyName == nameof(view.QueueButtonText)) {
                this.QueueStatusChanged?.Invoke();
            }
        }

        public void ChangeProgressText(double value) => this._requestBotListViewController.ChangeProgressText(value);

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation) {
                this.SetTitle("Song Request Manager");
                this.showBackButton = true;
                try {
                    this.ProvideInitialViewControllers(this._requestBotListViewController, null, this._keyboardViewController);
                }
                catch (System.Exception e) {
                    Logger.Error(e);
                }
            }
        }
    }
}
