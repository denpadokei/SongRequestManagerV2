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
using SongRequestManagerV2.UI;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SongRequestManagerV2.Views
{
    [HotReload]
    public class SRMButton : BSMLAutomaticViewController
    {
        // For this method of setting the ResourceName, this class must be the first class in the file.
        [Inject]
        public MainFlowCoordinator _mainFlowCoordinator;
        [Inject]
        public SoloFreePlayFlowCoordinator _soloFreeFlow;
        [Inject]
        public LevelCollectionNavigationController _levelCollectionNavigationController;
        [Inject]
        public RequestFlowCoordinator _requestFlow;
        [Inject]
        public SearchFilterParamsViewController _searchFilterParamsViewController;
        [Inject]
        private RequestBot _bot;
        //[Inject]
        //protected PhysicsRaycasterWithCache _physicsRaycaster;
        public HMUI.Screen Screen { get; set; }

        public FlowCoordinator Current => _mainFlowCoordinator.YoungestChildFlowCoordinatorOrSelf();

        public string ResourceName => string.Join(".", GetType().Namespace, "SRMButton.bsml");

        public static SRMButton instance { get; private set; }

        protected void Awake()
        {
            instance = this;
        }
        [UIAction("action")]
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
            if (Current.name != _soloFreeFlow.name) {
                return;
            }
            Current.PresentFlowCoordinator(_requestFlow, null, AnimationDirection.Horizontal, false, false);
        }

        internal void SetButtonColor(Color color)
        {
            //this.ButtonColor = color;
            Plugin.Logger.Debug($"Change button color : {color}");
            this.button.GetComponentsInChildren<ImageView>(true).First(x => x.name == "BG").color = color;
            this.button.GetComponentsInChildren<ImageView>(true).First(x => x.name == "BG").color0 = color;
            this.button.GetComponentsInChildren<ImageView>(true).First(x => x.name == "BG").color1 = color;
            this.button.interactable = true;
            //this.button.colors = new ColorBlock()
            //{
            //    colorMultiplier = button.colors.colorMultiplier,
            //    disabledColor = button.colors.disabledColor,
            //    fadeDuration = button.colors.fadeDuration,
            //    highlightedColor = button.colors.highlightedColor,
            //    normalColor = color,
            //    pressedColor = button.colors.pressedColor,
            //    selectedColor = button.colors.selectedColor
            //};
        }

        internal void BackButtonPressed()
        {
            Plugin.Logger.Debug($"{Current.name} : {_requestFlow.name}");
            if (Current.name != _requestFlow.name) {
                Plugin.Logger.Debug($"{Current.name != _requestFlow.name}");
                return;
            }
            try {
                Current.GetField<FlowCoordinator, FlowCoordinator>("_parentFlowCoordinator")?.DismissFlowCoordinator(Current, null, AnimationDirection.Horizontal, true);
            }
            catch (Exception e) {
                Plugin.Logger.Error(e);
            }
        }

        [Inject]
        public void Setup()
        {
            Plugin.Logger.Debug("Setup()");
            _bot.ChangeButtonColor += this.SetButtonColor;
            _bot.DismissRequest += this.BackButtonPressed;
            this.Screen = FloatingScreen.CreateFloatingScreen(new Vector2(20f, 20f), false, new Vector3(1.2f, 2.2f, 2.2f), Quaternion.Euler(Vector3.zero));
            button = UIHelper.CreateUIButton(this.Screen.transform, "OkButton", Vector2.zero, Vector2.zero, Action, "SRM", null);
            var canvas = this.Screen.GetComponent<Canvas>();
            canvas.sortingOrder = 3;
            this.Screen.SetRootViewController(this, AnimationType.None);
            Plugin.Logger.Debug($"{button == null}");

            Plugin.Log("Created request button!");
            
            Plugin.Logger.Debug("Setup() end");
        }
        //[UIComponent("srm-button")]
        private Button button;
    }
}
