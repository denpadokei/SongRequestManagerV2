using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using IPA.Utilities;
using SongPlayListEditer.Bases;
using SongRequestManagerV2.UI;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Views
{
    public class SRMButton : ViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        [Inject]
        public SoloFreePlayFlowCoordinator _soloFreeFlow;
        [Inject]
        public LevelCollectionNavigationController _levelCollectionNavigationController;
        [Inject]
        public RequestFlowCoordinator _requestFlow;
        [Inject]
        public SearchFilterParamsViewController _searchFilterParamsViewController;

        public string ResourceName => string.Join(".", GetType().Namespace, "SRMButton.bsml");

        public static SRMButton instance;

        protected void Awake()
        {
            instance = this;
        }

        public void Action()
        {
            try {
                Plugin.Log("action");
                button.interactable = false;
                SRMButtonPressed();
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
            finally {
                button.interactable = true;
            }
            
        }

        internal void SRMButtonPressed()
        {
            _soloFreeFlow.PresentFlowCoordinator(_requestFlow);
            //_soloFreeFlow.InvokeMethod<object, SoloFreePlayFlowCoordinator>("PresentFlowCoordinator", _requestFlow, null, ViewController.AnimationDirection.Horizontal, false, false);
        }

        [Inject]
        public void Setup()
        {
            Plugin.Logger.Debug("Setup()");
            
            //var navi = Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>().First();
            //if (navi) {
            if (_levelCollectionNavigationController) {
                //new Vector2(9f, 5.5f)

                //BSMLParser.instance.Parse(Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), this.ResourceName), navi.gameObject, this);
                //button = UIHelper.CreateUIButton(_levelSelectionNavigationController.rectTransform, "OkButton", new Vector2(50f, 23f),
                //        Vector2.zero, () => { button.interactable = false; Action(); button.interactable = true; }, "SRM", null);
                
                //_levelCollectionNavigationController.rectTransform.sizeDelta = new Vector2(_levelCollectionNavigationController.rectTransform.sizeDelta.x, _levelCollectionNavigationController.rectTransform.sizeDelta.y);
                _searchFilterParamsViewController.GetComponent<VRGraphicRaycaster>().enabled = false;
                button = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "OkButton"), (_levelCollectionNavigationController.transform as RectTransform));
                button.SetButtonText("SRM");
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(Action);
                (button.transform as RectTransform).anchoredPosition = new Vector2(120f, 42f);
                (button.transform as RectTransform).sizeDelta = new Vector2((button.transform as RectTransform).sizeDelta.x, 8f);

                Plugin.Logger.Debug($"{button == null}");
                foreach (var item in button?.GetComponentsInChildren<object>()) {
                    Plugin.Logger.Debug($"{item}");
                }
                //(button.transform as RectTransform).anchorMin = new Vector2(1, 1);
                //(button.transform as RectTransform).anchorMax = new Vector2(1, 1);

                //_requestButton.ToggleWordWrapping(false);
                //_requestButton.SetButtonTextSize(3.5f);
                //UIHelper.AddHintText(_requestButton.transform as RectTransform, "Manage the current request queue");

                Plugin.Log("Created request button!");
            }
            Plugin.Logger.Debug("Setup() end");
        }
        public Button button;

        //[UIObject("root-object")]
        //private GameObject root;
    }
}
