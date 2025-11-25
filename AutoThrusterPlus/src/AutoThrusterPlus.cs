using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using MVR.FileManagementSecure;
using AssetBundles;

namespace Dragontales
{
    public class AutoThrusterPlus : MVRScript, TriggerHandler
    {
        // ============================================================================================== //
        // ========================================= PROPERTIES ========================================= //
        // ============================================================================================== //
        public bool __DEBUG__ = false;

        // UI variables
        private JSONStorableBool _activeJSON;
        private JSONStorableFloat _distanceInJSON;
        private JSONStorableFloat _distanceOutJSON;
        private JSONStorableFloat _distanceRandomJSON;
        private JSONStorableFloat _cycleDurationMinJSON;
        private JSONStorableFloat _cycleDurationMaxJSON;
        private JSONStorableFloat _cycleVariationRateJSON;
        private JSONStorableFloat _distanceCompressionJSON;
        private JSONStorableFloat _distanceOffsetJSON;
        private JSONStorableBool _thrustJSON;
        private JSONStorableStringChooser _easingInJSON;
        private JSONStorableStringChooser _easingOutJSON;
        private JSONStorableFloat _chestMotionJSON;
        private JSONStorableFloat _headMotionJSON;
        private JSONStorableFloat _durationBiasJSON;
        private JSONStorableFloat _speedHoldCyclesMinJSON;
        private JSONStorableFloat _speedHoldCyclesMaxJSON;
        private JSONStorableFloat _speedDriftAmountJSON;
        private JSONStorableFloat _targetHipMotionJSON;
        private JSONStorableFloat _targetHeadMotionJSON;

        private JSONStorableFloat _chestForceYJSON;
        private JSONStorableFloat _chestForceZJSON;
        private JSONStorableFloat _chestTorqueXJSON;

        private JSONStorableFloat _headTorqueXJSON;
        private JSONStorableFloat _headForceZJSON;

        private JSONStorableFloat _hipTorqueXJSON;

        private JSONStorableFloat _targetHipForceYJSON;
        private JSONStorableFloat _targetHipForceZJSON;
        private JSONStorableFloat _targetHipTorqueXJSON;

        private JSONStorableFloat _targetChestForceYJSON;
        private JSONStorableFloat _targetChestForceZJSON;
        private JSONStorableFloat _targetChestTorqueXJSON;

        private JSONStorableFloat _targetHeadForceYJSON;
        private JSONStorableFloat _targetHeadForceZJSON;
        private JSONStorableFloat _targetHeadTorqueXJSON;
        private JSONStorableFloat _targetHeadTorqueZJSON;

        private JSONStorableFloat _initialEntryDurationJSON;
        private JSONStorableFloat _speedRampDurationJSON;
        private JSONStorableBool _freezePenisPositionJSON;
        private JSONStorableFloat _personMotionVariabilityJSON;
        private JSONStorableFloat _targetMotionVariabilityJSON;
        private JSONStorableFloat _outPhaseMotionJSON;
        private JSONStorableFloat _phaseLagJSON;

        // Person atoms, controls, colliders
        private Cycler _cycler;
        private JSONStorableFloat _cyclePctInJSON;

        private TargetManager _targetManager;
        private FreeControllerV3 _penisBaseCtrl;
        private Rigidbody _chestRB;
        private Rigidbody _headRB;
        private Rigidbody _hipRB;
        private Rigidbody _targetHipRB;
        private Rigidbody _targetHeadRB;
        private Rigidbody _targetChestRB;
        private Vector3 _previousPenisPos;

        // Temporary controller save/restore settings
        private CtrlSnapshot _ctrlSnapshot;
        private bool _restoring = false;
        private float _distanceFactor = 1.0f;
        private float _personMotionVariability = 1.0f;
        private float _targetMotionVariability = 1.0f;
        private float _thrustIntensity = 1f;
        private bool _isMovingToTarget = false;
        private bool _isMovingToRest = false;
        private bool _isFinishingCycle = false;
        private float _transitionDuration = 0.5f;
        private float _transitionTimer = 0f;
        private Vector3 _transitionStartPos;
        private Vector3 _transitionEndPos;
        private float _previousDuration = 0f;
        private int _cyclesRemainingAtCurrentSpeed = 0;
        private float _targetSpeedDuration = 0f;
        private bool _frozen = false;

        private Vector3 _smoothedTargetPos1;
        private Vector3 _smoothedTargetPos2;
        private bool _targetPositionsInitialized = false;
        private float _previousEasedMagnitude = 0f;
        private float _previousChestMagnitude = 0f;
        private float _previousHeadMagnitude = 0f;
        private float _previousHipMagnitude = 0f;
        private float _previousReverseMotion = 0f;
        private float _chestVelocity = 0f;
        private float _headVelocity = 0f;
        private float _hipVelocity = 0f;
        private float _reverseMotionVelocity = 0f;
        private JSONStorableFloat _targetTrackingDampingJSON;

        // Force/torque values to apply in FixedUpdate
        private float _appliedChestMagnitude;
        private float _appliedHeadMagnitude;
        private float _appliedHipMagnitude;
        private float _appliedReverseMotion;
        private float _appliedCompressionFactor;
        private float _appliedPersonVariability;
        private float _appliedTargetVariability;
        private float _appliedThrustIntensity;

        private bool _isPullingOut = false;
        private bool _isReinserting = false;
        private bool _isPulledOut = false;
        private float _speedRampTimer = 0f;
        private JSONStorableFloat _pullOutDistanceJSON;
        private JSONStorableAction _pullOutActionJSON;
        private JSONStorableAction _reInsertActionJSON;
        private JSONStorableAction _savePresetActionJSON;
        private JSONStorableAction _loadPresetActionJSON;
        private JSONStorableUrl _loadPresetPathJSON;
        private JSONStorableString _currentPresetPathDisplayJSON;
        private JSONStorableBool _showPresetMessagesJSON;

        protected Trigger _onPulledOutTrigger;
        protected RectTransform _triggerActionsPrefab;
        protected RectTransform _triggerActionMiniPrefab;
        protected RectTransform _triggerActionDiscretePrefab;
        protected RectTransform _triggerActionTransitionPrefab;

        private bool _isValid => _targetManager.TargetRB1 != null && _targetManager.TargetRB2 != null && _penisBaseCtrl != null;
        private float _thrustDistance => _distanceInJSON.val - _distanceOutJSON.val;

        // TriggerHandler interface implementation
        public void RemoveTrigger(Trigger trigger) { }
        public void DuplicateTrigger(Trigger trigger) { }

        public RectTransform CreateTriggerActionsUI()
        {
            return _triggerActionsPrefab != null ? (RectTransform)Instantiate(_triggerActionsPrefab) : null;
        }

        public RectTransform CreateTriggerActionMiniUI()
        {
            return _triggerActionMiniPrefab != null ? (RectTransform)Instantiate(_triggerActionMiniPrefab) : null;
        }

        public RectTransform CreateTriggerActionDiscreteUI()
        {
            return _triggerActionDiscretePrefab != null ? (RectTransform)Instantiate(_triggerActionDiscretePrefab) : null;
        }

        public RectTransform CreateTriggerActionTransitionUI()
        {
            return _triggerActionTransitionPrefab != null ? (RectTransform)Instantiate(_triggerActionTransitionPrefab) : null;
        }

        public void RemoveTriggerActionUI(RectTransform rt)
        {
            if (rt != null) Destroy(rt.gameObject);
        }

