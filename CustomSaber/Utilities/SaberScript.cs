using BS_Utils.Gameplay;
using CustomSaber.Data;
using CustomSaber.Settings;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CustomSaber.Utilities
{
    internal class SaberScript : MonoBehaviour
    {
        // CustomSabers
        private GameObject sabers;
        private GameObject leftSaber;
        private GameObject rightSaber;

        private float lastNoteTime;
        private bool playerHeadWasInObstacle;
        private ColorManager colorManager;

        // EventManagers
        private EventManager leftEventManager;
        private EventManager rightEventManager;

        // Controllers
        private BeatmapObjectManager beatmapObjectManager;
        private ScoreController scoreController;
        private ObstacleSaberSparkleEffectManager saberCollisionManager;
        private GameEnergyCounter gameEnergyCounter;
        private BeatmapObjectCallbackController beatmapCallback;
        private PlayerHeadAndObstacleInteraction playerHeadAndObstacleInteraction;
        private PauseController pauseController;

        public static SaberScript instance;

        /// <summary>
        /// Load the Saber swapper script
        /// </summary>
        public static void Load()
        {
            if (instance != null)
            {
                Destroy(instance.leftSaber);
                Destroy(instance.rightSaber);
                Destroy(instance.sabers);
                Destroy(instance.gameObject);
            }

            GameObject loader = new GameObject("Saber Loader");
            instance = loader.AddComponent<SaberScript>();
        }

        public void Restart()
        {
            CancelInvoke("_Restart");
            Invoke("_Restart", 0.5f);
        }

        private void _Restart()
        {
            OnDestroy();

            if (sabers != null)
            {
                pauseController = FindObjectsOfType<PauseController>().FirstOrDefault();
                pauseController.didResumeEvent -= OnPauseResume;
                pauseController.didResumeEvent += OnPauseResume;
            }

            if (sabers && Configuration.CustomEventsEnabled)
            {
                AddEvents();
            }
        }

        private void Start()
        {
            Restart();
        }

        private void AddEvents()
        {
            leftEventManager = leftSaber?.GetComponent<EventManager>();
            if (!leftEventManager)
            {
                leftEventManager = leftSaber.AddComponent<EventManager>();
            }

            rightEventManager = rightSaber?.GetComponent<EventManager>();
            if (!rightEventManager)
            {
                rightEventManager = rightSaber.AddComponent<EventManager>();
            }

            if (leftEventManager?.OnLevelStart == null
                || rightEventManager?.OnLevelStart == null)
            {
                return;
            }

            leftEventManager.OnLevelStart.Invoke();
            rightEventManager.OnLevelStart.Invoke();

            try
            {
                scoreController = FindObjectsOfType<ScoreController>().FirstOrDefault();
                if (scoreController)
                {
                    scoreController.multiplierDidChangeEvent += MultiplierCallBack;
                    scoreController.comboDidChangeEvent += ComboChangeEvent;
                }
                else
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(ScoreController)}'.");
                }

                beatmapObjectManager = scoreController.GetField<BeatmapObjectManager, ScoreController>("_beatmapObjectManager");
                if (beatmapObjectManager != null)
                {
                    beatmapObjectManager.noteWasCutEvent += SliceCallBack;
                    beatmapObjectManager.noteWasMissedEvent += NoteMissCallBack;
                }
                else
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(BeatmapObjectManager)}'.");
                }

                saberCollisionManager = Resources.FindObjectsOfTypeAll<ObstacleSaberSparkleEffectManager>().FirstOrDefault();
                if (saberCollisionManager)
                {
                    saberCollisionManager.sparkleEffectDidStartEvent += SaberStartCollide;
                    saberCollisionManager.sparkleEffectDidEndEvent += SaberEndCollide;
                }
                else
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(ObstacleSaberSparkleEffectManager)}'.");
                }

                gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().FirstOrDefault();
                if (gameEnergyCounter)
                {
                    gameEnergyCounter.gameEnergyDidReach0Event += FailLevelCallBack;
                }
                else
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(GameEnergyCounter)}'.");
                }

                beatmapCallback = Resources.FindObjectsOfTypeAll<BeatmapObjectCallbackController>().FirstOrDefault();
                if (beatmapCallback)
                {
                    beatmapCallback.beatmapEventDidTriggerEvent += LightEventCallBack;
                }
                else
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(BeatmapObjectCallbackController)}'.");
                }

                playerHeadAndObstacleInteraction = scoreController.GetField<PlayerHeadAndObstacleInteraction, ScoreController>("_playerHeadAndObstacleInteraction");
                if (playerHeadAndObstacleInteraction == null)
                {
                    Logger.log.Warn($"Failed to locate a suitable '{nameof(PlayerHeadAndObstacleInteraction)}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.log.Error(ex);
                throw;
            }

            try
            {
                float LastTime = 0.0f;
                LevelData levelData = BS_Utils.Plugin.LevelData;
                BeatmapData beatmapData = levelData.GameplayCoreSceneSetupData.difficultyBeatmap.beatmapData;

                IReadOnlyList<IReadonlyBeatmapLineData> beatmapLinesData = beatmapData.beatmapLinesData;
                foreach (BeatmapLineData beatMapLineData in beatmapLinesData)
                {
                    IReadOnlyList<BeatmapObjectData> beatmapObjectsData = beatMapLineData.beatmapObjectsData;
                    for (int i = beatmapObjectsData.Count - 1; i >= 0; i--)
                    {
                        BeatmapObjectData beatmapObjectData = beatmapObjectsData[i];
                        if (beatmapObjectData.beatmapObjectType == BeatmapObjectType.Note
                            && ((NoteData)beatmapObjectData).colorType != global::ColorType.None)
                        {
                            if (beatmapObjectData.time > LastTime)
                            {
                                LastTime = beatmapObjectData.time;
                            }
                        }
                    }
                }

                lastNoteTime = LastTime;
            }
            catch (Exception ex)
            {
                Logger.log.Error(ex);
                throw;
            }
        }

        private void OnPauseResume()
        {
            foreach (var saberTrailRenderer in Resources.FindObjectsOfTypeAll<SaberTrailRenderer>())
            {
                Logger.log.Debug("saberTrailRenderer");
                saberTrailRenderer.enabled = true;
            }
        }

        private void OnDestroy()
        {
            if (pauseController != null)
            {
                pauseController.didResumeEvent += OnPauseResume;
            }

            if (beatmapObjectManager != null)
            {
                beatmapObjectManager.noteWasCutEvent -= SliceCallBack;
                beatmapObjectManager.noteWasMissedEvent -= NoteMissCallBack;
            }

            if (scoreController)
            {
                scoreController.multiplierDidChangeEvent -= MultiplierCallBack;
                scoreController.comboDidChangeEvent -= ComboChangeEvent;
            }

            if (saberCollisionManager)
            {
                saberCollisionManager.sparkleEffectDidStartEvent -= SaberStartCollide;
                saberCollisionManager.sparkleEffectDidEndEvent -= SaberEndCollide;
            }

            if (gameEnergyCounter)
            {
                gameEnergyCounter.gameEnergyDidReach0Event -= FailLevelCallBack;
            }

            if (beatmapCallback)
            {
                beatmapCallback.beatmapEventDidTriggerEvent -= LightEventCallBack;
            }
        }

        private void Awake()
        {
            if (sabers)
            {
                Destroy(sabers);
                sabers = null;
            }

            colorManager = Resources.FindObjectsOfTypeAll<ColorManager>().LastOrDefault();

            ResetVanillaTrails();

            CustomSaberData customSaber = (Configuration.RandomSabersEnabled) ? SaberAssetLoader.GetRandomSaber() : SaberAssetLoader.CustomSabers[SaberAssetLoader.SelectedSaber];
            if (customSaber != null)
            {
                if (customSaber.FileName == "DefaultSabers")
                {
                    StartCoroutine(WaitToCheckDefault());
                }
                else
                {
                    Logger.log.Debug("Replacing sabers");

                    if (customSaber.Sabers)
                    {
                        sabers = Instantiate(customSaber.Sabers);
                        rightSaber = sabers?.transform.Find("RightSaber").gameObject;
                        leftSaber = sabers?.transform.Find("LeftSaber").gameObject;
                    }

                    StartCoroutine(WaitForSabers(customSaber.Sabers));
                }
            }
        }

        private IEnumerator WaitForSabers(GameObject saberRoot)
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<Saber>().Any());

            if (Configuration.TrailType == TrailType.None)
            {
                HideVanillaTrails();
            }

            IEnumerable<Saber> defaultSabers = FindObjectsOfType<Saber>();
            foreach (Saber defaultSaber in defaultSabers)
            {
                Logger.log.Debug($"Hiding default '{defaultSaber.saberType}'");
                IEnumerable<MeshFilter> meshFilters = defaultSaber.transform.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    meshFilter.gameObject.SetActive(!saberRoot);

                    MeshFilter filter = meshFilter.GetComponentInChildren<MeshFilter>();
                    filter?.gameObject.SetActive(!saberRoot);
                }

                Logger.log.Debug($"Attaching custom saber to '{defaultSaber.saberType}'");
                GameObject saber = GetCustomSaberByType(defaultSaber.saberType);
                if (saber)
                {
                    saber.transform.parent = defaultSaber.transform;
                    saber.transform.position = defaultSaber.transform.position;
                    saber.transform.rotation = defaultSaber.transform.rotation;

                    IEnumerable<CustomTrail> customTrails = saber.GetComponents<CustomTrail>();

                    if (Configuration.TrailType == TrailType.Custom && customTrails.Count() > 0)
                    {
                        HideVanillaTrails();

                        foreach (CustomTrail trail in customTrails)
                        {
                            trail.Init(defaultSaber, colorManager);
                        }
                    }
                    else if (Configuration.TrailType != TrailType.None)
                    {
                        if (Configuration.OverrideTrailLength)
                        {
                            SetDefaultTrailLength(defaultSaber);
                        }
                        if (Configuration.DisableWhitestep)
                        {
                            defaultSaber.GetComponentInChildren<SaberTrail>()?.SetField("_whiteSectionMaxDuration", 0f);
                        }
                    }

                    ApplyColorsToSaber(saber, colorManager.ColorForSaberType(defaultSaber.saberType));
                    ApplyScaleToSabers();
                }
            }
        }

        void SetDefaultTrailLength(Saber saber)
        {
            var trail = saber.GetComponentInChildren<SaberTrail>();
            float length = Configuration.TrailLength * 30;
            if (length < 2)
            {
                HideVanillaTrails();
                return;
            }
            trail.SetField("_trailDuration", length / 75f);
        }

        void ApplyScaleToSabers()
        {
            leftSaber.transform.localScale = new Vector3(Configuration.SaberWidthAdjust, Configuration.SaberWidthAdjust, 1);
            rightSaber.transform.localScale = new Vector3(Configuration.SaberWidthAdjust, Configuration.SaberWidthAdjust, 1);
        }

        public static void ApplyColorsToSaber(GameObject saber, Color color)
        {
            //Logger.log.Debug($"Applying Color: {color} to saber: {saber.name}");
            IEnumerable<Renderer> renderers = saber.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    foreach (Material renderMaterial in renderer.sharedMaterials)
                    {
                        if (renderMaterial == null)
                        {
                            continue;
                        }

                        if (renderMaterial.HasProperty("_Color"))
                        {
                            if (renderMaterial.HasProperty("_CustomColors"))
                            {
                                if (renderMaterial.GetFloat("_CustomColors") > 0)
                                    renderMaterial.SetColor("_Color", color);
                            }
                            else if (renderMaterial.HasProperty("_Glow") && renderMaterial.GetFloat("_Glow") > 0
                                || renderMaterial.HasProperty("_Bloom") && renderMaterial.GetFloat("_Bloom") > 0)
                            {
                                renderMaterial.SetColor("_Color", color);
                            }
                        }
                    }
                }
            }
        }

        private IEnumerator WaitToCheckDefault()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<Saber>().Any());

            if (Configuration.TrailType == TrailType.None)
            {
                HideVanillaTrails();
            }

            bool hideOneSaber = false;
            SaberType hiddenSaberType = SaberType.SaberA;
            if (BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.characteristicNameLocalizationKey.Contains("ONE_SABER"))
            {
                hideOneSaber = true;
                hiddenSaberType = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.playerSpecificSettings.leftHanded ? SaberType.SaberB : SaberType.SaberA;
            }

            Logger.log.Debug("Default Sabers. Not Replacing");
            IEnumerable<Saber> defaultSabers = Resources.FindObjectsOfTypeAll<Saber>();
            foreach (Saber defaultSaber in defaultSabers)
            {
                bool activeState = !hideOneSaber ? true : defaultSaber.saberType != hiddenSaberType;
                defaultSaber.gameObject.SetActive(activeState);

                if (defaultSaber.saberType == hiddenSaberType)
                {
                    IEnumerable<MeshFilter> meshFilters = defaultSaber.transform.GetComponentsInChildren<MeshFilter>();
                    foreach (MeshFilter meshFilter in meshFilters)
                    {
                        meshFilter.gameObject.SetActive(!sabers);

                        MeshFilter filter = meshFilter.GetComponentInChildren<MeshFilter>();
                        filter?.gameObject.SetActive(!sabers);
                    }
                }
            }
        }

        private void Update()
        {
            if (playerHeadAndObstacleInteraction != null
                && playerHeadAndObstacleInteraction.intersectingObstacles.Count > 0)
            {
                if (!playerHeadWasInObstacle)
                {
                    leftEventManager?.OnComboBreak?.Invoke();
                    rightEventManager?.OnComboBreak?.Invoke();
                }

                playerHeadWasInObstacle = !playerHeadWasInObstacle;
            }
        }

        private void HideVanillaTrails() => SetVanillaTrailVisibility(0f);
        private void ResetVanillaTrails() => SetVanillaTrailVisibility(1.007f);

        private IEnumerator SetVanillaTrailVisibilityCoroutine(float trailWidth)
        {
            void SetVanillaTrailVisibilityCoroutineInner()
            {
                foreach (SaberTrail trail in Resources.FindObjectsOfTypeAll<SaberTrail>())
                {
                    if (!(trail is CustomWeaponTrail))
                    {
                        SaberTrailRenderer trailRenderer = trail.GetField<SaberTrailRenderer, SaberTrail>("_trailRenderer");
                        if (trailRenderer != null)
                        {
                            trailRenderer.SetTrailWidth(trailWidth);
                        }
                    }
                }
            }

            // wait 5 frames for default trailwidth is set
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            SetVanillaTrailVisibilityCoroutineInner();

            // just in case
            yield return new WaitForSecondsRealtime(0.1f);
            SetVanillaTrailVisibilityCoroutineInner();
        }

        private void SetVanillaTrailVisibility(float trailWidth)
        {
            StartCoroutine(SetVanillaTrailVisibilityCoroutine(trailWidth));
        }

        private GameObject GetCustomSaberByType(SaberType saberType)
        {
            GameObject saber = null;
            if (saberType == SaberType.SaberA)
            {
                saber = leftSaber;
            }
            else if (saberType == SaberType.SaberB)
            {
                saber = rightSaber;
            }

            return saber;
        }

        private EventManager GetEventManagerByType(SaberType saberType)
        {
            EventManager eventManager = null;
            if (saberType == SaberType.SaberA)
            {
                eventManager = leftEventManager;
            }
            else if (saberType == SaberType.SaberB)
            {
                eventManager = rightEventManager;
            }

            return eventManager;
        }

        #region Events

        private void SliceCallBack(NoteController noteController, NoteCutInfo noteCutInfo)
        {
            if (!noteCutInfo.allIsOK)
            {
                leftEventManager?.OnComboBreak?.Invoke();
                rightEventManager?.OnComboBreak?.Invoke();
                StartCoroutine(CalculateAccuracyAndFireEvents());
            }
            else
            {
                EventManager eventManager = GetEventManagerByType(noteCutInfo.saberType);
                eventManager?.OnSlice?.Invoke();
                noteCutInfo.swingRatingCounter.didFinishEvent += OnSwingRatingCounterFinished;
            }

            if (Mathf.Approximately(noteController.noteData.time, lastNoteTime))
            {
                lastNoteTime = 0;
                leftEventManager?.OnLevelEnded?.Invoke();
                rightEventManager?.OnLevelEnded?.Invoke();
            }
        }

        private void NoteMissCallBack(NoteController noteController)
        {
            if (noteController.noteData.colorType != global::ColorType.None)
            {
                leftEventManager?.OnComboBreak?.Invoke();
                rightEventManager?.OnComboBreak?.Invoke();
            }

            if (Mathf.Approximately(noteController.noteData.time, lastNoteTime))
            {
                lastNoteTime = 0;
                leftEventManager?.OnLevelEnded?.Invoke();
                rightEventManager?.OnLevelEnded?.Invoke();
            }

            StartCoroutine(CalculateAccuracyAndFireEvents());
        }

        private void MultiplierCallBack(int multiplier, float progress)
        {
            if (multiplier > 1 && progress < 0.1f)
            {
                leftEventManager?.MultiplierUp?.Invoke();
                rightEventManager?.MultiplierUp?.Invoke();
            }
        }

        private void SaberStartCollide(SaberType saberType)
        {
            EventManager eventManager = GetEventManagerByType(saberType);
            eventManager?.SaberStartColliding?.Invoke();
        }

        private void SaberEndCollide(SaberType saberType)
        {
            EventManager eventManager = GetEventManagerByType(saberType);
            eventManager?.SaberStopColliding?.Invoke();
        }

        private void FailLevelCallBack()
        {
            leftEventManager?.OnLevelFail?.Invoke();
            rightEventManager?.OnLevelFail?.Invoke();
        }

        private void LightEventCallBack(BeatmapEventData songEvent)
        {
            if ((int)songEvent.type < 5)
            {
                if (songEvent.value > 0 && songEvent.value < 4)
                {
                    leftEventManager?.OnBlueLightOn?.Invoke();
                    rightEventManager?.OnBlueLightOn?.Invoke();
                }

                if (songEvent.value > 4 && songEvent.value < 8)
                {
                    leftEventManager?.OnRedLightOn?.Invoke();
                    rightEventManager?.OnRedLightOn?.Invoke();
                }
            }
        }

        private void ComboChangeEvent(int combo)
        {
            leftEventManager?.OnComboChanged?.Invoke(combo);
            rightEventManager?.OnComboChanged?.Invoke(combo);
        }

        private IEnumerator CalculateAccuracyAndFireEvents()
        {
            // wait for next frame to let the scoreController catch up
            yield return null;

            var rawScore = scoreController.prevFrameRawScore;
            var maximumScore = ScoreModel.MaxRawScoreForNumberOfNotes(ReflectionUtil.GetField<int, ScoreController>(scoreController, "_cutOrMissedNotes"));
            var accuracy = (float)rawScore / (float)maximumScore;

            leftEventManager?.OnAccuracyChanged?.Invoke(accuracy);
            rightEventManager?.OnAccuracyChanged?.Invoke(accuracy);
        }

        private void OnSwingRatingCounterFinished(ISaberSwingRatingCounter afterCutRating)
        {
            afterCutRating.didFinishEvent -= OnSwingRatingCounterFinished;
            StartCoroutine(CalculateAccuracyAndFireEvents());
        }

        #endregion
    }
}
