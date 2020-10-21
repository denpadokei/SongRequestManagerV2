using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SongRequestManagerV2.Extentions;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongRequestManagerV2.UI
{
    public class SRMButton : ViewController
    {
        private static RequestFlowCoordinator _flowCoordinator;
        private static RequestBot _bot;
        private static Button _srmButton;
        private static SoloFreePlayFlowCoordinator _soloFlowCoordinator;
        private static LevelCollectionViewController _levelCollectionViewController;

        internal void SRMButtonPressed()
        {
            try {
                Plugin.Logger.Debug("Pless SRM Button");
                var soloFlow = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
                Plugin.Logger.Debug($"soloFlow : {soloFlow == null}");
                Plugin.Logger.Debug($"ReqFlow : {_flowCoordinator == null}");
                _flowCoordinator.SetField<FlowCoordinator, UnityEngine.EventSystems.BaseInputModule>("_baseInputModule", _soloFlowCoordinator.GetField<UnityEngine.EventSystems.BaseInputModule, FlowCoordinator>("_baseInputModule"));
                soloFlow.InvokeMethod("PresentFlowCoordinator", _flowCoordinator, null, ViewController.AnimationDirection.Horizontal, false, false);
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
        }

        public void UpdateRequestUI(bool writeSummary = true)
        {
            Plugin.Log("start updateUI");
            try {
                if (writeSummary)
                    RequestBot.WriteQueueSummaryToFile(); // Write out queue status to file, do it first

                Dispatcher.RunOnMainThread(() =>
                {
                    //this.interactable = true;

                    if (RequestQueue.Songs.Count == 0) {
                        this.gameObject.GetComponentInChildren<Image>().color = Color.red;
                    }
                    else {
                        this.gameObject.GetComponentInChildren<Image>().color = Color.green;
                    }
                });
            }
            catch (Exception ex) {
                Plugin.Log(ex.ToString());
            }
            finally {
                Plugin.Log("end update UI");
            }
        }


        [Inject]
        public void Constraactor(SoloFreePlayFlowCoordinator soloFreePlayFlowCoordinator, LevelCollectionViewController levelCollectionViewController, RequestFlowCoordinator requestFlowCoordinator, RequestBot bot)
        {
            _soloFlowCoordinator = soloFreePlayFlowCoordinator;
            _levelCollectionViewController = levelCollectionViewController;
            _flowCoordinator = requestFlowCoordinator;
            _bot = bot;
            _bot.RecevieRequest += this._bot_RecevieRequest;

            _bot.Dismiss += this.Bot_Dismiss;
            _bot.UpdateUI += this._bot_UpdateUI;
            this.transform.parent = _levelCollectionViewController.rectTransform;
        }

        private void _bot_UpdateUI()
        {
            this.UpdateRequestUI();
        }

        private void _bot_RecevieRequest()
        {
            this.SRMButtonPressed();
        }

        protected void Awake()
        {
            _srmButton.onClick = new Button.ButtonClickedEvent();
            if (_srmButton.onClick != null)
                _srmButton.onClick.AddListener(this.SRMButtonPressed);
            this.name = "VersusUIButton";

            (this.transform as RectTransform).anchorMin = new Vector2(0.5f, 0.5f);
            (this.transform as RectTransform).anchorMax = new Vector2(0.5f, 0.5f);
            (this.transform as RectTransform).anchoredPosition = new Vector2(80f, -3.5f);
            (this.transform as RectTransform).sizeDelta = new Vector2(9f, 5.5f);

            _srmButton.SetButtonText("SRM");
        }

        private void _bot_ChangeTitle(string obj)
        {
            _flowCoordinator.SetFlowTitle(obj);
        }

        private void Bot_Dismiss()
        {
            _flowCoordinator.Dismiss();
        }
    }
}
