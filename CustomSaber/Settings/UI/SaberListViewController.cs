using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using CustomSaber.Data;
using CustomSaber.Utilities;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

namespace CustomSaber.Settings.UI
{
    internal class SaberListViewController : BSMLResourceViewController
    {
        public override string ResourceName => "CustomSaber.Settings.UI.Views.saberList.bsml";

        public static SaberListViewController Instance;

        private bool isGeneratingPreview;
        private GameObject preview;

        // Sabers
        private GameObject previewSabers;
        private GameObject leftSaber;
        private GameObject rightSaber;

        // SaberPositions (Local to the previewer)
        private Vector3 sabersPos = new Vector3(0, 0, 0);
        private Vector3 saberLeftPos = new Vector3(0, 0, 0);
        private Vector3 saberRightPos = new Vector3(0, 0.5f, 0);

        public Action<CustomSaberData> customSaberChanged;

        [UIComponent("saberList")]
        public CustomListTableData customListTableData;

        [UIAction("saberSelect")]
        public void Select(TableView _, int row)
        {
            SaberAssetLoader.SelectedSaber = row;
            Configuration.CurrentlySelectedSaber = SaberAssetLoader.CustomSabers[row].FileName;
            customSaberChanged?.Invoke(SaberAssetLoader.CustomSabers[row]);

            StartCoroutine(GenerateSaberPreview(row));
        }

        [UIAction("reloadSabers")]
        public void ReloadMaterials()
        {
            SaberAssetLoader.Reload();
            SetupList();
            Select(customListTableData.tableView, SaberAssetLoader.SelectedSaber);
        }

        [UIAction("deleteSaber")]
        public void DeleteCurrentSaber()
        {
            var deletedSaber = SaberAssetLoader.DeleteCurrentSaber();

            if (deletedSaber == 0) return;

            SetupList();
            Select(customListTableData.tableView, SaberAssetLoader.SelectedSaber);
        }

        [UIAction("update-confirmation")]
        public void UpdateDeleteConfirmation() => confirmationText.text = $"Are you sure you want to delete\n<color=\"red\">{SaberAssetLoader.CustomSabers[SaberAssetLoader.SelectedSaber].Descriptor.SaberName}</color>?";

        [UIComponent("delete-saber-confirmation-text")]
        public TextMeshProUGUI confirmationText;

        [UIAction("#post-parse")]
        public void SetupList()
        {
            customListTableData.data.Clear();
            foreach (var saber in SaberAssetLoader.CustomSabers)
            {
                var texture = saber.Descriptor.CoverImage?.texture;
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                var customCellInfo = new CustomListTableData.CustomCellInfo(saber.Descriptor.SaberName, saber.Descriptor.AuthorName, sprite);
                customListTableData.data.Add(customCellInfo);
            }

            customListTableData.tableView.ReloadData();
            var selectedSaber = SaberAssetLoader.SelectedSaber;

            customListTableData.tableView.SelectCellWithIdx(selectedSaber);
            if (!customListTableData.tableView.visibleCells.Where(x => x.selected).Any())
                customListTableData.tableView.ScrollToCellWithIdx(selectedSaber, TableViewScroller.ScrollPositionType.Beginning, true);
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            Instance = this;

            if (!preview)
            {
                preview = new GameObject("Preview");
                preview.transform.position = new Vector3(2.2f, 1.3f, 1.0f);
                preview.transform.Rotate(0.0f, 330.0f, 0.0f);
            }

            Select(customListTableData.tableView, SaberAssetLoader.SelectedSaber);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screen);
            ClearPreview();
        }

        public IEnumerator GenerateSaberPreview(int selectedSaber)
        {
            if (!isGeneratingPreview)
            {
                yield return new WaitUntil(() => DefaultSaberGrabber.isCompleted);
                try
                {
                    isGeneratingPreview = true;
                    ClearSabers();

                    var customSaber = SaberAssetLoader.CustomSabers[selectedSaber];
                    if (customSaber != null)
                    {

                        previewSabers = CreatePreviewSaber(customSaber.Sabers, preview.transform, sabersPos);
                        PositionPreviewSaber(saberLeftPos, previewSabers?.transform.Find("LeftSaber").gameObject);
                        PositionPreviewSaber(saberRightPos, previewSabers?.transform.Find("RightSaber").gameObject);

                        previewSabers?.transform.Find("LeftSaber").gameObject.SetActive(true);
                        previewSabers?.transform.Find("LeftSaber").gameObject.gameObject.AddComponent<DummySaber>();
                        previewSabers?.transform.Find("RightSaber").gameObject.SetActive(true);
                        previewSabers?.transform.Find("RightSaber").gameObject.gameObject.AddComponent<DummySaber>();

                        if (Configuration.ShowSabersInSaberMenu)
                            GenerateHandheldSaberPreview();
                    }
                }
                catch (Exception ex)
                {
                    Logger.log.Error(ex);
                }
                finally
                {
                    isGeneratingPreview = false;
                }
            }
        }