        private GameObject _enterTriggerGO;
        private SphereCollider _enterTriggerCollider;

        // ============================================================================================== //
        // ========================================== METHODS =========================================== //
        // ============================================================================================== //

        // ========================================== BUILT-IN ========================================== //

        protected System.Collections.IEnumerator LoadUIAssets()
        {
            AssetBundleLoadAssetOperation request = AssetBundleManager.LoadAssetAsync("z_ui2", "TriggerActionsPanel", typeof(GameObject));
            if (request == null) yield break;
            yield return request;
            GameObject go = request.GetAsset<GameObject>();
            if (go != null) _triggerActionsPrefab = go.GetComponent<RectTransform>();

            request = AssetBundleManager.LoadAssetAsync("z_ui2", "TriggerActionMiniPanel", typeof(GameObject));
            if (request == null) yield break;
            yield return request;
            go = request.GetAsset<GameObject>();
            if (go != null) _triggerActionMiniPrefab = go.GetComponent<RectTransform>();

            request = AssetBundleManager.LoadAssetAsync("z_ui2", "TriggerActionDiscretePanel", typeof(GameObject));
            if (request == null) yield break;
            yield return request;
            go = request.GetAsset<GameObject>();
            if (go != null) _triggerActionDiscretePrefab = go.GetComponent<RectTransform>();

            request = AssetBundleManager.LoadAssetAsync("z_ui2", "TriggerActionTransitionPanel", typeof(GameObject));
            if (request == null) yield break;
            yield return request;
            go = request.GetAsset<GameObject>();
            if (go != null) _triggerActionTransitionPrefab = go.GetComponent<RectTransform>();
        }

        // Called by game engine on initialization
        public override void Init()
        {
            _targetManager = new TargetManager()
            {
                OnTargetAtomChange = _onTargetAtomChange,
                OnTargetChange = _ => { }
            };

            try
            {
                _onPulledOutTrigger = new Trigger();
                _onPulledOutTrigger.handler = this;

                StartCoroutine(LoadUIAssets());

                if (SuperController.singleton != null)
                {
                    SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
                }

                _cycler = new Cycler(this, OnThrustBegin, OnThrustUpdate);
                SetupUI();
            }
            catch (Exception e)
            {
                SuperController.LogError($"{nameof(AutoThrusterPlus)}.{nameof(Init)}: {e}");
            }

        }

        protected void OnAtomRename(string oldid, string newid)
        {
            if (_onPulledOutTrigger != null)
            {
                _onPulledOutTrigger.SyncAtomNames();
            }
        }

        public override void InitUI()
        {
            base.InitUI();
            if (UITransform != null && _onPulledOutTrigger != null)
            {
                _onPulledOutTrigger.triggerActionsParent = UITransform;
            }
        }

        public override void Validate()
        {
            base.Validate();
            if (_onPulledOutTrigger != null)
            {
                _onPulledOutTrigger.Validate();
            }
        }

        // Called by game engine before first update
        private void Start()
        {
            try
            {
                _penisBaseCtrl = containingAtom.freeControllers.First(fc => fc.name == "penisBaseControl");
                _chestRB = containingAtom.rigidbodies.FirstOrDefault(rb => rb.name == "chest");
                _headRB = containingAtom.rigidbodies.FirstOrDefault(rb => rb.name == "head");
                _hipRB = containingAtom.rigidbodies.FirstOrDefault(rb => rb.name == "hip");
                _previousPenisPos = _penisBaseCtrl.transform.position;

            }
            catch (Exception e)
            {
                SuperController.LogError($"{nameof(AutoThrusterPlus)}: Plugin cannot be applied to an Atom without penisBaseControl.");
                return;
            }

            _previousDuration = _cycleDurationMaxJSON.val;
            _targetSpeedDuration = _cycleDurationMaxJSON.val;
            _cyclesRemainingAtCurrentSpeed = 0;
            _UpdateState();

        }

        private void _onTargetAtomChange(string atomUID)
        {
            if (string.IsNullOrEmpty(atomUID))
            {
                _targetHipRB = null;
                _targetHeadRB = null;
                _targetChestRB = null;
                return;
            }

            var targetAtom = SuperController.singleton.GetAtomByUid(atomUID);
            if (targetAtom != null)
            {
                _targetHipRB = targetAtom.rigidbodies.FirstOrDefault(rb => rb.name == "hip");
                _targetHeadRB = targetAtom.rigidbodies.FirstOrDefault(rb => rb.name == "head");
                _targetChestRB = targetAtom.rigidbodies.FirstOrDefault(rb => rb.name == "chest");
            }
        }

        public void OnEnable() { _UpdateState(); }
        public void OnDisable() { _UpdateState(); }
        public void OnDestroy()
        {
            _Deactivate();
            if (_targetManager != null) _targetManager.Cleanup();
            if (SuperController.singleton != null)
            {
                SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
            }
        }

        private void DoPullOut()
        {
            if (!_isValid || _isPulledOut || _isPullingOut || _isReinserting) return;

            // If not currently thrusting, can't pull out
            if (!_cycler.IsActive && !_isMovingToTarget)
            {
                SuperController.LogMessage("Cannot pull out - thrusting is not active");
                return;
            }

            // Mark that we want to pull out after current cycle
            _isPullingOut = true;
            _isFinishingCycle = true;
        }

        private void DoReInsert()
        {
            if (!_isValid || !_isPulledOut || _isReinserting) return;

            _transitionStartPos = _penisBaseCtrl.transform.position;
            // Move to the configured "out" position
            _transitionEndPos = _getPos(0f);
            _transitionTimer = 0f;
            _transitionDuration = _initialEntryDurationJSON.val;
            _isReinserting = true;
        }

        private void Update()
        {
            if (SuperController.singleton.freezeAnimation)
            {
                if (!_frozen)
                {
                    _frozen = true;
                    if (_cycler != null && _cycler.IsActive)
                        _cycler.StopCycle();
                }
                return;
            }
            else if (_frozen)
            {
                _frozen = false;
                if (_thrustJSON.val && _isValid && !_isPulledOut && !_isPullingOut && !_isReinserting && !_isMovingToTarget && !_isMovingToRest)
                {
                    if (!_cycler.IsActive)
                    {
                        _speedRampTimer = 0f;
                        _thrustIntensity = 0f;
                        _cycler.StartCycle();
                    }
                }
            }

            if (_targetManager != null) _targetManager.UpdateVisualization();

            if (_isMovingToTarget)
            {
                _transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_transitionTimer / _transitionDuration);
                _penisBaseCtrl.transform.position = Vector3.Lerp(_transitionStartPos, _transitionEndPos, t);

                if (t >= 1f)
                {
                    _isMovingToTarget = false;
                    _transitionTimer = 0f;
                    _penisBaseCtrl.transform.position = _transitionEndPos;
                    _previousPenisPos = _transitionEndPos;
                    if (!_cycler.IsActive)
                    {
                        _speedRampTimer = 0f;
                        _thrustIntensity = 0f;
                        _cycler.StartCycle();
                    }
                }
            }
            else if (_isPullingOut)
            {
                _transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_transitionTimer / _transitionDuration);
                _penisBaseCtrl.transform.position = Vector3.Lerp(_transitionStartPos, _transitionEndPos, t);

