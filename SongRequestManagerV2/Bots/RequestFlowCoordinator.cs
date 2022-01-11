using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using SongRequestManagerV2.Views;
using System;
using Zenject;

namespace SongRequestManagerV2
{
    public class RequestFlowCoordinator : FlowCoordinator, IInitializable, IDisposable
    {
        [Inject]
        private readonly RequestBotListView _requestBotListViewController;
        [Inject]
        private readonly KeyboardViewController _keyboardViewController;
        private bool disposedValue;

        public event Action<SongRequest, bool> PlayProcessEvent;
        public event Action QueueStatusChanged;

        public void RefreshSongList(bool obj)
        {
            this._requestBotListViewController.RefreshSongQueueList(obj);
        }

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

        private void OnPlayProcessEvent(SongRequest arg1, bool arg2)
        {
            PlayProcessEvent?.Invoke(arg1, arg2);
        }

        private void OnChangeTitle(string obj)
        {
            this.SetTitle(obj);
        }

        private void OnRequestBotListViewController_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is RequestBotListView view && e.PropertyName == nameof(view.QueueButtonText)) {
                QueueStatusChanged?.Invoke();
            }
        }

        public void ChangeProgressText(double value)
        {
            this._requestBotListViewController.ChangeProgressText(value);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation) {
                this.SetTitle("Song Request Manager");
                this.showBackButton = true;
                try {
                    this.ProvideInitialViewControllers(this._requestBotListViewController, null, this._keyboardViewController);
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
            }
        }
        public void Initialize()
        {
            this._requestBotListViewController.ChangeTitle += this.OnChangeTitle;
            this._requestBotListViewController.PlayProcessEvent += this.OnPlayProcessEvent;
            this._requestBotListViewController.PropertyChanged += this.OnRequestBotListViewController_PropertyChanged;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue) {
                if (disposing) {
                    this._requestBotListViewController.ChangeTitle -= this.OnChangeTitle;
                    this._requestBotListViewController.PlayProcessEvent -= this.OnPlayProcessEvent;
                    this._requestBotListViewController.PropertyChanged -= this.OnRequestBotListViewController_PropertyChanged;
                }
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