        private GameObject CreatePreviewSaber(GameObject saber, Transform transform, Vector3 localPosition)
        {
            if (!saber) return null;
            var saberObject = InstantiateGameObject(saber, transform);
            saberObject.name = "Preview Saber Object";
            PositionPreviewSaber(localPosition, saberObject);
            return saberObject;
        }

        SaberMovementData _leftMovementData = new SaberMovementData();
        SaberMovementData _rightMovementData = new SaberMovementData();
        VRController _leftController;
        VRController _rightController;
        SaberTrailRenderer _trailRendererPrefab;

        public void GenerateHandheldSaberPreview()
        {
            if (Environment.CommandLine.Contains("fpfc")) return;
            var customSaber = SaberAssetLoader.CustomSabers[SaberAssetLoader.SelectedSaber];
            if (customSaber == null || !customSaber.Sabers) return;
            var controllers = Resources.FindObjectsOfTypeAll<VRController>();
            var sabers = CreatePreviewSaber(customSaber.Sabers, preview.transform, sabersPos);
            var colorManager = Resources.FindObjectsOfTypeAll<ColorManager>().First();

            try
            {
                if (_trailRendererPrefab == null)
                {
                    foreach (var trail in Resources.FindObjectsOfTypeAll<SaberTrail>())
                    {
                        _trailRendererPrefab = trail.GetField<SaberTrailRenderer, SaberTrail>("_trailRendererPrefab");
                        if (_trailRendererPrefab != null)
                        {
                            break;
                        }
                    }
                }

                foreach (var controller in controllers)
                {
                    if (controller?.node == XRNode.LeftHand)
                    {
                        _leftController = controller;

                        leftSaber = sabers?.transform.Find("LeftSaber").gameObject;
                        if (!leftSaber) continue;

                        leftSaber.transform.parent = controller.transform;
                        leftSaber.transform.position = controller.transform.position;
                        leftSaber.transform.rotation = controller.transform.rotation;

                        leftSaber.SetActive(true);

                        var trails = leftSaber.GetComponentsInChildren<CustomTrail>();

                        if (trails == null || trails.Count() == 0)
                        {
                            SaberTrail saberTrail = leftSaber.AddComponent<SaberTrail>();
                            saberTrail.SetField("_trailRenderer", Instantiate(_trailRendererPrefab, Vector3.zero, Quaternion.identity));
                            saberTrail.Setup(colorManager.ColorForSaberType(SaberType.SaberA), _leftMovementData);

                            if (Configuration.OverrideTrailLength)
                            {
                                float length = Configuration.TrailLength * 30;
                                saberTrail.SetField("_trailDuration", length / 75f);
                            }
                            if (Configuration.DisableWhitestep)
                            {
                                saberTrail.SetField("_whiteSectionMaxDuration", 0f);
                            }
                        }
                        else
                        {
                            foreach (var trail in trails)
                            {
                                trail.Length = (Configuration.OverrideTrailLength) ? (int)(trail.Length * Configuration.TrailLength) : trail.Length;
                                if (trail.Length < 2 || !trail.PointStart || !trail.PointEnd) continue;
                                {
                                    leftSaber.AddComponent<CustomWeaponTrail>().Init(_trailRendererPrefab, colorManager, trail.PointStart, trail.PointEnd,
                                        trail.TrailMaterial, trail.TrailColor, trail.Length, trail.Granularity, trail.MultiplierColor, trail.colorType);
                                }
                            }
                        }

                        leftSaber.AddComponent<DummySaber>();

                        controller.transform.Find("MenuHandle")?.gameObject.SetActive(false);
                    }
                    else if (controller?.node == XRNode.RightHand)
                    {
                        _rightController = controller;

                        rightSaber = sabers?.transform.Find("RightSaber").gameObject;
                        if (!rightSaber) continue;

                        rightSaber.transform.parent = controller.transform;
                        rightSaber.transform.position = controller.transform.position;
                        rightSaber.transform.rotation = controller.transform.rotation;

                        rightSaber.SetActive(true);

                        var trails = rightSaber.GetComponentsInChildren<CustomTrail>();

                        if (trails == null || trails.Count() == 0)
                        {
                            SaberTrail saberTrail = rightSaber.AddComponent<SaberTrail>();
                            saberTrail.SetField("_trailRenderer", Instantiate(_trailRendererPrefab, Vector3.zero, Quaternion.identity));
                            saberTrail.Setup(colorManager.ColorForSaberType(SaberType.SaberB), _rightMovementData);

                            if (Configuration.OverrideTrailLength)
                            {
                                float length = Configuration.TrailLength * 30;
                                saberTrail.SetField("_trailDuration", length / 75f);
                            }
                            if (Configuration.DisableWhitestep)
                            {
                                saberTrail.SetField("_whiteSectionMaxDuration", 0f);
                            }
                        }
                        else
                        {
                            foreach (var trail in trails)
                            {
                                trail.Length = (Configuration.OverrideTrailLength) ? (int)(trail.Length * Configuration.TrailLength) : trail.Length;
                                if (trail.Length < 2 || !trail.PointStart || !trail.PointEnd) continue;
                                rightSaber.AddComponent<CustomWeaponTrail>().Init(_trailRendererPrefab, colorManager, trail.PointStart, trail.PointEnd,
                                    trail.TrailMaterial, trail.TrailColor, trail.Length, trail.Granularity, trail.MultiplierColor, trail.colorType);
                            }
                        }

                        rightSaber.AddComponent<DummySaber>();

                        controller.transform.Find("MenuHandle")?.gameObject.SetActive(false);
                    }
                    if (leftSaber && rightSaber) break;
                }
                StartCoroutine(HideOrShowPointer());
            }
            catch(Exception e)
            {
                Logger.log.Error($"Error generating saber preview\n{e.Message} - {e.StackTrace}");
            }
            finally
            {
                DestroyGameObject(ref sabers);
            }
        }

