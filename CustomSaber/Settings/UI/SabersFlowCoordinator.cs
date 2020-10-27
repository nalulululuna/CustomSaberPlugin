using BeatSaberMarkupLanguage;
using HMUI;
using System;

namespace CustomSaber.Settings.UI
{
    internal class SabersFlowCoordinator : FlowCoordinator
    {
        private SaberListViewController saberListView;
        private SaberPreviewViewController saberPreviewView;
        private SaberSettingsViewController saberSettingsView;

        public void Awake()
        {
            if (!saberPreviewView)
            {
                saberPreviewView = BeatSaberUI.CreateViewController<SaberPreviewViewController>();
            }

            if (!saberSettingsView)
            {
                saberSettingsView = BeatSaberUI.CreateViewController<SaberSettingsViewController>();
            }

            if (!saberListView)
            {
                saberListView = BeatSaberUI.CreateViewController<SaberListViewController>();
                saberListView.customSaberChanged += saberPreviewView.OnSaberWasChanged;
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            try
            {
                if (firstActivation)
                {
                    SetTitle("Custom Sabers");
                    showBackButton = true;
                    ProvideInitialViewControllers(saberListView, saberSettingsView, saberPreviewView);

                    var grabber = new UnityEngine.GameObject("Default Saber Grabber").AddComponent<CustomSaber.Utilities.DefaultSaberGrabber>();
                    DontDestroyOnLoad(grabber);
                }
            }
            catch (Exception ex)
            {
                Logger.log.Error(ex);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            // Dismiss ourselves
            BeatSaberUI.MainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
