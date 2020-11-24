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
            _trailDuration = (float)Length / 75f;
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
            if (Settings.Configuration.DisableWhitestep) ReflectionUtil.SetField<SaberTrail, float>(this, "_whiteSectionMaxDuration", 0f);
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
            if (_trailElementCollection != null)
            {
                _lastTrailElementTime = TimeHelper.time;
                _trailElementCollection.InitSnapshots(_pointStart.position, _pointEnd.position, _lastTrailElementTime);
            }
        }

        public override void Init()
        {
            // nop
        }

        Vector3 _lastPointStart;
        Vector3 _lastPointEnd;

        public override void LateUpdate()
        {
            // wait until the fps is stable
            const int passThroughFrames = 4;

            if (_framesPassed <= passThroughFrames)
            {
                if (_framesPassed == passThroughFrames)
                {
                    _samplingFrequency = Mathf.RoundToInt(VRDeviceInfo.Instance.refreshRate);
                    if (VRDeviceInfo.Instance.isPimax)
                    {
                        _samplingFrequency = _samplingFrequency / 2;
                    }
                    if (_samplingFrequency == 0)
                    {
                        _samplingFrequency = 60;
                    }
                    _samplingFrequency = _samplingFrequency + 1;// Mathf.RoundToInt((float)_samplingFrequency * 1.1f);

                    _sampleStep = 1f / (float)_samplingFrequency;
                    int capacity = Mathf.CeilToInt((float)_samplingFrequency * _trailDuration);
                    Logger.log.Debug($"trail samplingFrequency={_samplingFrequency}, capacity={capacity}");
                    _lastTrailElementTime = TimeHelper.time;
                    _trailElementCollection = new TrailElementCollection(capacity, _pointStart.position, _pointEnd.position, _lastTrailElementTime);
		            float trailWidth = (_pointEnd.position - _pointStart.position).magnitude;
		            _whiteSectionMaxDuration = Mathf.Min(_whiteSectionMaxDuration, _trailDuration);
		            _lastZScale = transform.lossyScale.z;
                    _trailRenderer.Init(trailWidth, _trailDuration, _granularity, _whiteSectionMaxDuration);

                    _lastPointStart = _pointStart.position;
                    _lastPointEnd = _pointEnd.position;

                    _inited = true;
                }
                _framesPassed++;

                return;
            }

            /* trailWidth update
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
            */

            int num = Mathf.RoundToInt((TimeHelper.time - _lastTrailElementTime) / _sampleStep);

            // frame drop correction ----
            for (int i = 1; i < num; i++)
            {
                float t = (float)i / (float)num;
                _lastTrailElementTime = (TimeHelper.time - _lastTrailElementTime) * t + _lastTrailElementTime;
                _lastPointStart = Vector3.Slerp(_lastPointStart, _pointStart.position, t);
                _lastPointEnd = Vector3.Slerp(_lastPointEnd, _pointEnd.position, t);

                _trailElementCollection.MoveTailToHead();
                _trailElementCollection.head.SetData(_lastPointStart, _lastPointEnd, _lastTrailElementTime);
            }

            _lastTrailElementTime = TimeHelper.time;
            _lastPointStart = _pointStart.position;
            _lastPointEnd = _pointEnd.position;
            _trailElementCollection.MoveTailToHead();
            _trailElementCollection.head.SetData(_lastPointStart, _lastPointEnd, _lastTrailElementTime);
            // ----

            /*
            // no frame drop correction ----
            for (int i = 0; i < num; i++)
            {
                _lastTrailElementTime = TimeHelper.time;
                _trailElementCollection.MoveTailToHead();
                _trailElementCollection.head.SetData(_pointStart.position, _pointEnd.position, _lastTrailElementTime);
            }
            // ----
            */

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