        private void Update()
        {
            if (_rightController != null)
            {
                Vector3 top = new Vector3(0f, 0f, 1f);
                top = _rightController.rotation * top;
                _rightMovementData.AddNewData(_rightController.transform.position, _rightController.transform.position + top, TimeHelper.time);
            }

            if (_leftController != null)
            {
                Vector3 top = new Vector3(0f, 0f, 1f);
                top = _leftController.rotation * top;
                _leftMovementData.AddNewData(_leftController.transform.position, _leftController.transform.position + top, TimeHelper.time);
            }
        }

        private GameObject InstantiateGameObject(GameObject gameObject, Transform transform = null)
        {
            if (gameObject)
            {
                return transform ? Instantiate(gameObject, transform) : Instantiate(gameObject);
            }

            return null;
        }

        private void PositionPreviewSaber(Vector3 vector, GameObject saberObject)
        {
            if (saberObject && vector != null)
            {
                saberObject.transform.localPosition = vector;
            }
        }

        private void ClearPreview()
        {
            ClearSabers();
            DestroyGameObject(ref preview);
            ShowMenuHandles();
        }

        private void ClearSabers()
        {
            DestroyGameObject(ref previewSabers);
            ClearHandheldSabers();
        }

        public void ClearHandheldSabers()
        {
            DestroyGameObject(ref leftSaber);
            DestroyGameObject(ref rightSaber);
        }

        float initialSize = -1;
        VRUIControls.VRPointer pointer = null;
        IEnumerator HideOrShowPointer(bool enable = false)
        {
            yield return new WaitUntil(() => pointer = Resources.FindObjectsOfTypeAll<VRUIControls.VRPointer>().FirstOrDefault());
            if (initialSize == -1) initialSize = ReflectionUtil.GetField<float, VRUIControls.VRPointer>(pointer, "_laserPointerWidth");
            pointer.SetField("_laserPointerWidth", enable ? initialSize : 0f);
        }

        public void ShowMenuHandles()
        {
            foreach (var controller in Resources.FindObjectsOfTypeAll<VRController>())
            {
                controller.transform?.Find("MenuHandle")?.gameObject?.SetActive(true);
            }

            StartCoroutine(HideOrShowPointer(true));
        }

        private void DestroyGameObject(ref GameObject gameObject)
        {
            if (gameObject)
            {
                DestroyImmediate(gameObject);
                gameObject = null;
            }
        }
    }
}
