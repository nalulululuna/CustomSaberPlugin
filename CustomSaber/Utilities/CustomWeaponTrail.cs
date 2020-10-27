using IPA.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

namespace CustomSaber.Utilities
{
    public enum TrailType
    {
        Custom,
        Vanilla,
        None
    }

    internal class CustomWeaponTrail : SaberTrail
    {
        public ColorType _saberType;
        public ColorManager _colorManager;
        public Color _multiplierSaberColor;
        public Color _customColor;
        public Material _customMaterial;

        protected Transform _pointStart;
        protected Transform _pointEnd;
        protected Color trailTintColor;

        public Color color
        {
            get
            {
                Color tempColor = _customColor * _multiplierSaberColor;
                if (_colorManager != null)
                {
                    if (_saberType.Equals(ColorType.LeftSaber))
                    {
                        tempColor = _colorManager.ColorForSaberType(SaberType.SaberA) * _multiplierSaberColor;
                    }
                    else if (_saberType.Equals(ColorType.RightSaber))
                    {
                        tempColor = _colorManager.ColorForSaberType(SaberType.SaberB) * _multiplierSaberColor;
                    }
                }

                //return (tempColor * trailTintColor).linear;
                return tempColor;
            }
        }

        public void Init(SaberTrailRenderer TrailRendererPrefab, ColorManager colorManager, Transform PointStart, Transform PointEnd, Material TrailMaterial, Color TrailColor, int Length, int Granularity, Color multiplierSaberColor, ColorType colorType)
        {
            _colorManager = colorManager;
            _multiplierSaberColor = multiplierSaberColor;
            _customColor = TrailColor;
            _customMaterial = TrailMaterial;
            _saberType = colorType;

            _pointStart = PointStart;
            _pointEnd = PointEnd;
            _trailDuration = Length / 75f;
            if (!Settings.Configuration.DisableWhitestep)
            {
                _whiteSectionMaxDuration = 0.04f;
            }

            Logger.log.Info($"Granularity: {_granularity}");
            _granularity = Granularity;
            _trailRendererPrefab = TrailRendererPrefab;

            SaberModelController saberModelController = Resources.FindObjectsOfTypeAll<SaberModelController>().FirstOrDefault();
            SaberModelController.InitData initData = saberModelController?.GetField<SaberModelController.InitData, SaberModelController>("_initData");
            if (initData != null)
            {
                trailTintColor = initData.trailTintColor;
            }

            _trailRenderer = Instantiate<SaberTrailRenderer>(TrailRendererPrefab, Vector3.zero, Quaternion.identity);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            StartCoroutine(replaceMaterialCoroutine());
            if (Settings.Configuration.DisableWhitestep) ReflectionUtil.SetField<SaberTrail, float>(this, "_whiteSectionMaxDuration", 0);
        }

        protected IEnumerator replaceMaterialCoroutine()
        {
            MeshRenderer meshRenderer = null;
            for (int i = 0; i < 10; i++)
            {
                meshRenderer = _trailRenderer?.GetField<MeshRenderer, SaberTrailRenderer>("_meshRenderer");
                if (meshRenderer != null)
                {
                    break;
                }
                yield return new WaitForSecondsRealtime(0.05f);
            }

            if (meshRenderer != null)
            {
                meshRenderer.material = _customMaterial;
            }
        }

        public void SetColor(Color newColor)
        {
            _customColor = newColor;
        }

        public void SetMaterial(Material newMaterial)
        {
            _customMaterial = newMaterial;
            StartCoroutine(replaceMaterialCoroutine());
        }

        public override void ResetTrailData()
        {
            _trailElementCollection.InitSnapshots(_pointStart.position, _pointEnd.position, TimeHelper.time);
        }

        public override void Init()
        {
            // nop
        }

        private float _prevTrailTime;

        public override void LateUpdate()
        {
            // wait until the fps is stable
            const int passThroughFrames = 20;

            if (_framesPassed <= passThroughFrames)
            {
                if (_framesPassed == passThroughFrames)
                {
                    _samplingFrequency = Mathf.RoundToInt(VRDeviceInfo.Instance.refreshRate);
                    if (VRDeviceInfo.Instance.isPimax)
                    {
                        _samplingFrequency = _samplingFrequency / 2;
                    }
                    Logger.log.Debug($"trail samplingFrequency={_samplingFrequency}");

                    _sampleStep = 1f / (float)_samplingFrequency;
                    int capacity = Mathf.CeilToInt((float)_samplingFrequency * _trailDuration);
		            _trailElementCollection = new TrailElementCollection(capacity, _pointStart.position, _pointEnd.position, TimeHelper.time);
		            float trailWidth = (_pointEnd.position - _pointStart.position).magnitude;
		            _whiteSectionMaxDuration = Mathf.Min(_whiteSectionMaxDuration, _trailDuration);
		            _lastZScale = transform.lossyScale.z;
                    _trailRenderer.Init(trailWidth, _trailDuration, _granularity, _whiteSectionMaxDuration);
		            _inited = true;
                }
                _framesPassed++;

                return;
            }

            _framesToScaleCheck--;
            if (_framesToScaleCheck <= 0)
            {
                _framesToScaleCheck = 10;
                if (!Mathf.Approximately(base.transform.lossyScale.z, _lastZScale))
                {
                    _lastZScale = base.transform.lossyScale.z;
                    float trailWidth = (_pointEnd.position - _pointStart.position).magnitude;
                    _trailRenderer.SetTrailWidth(trailWidth);
                }
            }

            if (_prevTrailTime == 0)
            {
                _prevTrailTime = TimeHelper.time;
            }
            int num = Mathf.RoundToInt((TimeHelper.time - _prevTrailTime) / _sampleStep);
            for (int i = 0; i < num; i++)
            {
                _prevTrailTime = TimeHelper.time;
                _trailElementCollection.MoveTailToHead();
                _trailElementCollection.head.SetData(_pointStart.position, _pointEnd.position, _prevTrailTime);
            }
            _trailElementCollection.UpdateDistances();
            _trailRenderer.UpdateMesh(_trailElementCollection, color);
        }
    }

    public sealed class VRDeviceInfo
    {
        public bool isPimax { get; private set; }
        public float refreshRate { get; private set; }

        private static VRDeviceInfo instance = new VRDeviceInfo();

        public static VRDeviceInfo Instance
        {
            get
            {
                return instance;
            }
        }

        private VRDeviceInfo()
        {
            Logger.log?.Info($"XRDevice.model: {XRDevice.model}");
            Logger.log?.Info($"XRSettings.loadedDeviceName: {XRSettings.loadedDeviceName}");

            refreshRate = XRDevice.refreshRate;
            Logger.log?.Info($"refreshRate: {refreshRate}");

            isPimax = XRDevice.model.ToLower().Contains("pimax");
        }
    }
}