                if (t >= 1f)
                {
                    _isPullingOut = false;
                    _isPulledOut = true;
                    _transitionTimer = 0f;
                    _penisBaseCtrl.transform.position = _transitionEndPos;

                    // Make sure cycler is stopped
                    if (_cycler != null && _cycler.IsActive)
                    {
                        _cycler.StopCycle();
                    }

                    // Trigger the pulled out event
                    if (_onPulledOutTrigger != null)
                    {
                        _onPulledOutTrigger.active = false;
                        _onPulledOutTrigger.active = true;
                    }
                }
            }
            else if (_isReinserting)
            {
                _transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_transitionTimer / _transitionDuration);
                _penisBaseCtrl.transform.position = Vector3.Lerp(_transitionStartPos, _transitionEndPos, t);

                if (t >= 1f)
                {
                    _isReinserting = false;
                    _isPulledOut = false;
                    _isPullingOut = false;
                    _transitionTimer = 0f;
                    _penisBaseCtrl.transform.position = _transitionEndPos;
                    _previousPenisPos = _transitionEndPos;

                    // Resume thrusting if enabled
                    if (_thrustJSON.val && !_cycler.IsActive)
                    {
                        _speedRampTimer = 0f;
                        _thrustIntensity = 0f;
                        _cycler.StartCycle();
                    }
                }
            }
            else if (_isMovingToRest)
            {
                _transitionTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_transitionTimer / _transitionDuration);
                _penisBaseCtrl.transform.position = Vector3.Lerp(_transitionStartPos, _transitionEndPos, t);

                if (t >= 1f)
                {
                    _isMovingToRest = false;
                    _isFinishingCycle = false;
                    _transitionTimer = 0f;
                    _penisBaseCtrl.transform.position = _transitionEndPos;
                    _RestoreCtrlSnapshot();
                    _ctrlSnapshot = null;
                }
            }
        }

        // ========================================== CUSTOM ========================================== //

        private void OnThrustBegin()
        {
            // Decrement hold counter
            if (_cyclesRemainingAtCurrentSpeed > 0)
            {
                _cyclesRemainingAtCurrentSpeed--;

                // Apply small drift while holding
                float driftAmount = _speedDriftAmountJSON.val;
                float currentDrift = UnityEngine.Random.Range(-driftAmount, driftAmount);
                _targetSpeedDuration = Mathf.Clamp(_targetSpeedDuration + currentDrift, _cycleDurationMinJSON.val, _cycleDurationMaxJSON.val);
            }
            else
            {
                // Time to pick a new speed - determine new hold duration
                int minHold = Mathf.RoundToInt(_speedHoldCyclesMinJSON.val);
                int maxHold = Mathf.RoundToInt(_speedHoldCyclesMaxJSON.val);
                _cyclesRemainingAtCurrentSpeed = UnityEngine.Random.Range(minHold, maxHold + 1);

                // Pick completely new target speed using bias system
                _targetSpeedDuration = _cycler._getWeightedRandomDuration();
            }

            // If we're finishing the cycle and starting a new one, check if it's for pull-out or normal stop
            if (_isFinishingCycle)
            {
                _cycler.StopCycle();
                _isFinishingCycle = false;

                // Check if this is a pull-out request
                if (_isPullingOut || (!_thrustJSON.val && _ctrlSnapshot != null))
                {
                    if (_isPullingOut)
                    {
                        // Calculate pull-out position
                        Vector3 basedir = (_targetManager.TargetPos2 - _targetManager.TargetPos1).normalized;
                        _transitionStartPos = _penisBaseCtrl.transform.position;
                        _transitionEndPos = basedir * _pullOutDistanceJSON.val / 10 + _targetManager.TargetPos1;
                        _transitionTimer = 0f;
                        _transitionDuration = _initialEntryDurationJSON.val;
                        // _isPullingOut stays true, handled in Update
                    }
                    else
                    {
                        // Normal stop - move back to rest position
                        _transitionStartPos = _penisBaseCtrl.transform.position;
                        _transitionEndPos = _penisBaseCtrl.transform.parent.TransformPoint(_ctrlSnapshot.localPosition);
                        _transitionTimer = 0f;
                        _isMovingToRest = true;
                    }
                }
                return;
            }

            _distanceFactor = UnityEngine.Random.Range(1 - _distanceRandomJSON.val, 1 + _distanceRandomJSON.val);
            _personMotionVariability = UnityEngine.Random.Range(1.0f - _personMotionVariabilityJSON.val, 1.0f);
            _targetMotionVariability = UnityEngine.Random.Range(1.0f - _targetMotionVariabilityJSON.val, 1.0f);
        }

        private void OnThrustUpdate(float pct)
        {
            if (__DEBUG__) SuperController.LogMessage($"Updating thrust: pct = {pct}, t = {_T(_cycler.CurrentPct, _easingInJSON.val, _easingOutJSON.val)}");

            // Don't update during freeze
            if (SuperController.singleton.freezeAnimation) return;

            // Don't update position during transitions
            if (_isMovingToTarget || _isMovingToRest || _isPullingOut || _isReinserting) return;

            // Smooth target position tracking
            if (!_targetPositionsInitialized)
            {
                _smoothedTargetPos1 = _targetManager.TargetPos1;
                _smoothedTargetPos2 = _targetManager.TargetPos2;
                _targetPositionsInitialized = true;
            }
            else
            {
                float dampingFactor = Mathf.Clamp01(_targetTrackingDampingJSON.val * Time.deltaTime);
                _smoothedTargetPos1 = Vector3.Lerp(_smoothedTargetPos1, _targetManager.TargetPos1, dampingFactor);
                _smoothedTargetPos2 = Vector3.Lerp(_smoothedTargetPos2, _targetManager.TargetPos2, dampingFactor);
            }

            float easedMagnitude = _T(_cycler.CurrentPct, _easingInJSON.val, _easingOutJSON.val);

            // Smooth force magnitude changes to prevent jitter at transitions
            float smoothingFactor = Mathf.Clamp01(10f * Time.deltaTime / _cycler.CurrentDuration);
            easedMagnitude = Mathf.Lerp(_previousEasedMagnitude, easedMagnitude, smoothingFactor);
            _previousEasedMagnitude = easedMagnitude;

            // Pre-calculate and smooth body force magnitudes with derivative continuity
            float rawChestMagnitude = _GetLaggedMagnitude(0.33f);
            float rawHeadMagnitude = _GetLaggedMagnitude(0.66f);
            float rawHipMagnitude = _GetLaggedMagnitude(0f);

            // Use SmoothDamp for C1 continuity (eliminates jitter at reversal points)
            float smoothTime = 0.05f;
            float smoothedChestMagnitude = Mathf.SmoothDamp(_previousChestMagnitude, rawChestMagnitude, ref _chestVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            float smoothedHeadMagnitude = Mathf.SmoothDamp(_previousHeadMagnitude, rawHeadMagnitude, ref _headVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            float smoothedHipMagnitude = Mathf.SmoothDamp(_previousHipMagnitude, rawHipMagnitude, ref _hipVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);

            _previousChestMagnitude = smoothedChestMagnitude;
            _previousHeadMagnitude = smoothedHeadMagnitude;
            _previousHipMagnitude = smoothedHipMagnitude;

            // Correct amplitude to match pre-smoothing force levels
            const float bodyAmplitudeCorrection = 0.65f;
            smoothedChestMagnitude *= bodyAmplitudeCorrection;
            smoothedHeadMagnitude *= bodyAmplitudeCorrection;
            smoothedHipMagnitude *= bodyAmplitudeCorrection;

            // Store smoothed magnitudes for FixedUpdate application
            _appliedChestMagnitude = smoothedChestMagnitude;
            _appliedHeadMagnitude = smoothedHeadMagnitude;
            _appliedHipMagnitude = smoothedHipMagnitude;

            float speedFactor = (_cycler.CurrentDuration - _cycleDurationMinJSON.val) / Mathf.Max(_cycleDurationMaxJSON.val - _cycleDurationMinJSON.val, 0.01f); float compressionFactor = 1.0f - ((1.0f - speedFactor) * _distanceCompressionJSON.val);
            _appliedCompressionFactor = compressionFactor;
            _appliedPersonVariability = _personMotionVariability;
            _appliedTargetVariability = _targetMotionVariability;

            // Ramp up thrust intensity over time
            if (_speedRampTimer < _speedRampDurationJSON.val)
            {
                _speedRampTimer += Time.deltaTime;
                _thrustIntensity = Mathf.Clamp01(_speedRampTimer / _speedRampDurationJSON.val);
            }
            else
            {
                _thrustIntensity = 1f;
            }
            _appliedThrustIntensity = _thrustIntensity;

            // Update penis position unless frozen
            if (!_freezePenisPositionJSON.val)
            {
                // Apply intensity as a multiplier to the eased magnitude
                float intensityAdjustedMagnitude = easedMagnitude * _thrustIntensity;
                Vector3 newPenisPos = _getPos(intensityAdjustedMagnitude);

                _penisBaseCtrl.transform.position = newPenisPos;
            }

            // Calculate target motion with proportional reverse motion on OUT phase
            float rawReverseMotion = (1f - easedMagnitude) * _outPhaseMotionJSON.val;
            float reverseMotionAmount = Mathf.SmoothDamp(_previousReverseMotion, rawReverseMotion, ref _reverseMotionVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            _previousReverseMotion = reverseMotionAmount;

            // Correct amplitude to match pre-smoothing levels
            reverseMotionAmount *= bodyAmplitudeCorrection;
            _appliedReverseMotion = reverseMotionAmount;
        }

        private void FixedUpdate()
        {
            // Apply all forces in FixedUpdate for physics stability
            if (!_isValid || _isPulledOut || _isFinishingCycle) return;
            if (!_cycler.IsActive) return;

            // Apply forces to person rigidbodies
            if (_chestRB != null)
            {
                Vector3 chestForce = new Vector3(0f, _chestForceYJSON.val, _chestForceZJSON.val);
                Vector3 chestTorque = new Vector3(_chestTorqueXJSON.val, 0f, 0f);
                _chestRB.AddRelativeForce(chestForce * _appliedChestMagnitude * _appliedCompressionFactor * _appliedPersonVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                _chestRB.AddRelativeTorque(chestTorque * _appliedChestMagnitude * _appliedCompressionFactor * _appliedPersonVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }

            if (_headRB != null)
            {
                Vector3 headForce = new Vector3(0f, 0f, _headForceZJSON.val);
                Vector3 headTorque = new Vector3(_headTorqueXJSON.val, 0f, 0f);
                _headRB.AddRelativeForce(headForce * _appliedHeadMagnitude * _appliedCompressionFactor * _appliedPersonVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                _headRB.AddRelativeTorque(headTorque * _appliedHeadMagnitude * _appliedCompressionFactor * _appliedPersonVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }

            if (_hipRB != null)
            {
                Vector3 hipTorque = new Vector3(_hipTorqueXJSON.val, 0f, 0f);
                _hipRB.AddRelativeTorque(hipTorque * _appliedHipMagnitude * _appliedCompressionFactor * _appliedPersonVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }

            // Apply forces to target rigidbodies
            if (_targetHipRB != null)
            {
                float hipMotionMultiplier = _appliedHipMagnitude - _appliedReverseMotion;
                Vector3 targetHipForce = new Vector3(0f, _targetHipForceYJSON.val * hipMotionMultiplier, _targetHipForceZJSON.val * hipMotionMultiplier);
                Vector3 targetHipTorque = new Vector3(_targetHipTorqueXJSON.val * hipMotionMultiplier, 0f, 0f);
                _targetHipRB.AddRelativeForce(targetHipForce * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                _targetHipRB.AddRelativeTorque(targetHipTorque * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }

            if (_targetChestRB != null)
            {
                float chestMotionMultiplier = _appliedChestMagnitude - _appliedReverseMotion;
                Vector3 targetChestForce = new Vector3(0f, _targetChestForceYJSON.val * chestMotionMultiplier, _targetChestForceZJSON.val * chestMotionMultiplier);
                Vector3 targetChestTorque = new Vector3(_targetChestTorqueXJSON.val * chestMotionMultiplier, 0f, 0f);
                _targetChestRB.AddRelativeForce(targetChestForce * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                _targetChestRB.AddRelativeTorque(targetChestTorque * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }

            if (_targetHeadRB != null)
            {
                float headMotionMultiplier = _appliedHeadMagnitude - _appliedReverseMotion;
                // Get head control for stable local coordinate system
                FreeControllerV3 targetHeadCtrl = _targetHeadRB.GetComponent<FreeControllerV3>();

                if (targetHeadCtrl != null)
                {
                    // Apply forces in control node's local space
                    Vector3 targetHeadForceLocal = new Vector3(0f, _targetHeadForceYJSON.val * headMotionMultiplier, _targetHeadForceZJSON.val * headMotionMultiplier);
                    Vector3 targetHeadForceWorld = targetHeadCtrl.transform.TransformDirection(targetHeadForceLocal);
                    _targetHeadRB.AddForce(targetHeadForceWorld * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                }
                else
                {
                    // Fallback to rigidbody local
                    Vector3 targetHeadForce = new Vector3(0f, _targetHeadForceYJSON.val * headMotionMultiplier, _targetHeadForceZJSON.val * headMotionMultiplier);
                    _targetHeadRB.AddRelativeForce(targetHeadForce * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
                }

                Vector3 targetHeadTorque = new Vector3(_targetHeadTorqueXJSON.val * headMotionMultiplier, 0f, _targetHeadTorqueZJSON.val * headMotionMultiplier);
                _targetHeadRB.AddRelativeTorque(targetHeadTorque * _appliedCompressionFactor * _appliedTargetVariability * _appliedThrustIntensity, ForceMode.Acceleration);
            }
        }

        private float _T(float currPct, string easeIn = null, string easeOut = null)
        {
            easeIn = easeIn ?? "Linear";
            easeOut = easeOut ?? "Linear";
            bool r = currPct >= _cyclePctInJSON.val;
            float x = r ? (currPct - _cyclePctInJSON.val) / (1 - _cyclePctInJSON.val) : currPct / _cyclePctInJSON.val;
            return r ? 1 - Easings.EasingFuncs[easeOut](x) : Easings.EasingFuncs[easeIn](x);
        }

        private float _GetLaggedMagnitude(float lagMultiplier)
        {
            float lagAmount = _phaseLagJSON.val * lagMultiplier;
            float laggedPct = Mathf.Clamp01(_cycler.CurrentPct - lagAmount);
            return _T(laggedPct, _easingInJSON.val, _easingOutJSON.val);
        }

        private Vector3 _getPos(float t)
        {
            float speedFactor = (_cycler.CurrentDuration - _cycleDurationMinJSON.val) / Mathf.Max(_cycleDurationMaxJSON.val - _cycleDurationMinJSON.val, 0.01f);
            float compressionAmount = 1.0f - ((1.0f - speedFactor) * _distanceCompressionJSON.val);
            float offsetAmount = (1.0f - speedFactor) * _distanceOffsetJSON.val;

            float distanceIn = _distanceInJSON.val * compressionAmount + offsetAmount;
            float distanceOut = _distanceOutJSON.val * compressionAmount + offsetAmount;
            float thrustDistance = distanceIn - distanceOut;

            // When t=0, we're at distanceOut. When t increases, we move toward distanceIn
            float u = t * thrustDistance * _distanceFactor + distanceOut;
            Vector3 basedir = (_smoothedTargetPos2 - _smoothedTargetPos1).normalized;
            return basedir * u / 10 + _smoothedTargetPos1;
        }

        private void _UpdateState()
        {
            if (!_restoring)
            {
                if (enabled && _isValid)
                {
                    if (_thrustJSON.val)
                    {
                        if (_ctrlSnapshot == null) _ctrlSnapshot = new CtrlSnapshot(_penisBaseCtrl);

                        if (!_cycler.IsActive && !_isMovingToTarget && !_isMovingToRest && !_isFinishingCycle)
                        {
                            _penisBaseCtrl.RBHoldPositionSpring = 10000f;
                            _penisBaseCtrl.currentPositionState = FreeControllerV3.PositionState.On;
                            _previousPenisPos = _penisBaseCtrl.transform.position;

                            // Initialize smoothed target positions
                            _smoothedTargetPos1 = _targetManager.TargetPos1;
                            _smoothedTargetPos2 = _targetManager.TargetPos2;
                            _targetPositionsInitialized = true;

                            // Start transition to target
                            _transitionStartPos = _penisBaseCtrl.transform.position;
                            // Move to the configured "out" position
                            _transitionEndPos = _getPos(0f);
                            _transitionTimer = 0f;
                            _transitionDuration = _initialEntryDurationJSON.val;
                            _isMovingToTarget = true;
                        }
                    }
                    else if (_cycler.IsActive || _isMovingToTarget || _isFinishingCycle)
                    {
                        if (_isMovingToTarget)
                        {
                            _isMovingToTarget = false;
                            _transitionTimer = 0f;
                            // If still moving to target when toggled off, go straight back
                            _transitionStartPos = _penisBaseCtrl.transform.position;
                            _transitionEndPos = _penisBaseCtrl.transform.parent.TransformPoint(_ctrlSnapshot.localPosition);
                            _transitionTimer = 0f;
                            _transitionDuration = _initialEntryDurationJSON.val;
                            _isMovingToRest = true;
                        }
                        else if (_cycler.IsActive && !_isFinishingCycle)
                        {
                            // Stop immediately and transition back
                            _cycler.StopCycle();
                            _transitionStartPos = _penisBaseCtrl.transform.position;
                            _transitionEndPos = _penisBaseCtrl.transform.parent.TransformPoint(_ctrlSnapshot.localPosition);
                            _transitionTimer = 0f;
                            _transitionDuration = _initialEntryDurationJSON.val;
                            _isMovingToRest = true;
                        }
                    }
                    else
                    {
                        // Already stopped, nothing to do
                    }
                }
                else
                {
                    if (_cycler.IsActive || _isMovingToTarget || _isMovingToRest || _isFinishingCycle)
                    {
                        if (_isMovingToTarget)
                        {
                            _isMovingToTarget = false;
                            _transitionTimer = 0f;
                        }
                        if (_cycler.IsActive)
                        {
                            _cycler.StopCycle();
                        }
                        if (_isFinishingCycle)
                        {
                            _isFinishingCycle = false;
                        }
                        if (!_isMovingToRest && _ctrlSnapshot != null)
                        {
                            _transitionStartPos = _penisBaseCtrl.transform.position;
                            _transitionEndPos = _penisBaseCtrl.transform.parent.TransformPoint(_ctrlSnapshot.localPosition);
                            _transitionTimer = 0f;
                            _transitionDuration = _initialEntryDurationJSON.val;
                            _isMovingToRest = true;
                        }
                        else
                        {
                            _Deactivate();
                        }
                    }
                }
            }
        }

        private void _Deactivate()
        {
            _distanceFactor = 1.0f;
            _thrustIntensity = 1f;
            _targetPositionsInitialized = false;
            _previousEasedMagnitude = 0f;
            _previousChestMagnitude = 0f;
            _previousHeadMagnitude = 0f;
            _previousHipMagnitude = 0f;
            _previousReverseMotion = 0f;
            _chestVelocity = 0f;
            _headVelocity = 0f;
            _hipVelocity = 0f;
            _reverseMotionVelocity = 0f;
            _isMovingToTarget = false;
            _isMovingToRest = false;
            _isFinishingCycle = false;
            _speedRampTimer = 0f;
            _transitionTimer = 0f;

            if (_cycler != null && _cycler.IsActive) _cycler.StopCycle();
            _RestoreCtrlSnapshot();
            _ctrlSnapshot = null;
        }

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            if (json.HasKey("thrustJSON"))
            {
                json.Remove("thrustJSON");
            }

            if (json.HasKey("showPresetMessagesJSON"))
            {
                json.Remove("showPresetMessagesJSON");
            }

            var snapshot = Serializer.Serialize(_ctrlSnapshot);
            if (snapshot != null) json["CtrlSnapshot"] = snapshot;

            if (_onPulledOutTrigger != null)
            {
                json["OnPulledOutTrigger"] = _onPulledOutTrigger.GetJSON(base.subScenePrefix);
            }

            needsStore = true;
            return json;
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            _restoring = true;

            bool currentThrustState = _thrustJSON.val;
            bool currentShowMessagesState = _showPresetMessagesJSON.val;

            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);

            _thrustJSON.val = currentThrustState;
            _showPresetMessagesJSON.val = currentShowMessagesState;

            if (jc.HasKey("CtrlSnapshot")) _ctrlSnapshot = Serializer.DeserializeCtrlSnapshot(jc["CtrlSnapshot"] as JSONClass);

            if (jc.HasKey("OnPulledOutTrigger") && _onPulledOutTrigger != null)
            {
                _onPulledOutTrigger.RestoreFromJSON(jc["OnPulledOutTrigger"].AsObject);
            }

            _restoring = false;
        }

        private void _RestoreCtrlSnapshot()
        {
            if (_ctrlSnapshot == null) return;

            _penisBaseCtrl.transform.localPosition = _ctrlSnapshot.localPosition;
            _penisBaseCtrl.currentPositionState = (FreeControllerV3.PositionState)Enum.Parse(typeof(FreeControllerV3.PositionState), _ctrlSnapshot.currentPositionState);
            _penisBaseCtrl.RBHoldPositionSpring = _ctrlSnapshot.RBHoldPositionSpring;
        }

        private void OnCycleDurationChange(float _)
        {
            _previousDuration = Mathf.Clamp(_previousDuration, _cycleDurationMinJSON.val, _cycleDurationMaxJSON.val);
        }

        private UIDynamic CreateDisplayText(string textContent, bool forRightColumn, int fontSize = 28, float preferredHeight = 50f)
        {
            UIDynamic uiDynamicTextHolder = CreateSpacerControl(forRightColumn, preferredHeight);

            Text textComponent = uiDynamicTextHolder.gameObject.AddComponent<Text>();
            textComponent.text = textContent;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = fontSize;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;
            textComponent.supportRichText = true;

            RectTransform rectTransform = textComponent.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.offsetMin = new Vector2(10f, 2f);
            rectTransform.offsetMax = new Vector2(-10f, -2f);

            return uiDynamicTextHolder;
        }

        private UIDynamic CreateSpacerControl(bool rightSide, float? prefHeight = null)
        {
            UIDynamic item = base.CreateSpacer(rightSide);

            LayoutElement layoutElement = item.gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = item.gameObject.AddComponent<LayoutElement>();

            if (prefHeight.HasValue)
            {
                layoutElement.preferredHeight = prefHeight.Value;
                layoutElement.minHeight = prefHeight.Value;
            }
            else
            {
                layoutElement.preferredHeight = 0f;
                layoutElement.minHeight = 0f;
            }
            return item;
        }

        // ========================================== UI ========================================== //

        private void SetupUI()
        {

            _thrustJSON = new JSONStorableBool("<color=#006400><size=32><b>Enable Thrust</b></size></color>", false)
            {
                storeType = JSONStorableParam.StoreType.Physical
            };
            CreateToggle(_thrustJSON);
            RegisterBool(_thrustJSON);
            _thrustJSON.setCallbackFunction += val => { _UpdateState(); };
            CreateSpacer(true).height = 50f;

            _targetManager.SetupUI(this, targetAdjust: false);

            // Preset buttons
            _savePresetActionJSON = new JSONStorableAction("Save Preset", () => {
                string presetDir = "Saves/PluginPresets/AutoThrusterPlus";
                FileManagerSecure.CreateDirectory(presetDir);
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.SetTitle("Save AutoThrusterPlus Preset");
                fileBrowserUI.fileRemovePrefix = null;
                fileBrowserUI.hideExtension = false;
                fileBrowserUI.keepOpen = false;
                fileBrowserUI.fileFormat = "json";
                fileBrowserUI.defaultPath = presetDir;
                fileBrowserUI.showDirs = true;
                fileBrowserUI.shortCuts = null;
                fileBrowserUI.browseVarFilesAsDirectories = false;
                fileBrowserUI.SetTextEntry(true);
                fileBrowserUI.Show((path) => {
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (!path.ToLower().EndsWith(".json")) path += ".json";
                        SuperController.singleton.DoSaveScreenshot(path);
                        SaveJSON(GetJSON(), path);
                        if (_showPresetMessagesJSON != null && _showPresetMessagesJSON.val) SuperController.LogMessage("Preset saved to: " + path);
                    }
                });
                fileBrowserUI.ActivateFileNameField();
            });
            UIDynamicButton savePresetButton = CreateButton("Save Preset", true);
            savePresetButton.buttonColor = new Color(0.6f, 1f, 0.6f);
            _savePresetActionJSON.dynamicButton = savePresetButton;
            RegisterAction(_savePresetActionJSON);

            _loadPresetActionJSON = new JSONStorableAction("Load Preset", () => {
                string presetDir = "Saves/PluginPresets/AutoThrusterPlus";
                FileManagerSecure.CreateDirectory(presetDir);
                var shortcuts = FileManagerSecure.GetShortCutsForDirectory(presetDir);
                SuperController.singleton.GetMediaPathDialog((path) => {
                    if (!string.IsNullOrEmpty(path))
                    {
                        var jc = SuperController.singleton.LoadJSON(path);
                        if (jc != null && jc.AsObject != null)
                        {
                            RestoreFromJSON(jc.AsObject, true, true, null, true);
                            if (_showPresetMessagesJSON != null && _showPresetMessagesJSON.val) SuperController.LogMessage("Preset loaded from: " + path);
                            if (_currentPresetPathDisplayJSON != null)
                            {
                                _currentPresetPathDisplayJSON.val = path;
                            }
                        }
                    }
                }, "json", presetDir, false, true, false, null, false, shortcuts);
            });
            UIDynamicButton loadPresetButton = CreateButton("Load Preset", true);
            loadPresetButton.buttonColor = new Color(0.6f, 1f, 0.6f);
            _loadPresetActionJSON.dynamicButton = loadPresetButton;
            RegisterAction(_loadPresetActionJSON);

            _loadPresetPathJSON = new JSONStorableUrl("LoadPresetPath", "", (path) => {
                if (!string.IsNullOrEmpty(path))
                {
                    var jc = SuperController.singleton.LoadJSON(path);
                    if (jc != null && jc.AsObject != null)
                    {
                        RestoreFromJSON(jc.AsObject, true, true, null, true);
                        if (_showPresetMessagesJSON != null && _showPresetMessagesJSON.val) SuperController.LogMessage("Preset loaded from: " + path);
                        if (_currentPresetPathDisplayJSON != null)
                        {
                            _currentPresetPathDisplayJSON.val = path;
                        }
                    }
                    else
                    {
                        SuperController.LogError("Failed to load preset from: " + path);
                    }
                }
            });
            RegisterUrl(_loadPresetPathJSON);

            CreateSpacer(true).height = 10f;

            _freezePenisPositionJSON = new JSONStorableBool("Disable Penis Thrust", false)
            {
                storeType = JSONStorableParam.StoreType.Physical
            };
            CreateToggle(_freezePenisPositionJSON, true);
            RegisterBool(_freezePenisPositionJSON);

            // Settings - Thrust
            CreateDisplayText("<b>-- Thrust Cycle Controls --</b>", false, 36, 60f);
            CreateSpacer(true).height = 70f;
            _initialEntryDurationJSON = UI_Utils.SetupSlider(this, "Initial Entry Duration", 0.5f, 0f, 1f, true, false);
            _cycleDurationMinJSON = UI_Utils.SetupSlider(this, "Cycle Duration Min", 0.25f, 0.1f, 5.0f, false);
            _cycleDurationMinJSON.setCallbackFunction += OnCycleDurationChange;
            _speedRampDurationJSON = UI_Utils.SetupSlider(this, "Speed Ramp Duration", 3f, 0f, 5f, true, true);
            _cycleDurationMaxJSON = UI_Utils.SetupSlider(this, "Cycle Duration Max", 0.8f, 0.1f, 5.0f, false, true);
            _cycleDurationMaxJSON.setCallbackFunction += OnCycleDurationChange;
            _cyclePctInJSON = UI_Utils.SetupSlider(this, "Duration % In", 0.4f, 0.1f, 0.9f);
            _cycleVariationRateJSON = UI_Utils.SetupSlider(this, "Duration Change Rate", 0.5f, 0.0f, 1.0f, true, true);
            _durationBiasJSON = UI_Utils.SetupSlider(this, "<color=#1010A0><b>Duration Range Bias</b></color>", 0.5f, 0.0f, 1.0f);
            _speedDriftAmountJSON = UI_Utils.SetupSlider(this, "Speed Drift Amount", 0.05f, 0f, 0.5f, true, true);
            _cycler.SetupUI(this, _cycleDurationMinJSON, _cycleDurationMaxJSON, _cycleVariationRateJSON, _durationBiasJSON, () => _previousDuration, (d) => _previousDuration = d, () => _targetSpeedDuration);
            _speedHoldCyclesMinJSON = UI_Utils.SetupSlider(this, "Speed Hold Cycles Min", 2f, 1f, 20f, true, false);
            _speedHoldCyclesMaxJSON = UI_Utils.SetupSlider(this, "Speed Hold Cycles Max", 4f, 1f, 20f, true, true);
            _easingInJSON = new JSONStorableStringChooser("EaseIn", Easings.EasingTypes, "Sinusoidal InOut", "Ease In");
            CreateScrollablePopup(_easingInJSON);
            RegisterStringChooser(_easingInJSON);
            _easingOutJSON = new JSONStorableStringChooser("EaseOut", Easings.EasingTypes, "Sinusoidal InOut", "Ease Out");
            CreateScrollablePopup(_easingOutJSON, true);
            RegisterStringChooser(_easingOutJSON);

            // Settings - Distances
            CreateSpacer().height = 40f;
            CreateSpacer(true).height = 40f;
            _distanceInJSON = UI_Utils.SetupSlider(this, "Distance In", 0.6f, -3.0f, 3.0f);
            _distanceOutJSON = UI_Utils.SetupSlider(this, "Distance Out", -1.0f, -3.0f, 3.0f, true, true);
            _targetTrackingDampingJSON = UI_Utils.SetupSlider(this, "Target Tracking Smoothing", 10f, 0f, 50f, true, true);
            _distanceRandomJSON = UI_Utils.SetupSlider(this, "Distance Random %", 0.25f, 0f, 0.5f, false);
            _distanceCompressionJSON = UI_Utils.SetupSlider(this, "Distance Compression at Max Speed", 0.2f, 0f, 1.0f);
            _distanceOffsetJSON = UI_Utils.SetupSlider(this, "Distance Offset at Max Speed", 0.3f, -2.0f, 2.0f, true, true);
            CreateSpacer(true).height = 40f;

            // Body motion
            CreateDisplayText("<b>-- Extra Body Movements - Thruster --</b>", false, 36, 60f);
            CreateSpacer(true).height = 5f;

            _chestForceYJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Chest Force Y (Up+)</color>", 0f, -2000f, 2000f, false);
            _hipTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Hip Torque X</color>", 0f, -500f, 500f, false, true);
            _chestForceZJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Chest Force Z (Forward+)</color>", 0f, -2000f, 2000f, false);
            _headForceZJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Head Force Z (Forward+)</color>", 0f, -2000f, 2000f, false, true);
            _chestTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Chest Torque X</color>", 0f, -500f, 500f, false);
            _headTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Head Torque X</color>", 0f, -500f, 500f, false, true);

            _personMotionVariabilityJSON = UI_Utils.SetupSlider(this, "<color=#00008B>Person Body Motion Variability</color>", 0f, 0f, 1f, false);
            CreateSpacer(true).height = 120f;

            CreateSpacer().height = 20f;
            CreateSpacer(true).height = 20f;

            _targetChestForceYJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Chest Force Y (Up+)</color>", 0f, -2000f, 2000f, false);
            _targetHipForceYJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Hip Force Y (Up+)</color>", 0f, -2000f, 2000f, false, true);
            _targetChestForceZJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Chest Force Z (Forward+)</color>", 0f, -2000f, 2000f, false);
            _targetHipForceZJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Hip Force Z (Forward+)</color>", 0f, -2000f, 2000f, false, true);
            _targetChestTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Chest Torque X</color>", 0f, -500f, 500f, false);
            _targetHipTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Hip Torque X</color>", 0f, -500f, 500f, false, true);

            CreateSpacer().height = 20f;
            CreateSpacer(true).height = 20f;

            _targetHeadForceYJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Head Force Y (Up+)</color>", 0f, -2000f, 2000f, false);
            _targetHeadTorqueXJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Head Torque X</color>", 0f, -500f, 500f, false, true);
            _targetHeadForceZJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Head Force Z (Forward+)</color>", 0f, -2000f, 2000f, false);
            _targetHeadTorqueZJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Head Torque Z</color>", 0f, -500f, 500f, false, true);

            _targetMotionVariabilityJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>Target Body Motion Variability</color>", 0f, 0f, 1f, false);

            _outPhaseMotionJSON = UI_Utils.SetupSlider(this, "<color=#8B008B>OUT Phase Motion %</color>", 0f, 0f, 1f, true, true);

            _phaseLagJSON = UI_Utils.SetupSlider(this, "<color=#8B008B><b>Phase Lag</b></color>", 0f, 0f, 0.3f, false);
            CreateDisplayText("<b>← Delays body motion from \n hip → chest → head \n for more natural wave-like movement.</b>", true, 22, 80f);

            // Target adjustments
            CreateSpacer(true).height = 25f;
            CreateDisplayText("<b>-- Target Adjustments --</b>", false, 36, 60f);
            CreateDisplayText("<b>-- Pull Out Trigger --</b>", true, 36, 60f);
            _targetManager.SetupUI(this, false);

            // Pull-out controls
            _pullOutDistanceJSON = UI_Utils.SetupSlider(this, "Pull Out Distance", -2.0f, -5.0f, 0.0f, true, true);

            _pullOutActionJSON = new JSONStorableAction("Pull Out", DoPullOut);
            UIDynamicButton pullOutButton = CreateButton("Pull Out", true);
            pullOutButton.buttonColor = new Color(0.6f, 1f, 0.6f);
            _pullOutActionJSON.dynamicButton = pullOutButton;
            RegisterAction(_pullOutActionJSON);

            _reInsertActionJSON = new JSONStorableAction("Re-Insert", DoReInsert);
            UIDynamicButton reInsertButton = CreateButton("Re-Insert", true);
            reInsertButton.buttonColor = new Color(0.6f, 1f, 0.6f);
            _reInsertActionJSON.dynamicButton = reInsertButton;
            RegisterAction(_reInsertActionJSON);

            UIDynamicButton openPulledOutTriggerButton = CreateButton("On Pulled Out Trigger...", true);
            openPulledOutTriggerButton.buttonColor = new Color(0.6f, 0.8f, 1f);
            openPulledOutTriggerButton.button.onClick.AddListener(() => {
                if (_onPulledOutTrigger != null) _onPulledOutTrigger.OpenTriggerActionsPanel();
            });

            CreateSpacer(true).height = 20f;

            // Instructions
            string instructionText =
                "<color=#FFFFFF><size=36><b>Settings Information</b></size></color>\n\n" +
                "<color=#FFFFFF><b>Activation Settings</b></color>\n" +
                "<b>Enable Thrust</b>: Turns thrusting on or off.\n" +
                "<b>Disable Penis Thrust</b>: Keeps the penis stationary while still applying body forces and torques. Useful for certain positions or when you want only body motion without penis movement.\n\n" +

                "<color=#FFFFFF><b>Target Settings</b></color>\n" +
                "<b>Target Atom</b>: Name of target Atom.\n" +
                "<b>Target</b>: Anus, Mouth, or Vagina.\n\n" +

                "<color=#FFFFFF><b>Cycle Settings</b></color>\n" +
                "<b>Initial Entry Duration</b>: Time in seconds for the initial smooth transition when thrusting starts or when re-inserting. Longer durations make the entry more gradual and gentle.\n" +
                "<b>Cycle Duration Min:</b> Minimum duration of a complete <i>In</i> and <i>Out</i> motion.\n" +
                "<b>Speed Ramp Duration</b>: Time in seconds to gradually increase thrust intensity from 0 to 100% when thrusting begins. This creates a smooth, natural ramp-up rather than starting at full intensity immediately.\n" +
                "<b>Cycle Duration Max:</b> Maximum duration of a complete <i>In</i> and <i>Out</i> motion.\n" +
                "<b>Variation Rate:</b> How quickly the cycle duration changes between min and max. 0 = slow drifting changes, 1 = rapid changes every cycle.\n" +
                "<b>Duration Range Bias</b>: Controls which speeds are favored when randomly selecting cycle durations. 0 = favors faster speeds, 0.5 = equal probability across the range, 1 = favors slower speeds. Creates a bell curve distribution around the bias point.\n" +
                "<b>Speed Drift Amount</b>: Small random variations applied to speed while holding at a target speed. Adds subtle, natural fluctuations to make motion less robotic.\n" +
                "<b>Speed Hold Cycles Min/Max</b>: How many thrust cycles to maintain approximately the same speed before picking a new target speed. Creates periods of consistent rhythm followed by speed changes.\n" +
                "<b>Cycle Duration % In:</b> Proportion of Cycle Duration taken by the <i>In</i> motion.\n" +
                " 0.5 = <i>In</i> and <i>Out</i> have equal duration.\n" +
                "Less than 0.5 = <i>In</i> motion is faster than <i>Out</i> motion.\n" +
                "More than 0.5 = <i>In</i> motion is slower than <i>Out</i> motion.\n" +
                "<b>Ease In:</b> Easing function for <i>In</i> motion. See https://easings.net for reference.\n" +
                "<b>Ease Out:</b> Easing function for <i>Out</i> motion.\n\n" +

                "<color=#FFFFFF><b>Distances</b></color>\n" +
                "<b>Distance In/Out</b>: Endpoints of the thrust motion at slowest cycle speed. Zero is the target's location. Positive values mean towards the target, and negative values mean away from the target.\n" +
                "<b>Target Tracking Smoothing</b>: How smoothly the penis follows when the target person moves. Higher values = smoother following but slightly delayed, lower values = more immediate following but potentially jerky if target moves quickly.\n" +
                "<b>Distance Random %</b>: Proportion of Distance to randomly add/subtract from each cycle.\n" +
                "<b>Distance Compression at Max Speed</b>: How much to reduce the thrust distance when at maximum cycle speed. 0 = no change, 1 = distances compressed to center point.\n" +
                "<b>Distance Offset at Max Speed</b>: How much to shift the center point of the thrust when at maximum cycle speed. Positive = shift towards target, negative = shift away from target.\n\n" +

                "<color=#FFFFFF><b>Body Motion</b></color>\n" +
                "All forces and torques are applied relative to each rigidbody's local coordinate system and are scaled by the easing function (0-1 over the cycle).\n\n" +
                "<b>Force X/Y/Z</b>: Linear force in the rigidbody's local axes. X=Right(+)/Left(-), Y=Up(+)/Down(-), Z=Forward(+)/Backward(-). Range -2000 to 2000.\n" +
                "<b>Torque X/Y/Z</b>: Rotational force around the rigidbody's local axes. X=Pitch, Y=Yaw, Z=Roll. Range -500 to 500.\n\n" +
                "<b>Chest Force Y</b>: Vertical force on person's chest.\n" +
                "<b>Chest Force Z</b>: Forward/backward force on person's chest.\n" +
                "<b>Chest Torque X</b>: Pitch rotation on person's chest.\n" +
                "<b>Hip Torque X</b>: Pitch rotation on person's hip.\n" +
                "<b>Head Force Z</b>: Forward/backward force on person's head.\n" +
                "<b>Head Torque X</b>: Pitch rotation on person's head.\n" +
                "<b>Target Hip Force Y</b>: Vertical force on target's hip.\n" +
                "<b>Target Hip Force Z</b>: Forward/backward force on target's hip.\n" +
                "<b>Target Hip Torque X</b>: Pitch rotation on target's hip.\n" +
                "<b>Target Chest Force Y</b>: Vertical force on target's chest.\n" +
                "<b>Target Chest Force Z</b>: Forward/backward force on target's chest.\n" +
                "<b>Target Chest Torque X</b>: Pitch rotation on target's chest.\n" +
                "<b>Target Head Force Y</b>: Vertical force on target's head.\n" +
                "<b>Target Head Force Z</b>: Forward/backward force on target's head.\n" +
                "<b>Target Head Torque X</b>: Pitch rotation on target's head.\n" +
                "<b>Target Head Torque Z</b>: Roll rotation on target's head.\n\n" +

                "<color=#FFFFFF><b>Body Motion Variability</b></color>\n" +
                "<b>Person Body Motion Variability</b>: Adds randomness to person's body forces/torques each cycle. 0 = no variation (uses exact slider values), 1 = maximum variation (random between 0 and slider value). Default is 0 for backward compatibility.\n" +
                "<b>Target Body Motion Variability</b>: Adds randomness to target's body forces/torques each cycle. 0 = no variation (uses exact slider values), 1 = maximum variation (random between 0 and slider value). Default is 0 for backward compatibility.\n" +
                "<b>OUT Phase Motion %</b>: Makes the target move in the opposite direction during the OUT phase of thrusting. 0 = no reverse motion, higher values = target pulls back more during withdrawal. Creates a more dynamic, push-pull interaction.\n" +
                "<b>Phase Lag</b>: Delays body motion from hip to chest to head, creating a natural wave-like movement through the body. 0 = all parts move together, higher values = more pronounced wave effect. Makes motion look more organic and realistic.\n\n" +

                "<color=#FFFFFF><b>Adjustments</b></color>\n" +
                "<b>Target Adj. Left-Right</b>: Offsets the target locations left or right, relative to the target. Each target type (Anus/Mouth/Vagina) stores its own offset values.\n" +
                "<b>Target Adj. Down-Up</b>: Offsets the target locations down or up, relative to the target. Each target type (Anus/Mouth/Vagina) stores its own offset values.\n" +
                "<b>Target Adj. Back-Forward</b>: Offsets the target locations backward or forward along the thrust direction, relative to the target. Each target type (Anus/Mouth/Vagina) stores its own offset values.\n" +
                "<b>Show Target Points</b>: Displays visualization spheres at the two target points. Green sphere = shallow point, Red sphere = deep point.\n\n" +

                "<color=#FFFFFF><b>Pull Out Controls</b></color>\n" +
                "<b>Pull Out Distance</b>: How far to pull out when the Pull Out button is pressed. Negative values pull away from the target.\n" +
                "<b>Pull Out Button</b>: Immediately completes the current thrust cycle and smoothly pulls out to the specified distance.\n" +
                "<b>Re-Insert Button</b>: Smoothly re-inserts and resumes thrusting after pulling out.\n" +
                "<b>On Pulled Out Trigger</b>: Opens a trigger panel that fires when pull-out is complete. Use this to trigger other actions, animations, or scenes when fully withdrawn.\n\n" +

                "<color=#FFFFFF><b>Presets</b></color>\n" +
                "<b>Save Preset</b>: Saves all current settings to a JSON file for later reuse.\n" +
                "<b>Load Preset</b>: Loads previously saved settings from a JSON file.\n\n";

            CreateTextField(new JSONStorableString("text", instructionText), true).height = 1400;

            _currentPresetPathDisplayJSON = new JSONStorableString("CurrentPresetPath", "No preset loaded");
            UIDynamicTextField currentPresetDisplay = CreateTextField(_currentPresetPathDisplayJSON, false);
            currentPresetDisplay.height = 60f;
            RegisterString(_currentPresetPathDisplayJSON);

            _showPresetMessagesJSON = new JSONStorableBool("Show Preset Messages", true)
            {
                storeType = JSONStorableParam.StoreType.Physical
            };
            CreateToggle(_showPresetMessagesJSON, false);
            RegisterBool(_showPresetMessagesJSON);

        }

    }


}
