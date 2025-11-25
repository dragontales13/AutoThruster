using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

namespace Dragontales
{

    public class CtrlSnapshot
    {
        public string name = string.Empty;
        public string currentPositionState = string.Empty;
        public string currentRotationState = string.Empty;
        public float RBHoldPositionSpring = 2000;
        public float RBHoldRotationSpring = 250;
        public Vector3 localPosition = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
        public Matrix4x4 worldMatrix = Matrix4x4.identity;

        public CtrlSnapshot(FreeControllerV3 control = null)
        {
            if (control == null) return;

            name = control.name;
            currentPositionState = control.currentPositionState.ToString();
            currentRotationState = control.currentRotationState.ToString();
            RBHoldPositionSpring = control.RBHoldPositionSpring;
            RBHoldRotationSpring = control.RBHoldRotationSpring;
            localPosition = control.transform.localPosition;
            localRotation = control.transform.localRotation;
            worldMatrix = control.transform.localToWorldMatrix;
        }
    }

    public class Cycler
    {
        public bool __DEBUG__ = false;

        private bool _isActive = false;

        private float _currTime = 0.0f;
        private Coroutine _co_instance = null;

        public bool IsActive => _isActive;
        public float CurrentPct => _currTime / CurrentDuration;

        public float CurrentDuration = 1.0f;

        private readonly MonoBehaviour _mono;
        private Action _onBegin;
        private Action<float> _onUpdate;


        private JSONStorableFloat _cycleDurationMinJSON;
        private JSONStorableFloat _cycleDurationMaxJSON;
        private JSONStorableFloat _cycleVariationRateJSON;
        private JSONStorableFloat _durationBiasJSON;
        private Func<float> _getPreviousDuration;
        private Action<float> _setPreviousDuration;

        public Cycler(MonoBehaviour mono, Action OnBegin = null, Action<float> OnUpdate = null)
        {
            _mono = mono;
            _onBegin = OnBegin ?? (() => { });
            _onUpdate = OnUpdate ?? ((_) => { });
        }

        public void StartCycle()
        {
            if (_co_instance == null)
            {
                _currTime = 0.0f;
                _isActive = true;
                _co_instance = _mono.StartCoroutine(_cycle());
                if (__DEBUG__) SuperController.LogMessage($"Starting cycle... (_co_instance = {_co_instance})");
            }
        }

        public void StartCycleAt(float pct)
        {
            if (_co_instance == null)
            {
                float biasedMin = Mathf.Lerp(_cycleDurationMinJSON.val, _cycleDurationMaxJSON.val, Mathf.Max(0, _durationBiasJSON.val * 2 - 1));
                float biasedMax = Mathf.Lerp(_cycleDurationMinJSON.val, _cycleDurationMaxJSON.val, Mathf.Min(1, _durationBiasJSON.val * 2));
                float targetDuration = UnityEngine.Random.Range(biasedMin, biasedMax);
                float previousDuration = _getPreviousDuration();
                CurrentDuration = Mathf.Max(Mathf.Lerp(previousDuration, targetDuration, _cycleVariationRateJSON.val), 0.1f);
                _setPreviousDuration(CurrentDuration);
                _currTime = pct * CurrentDuration;
                _isActive = true;
                _co_instance = _mono.StartCoroutine(_cycle());
                if (__DEBUG__) SuperController.LogMessage($"Starting cycle at {pct * 100}%... (_co_instance = {_co_instance})");
            }
        }

        public void StopCycle()
        {
            if (__DEBUG__) SuperController.LogMessage($"Stopping cycle... (_co_instance = {_co_instance})");
            if (_co_instance == null) return;

            _mono.StopCoroutine(_co_instance);
            _co_instance = null;
            _isActive = false;
        }

        private IEnumerator _cycle()
        {
            float targetDuration = _getTargetDuration();
            float previousDuration = _getPreviousDuration();
            CurrentDuration = Mathf.Max(Mathf.Lerp(previousDuration, targetDuration, _cycleVariationRateJSON.val), 0.1f);
            _setPreviousDuration(CurrentDuration);
            _onBegin();

            while (_currTime <= CurrentDuration)
            {
                _onUpdate(CurrentPct);
                _currTime += Time.deltaTime;
                yield return null;
            }

            _endCycle();
        }

        private void _endCycle()
        {
            _co_instance = null;

            if (_isActive) StartCycle();
        }

        public void SetupUI(MVRScript script, JSONStorableFloat minJSON, JSONStorableFloat maxJSON, JSONStorableFloat variationJSON, JSONStorableFloat biasJSON, Func<float> getPrevious, Action<float> setPrevious, Func<float> getTarget)
        {
            _getTargetDuration = getTarget;
            _cycleDurationMinJSON = minJSON;
            _cycleDurationMaxJSON = maxJSON;
            _cycleVariationRateJSON = variationJSON;
            _durationBiasJSON = biasJSON;
            _getPreviousDuration = getPrevious;
            _setPreviousDuration = setPrevious;
        }

        private Func<float> _getTargetDuration;

        public float _getWeightedRandomDuration()
        {
            float bias = _durationBiasJSON.val;
            float minDuration = _cycleDurationMinJSON.val;
            float maxDuration = _cycleDurationMaxJSON.val;

            // Calculate how far we are from the center (0.5)
            // 0 = at center, 1 = at either extreme
            float distanceFromCenter = Mathf.Abs(bias - 0.5f) * 2f;

            // At center (0.5): use flat random
            // At extremes (0 or 1): use bell curve
            if (distanceFromCenter < 0.1f)
            {
                // Near center - use flat distribution
                return Mathf.Lerp(minDuration, maxDuration, UnityEngine.Random.value);
            }

            // Use bell curve, with tightness based on distance from center
            float u1 = UnityEngine.Random.value;
            float u2 = UnityEngine.Random.value;
            float stdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);

            // Standard deviation decreases as we move away from center
            // At 0.5: wider spread, at 0 or 1: tighter spread
            float stdDev = Mathf.Lerp(0.5f, 0.15f, distanceFromCenter);

            // Center the bell curve at the bias position
            float normalizedValue = bias + (stdNormal * stdDev);
            normalizedValue = Mathf.Clamp01(normalizedValue);

            return Mathf.Lerp(minDuration, maxDuration, normalizedValue);
        }

    }

    public class SyncManager
    {
        private MVRScript _mvr;
        public JSONStorableStringChooser _masterAtomJSON;
        public JSONStorableStringChooser _masterPluginJSON;

        private JSONStorable _masterPlugin = null;
        public bool masterValid => _IsMasterValid();
        public bool masterActive => masterValid && _masterPlugin.GetBoolJSONParam("isActive").val;
        public float CurrentPct => _masterPlugin == null ? 0f : _masterPlugin.GetFloatParamValue("cyclePct");

        public SyncManager(MVRScript mvr)
        {
            _mvr = mvr;
        }

        private bool _IsMasterValid()
        {
            if (_masterPlugin != null && _masterPlugin.containingAtom.uid == _masterAtomJSON.val && _masterPlugin.storeId == _masterPluginJSON.val)
                return true;

            try
            {
                _masterPlugin = Scene_Utils.GetThrusterPlugins(_masterAtomJSON.val).First(p => p.storeId == _masterPluginJSON.val);
            }
            catch (Exception e)
            {
                _masterPlugin = null;
            }

            return _masterPlugin != null;
        }

        private List<string> _GetMasterAtomList()
        {
            List<string> atoms = new List<string> { string.Empty };
            atoms.AddRange(Scene_Utils.GetThrusterAtomIds(_mvr));
            return atoms;
        }

        private List<string> _GetMasterPluginList()
        {
            List<string> plugins = new List<string> { string.Empty };
            plugins.AddRange(Scene_Utils.GetThrusterPlugins(_masterAtomJSON.val).ConvertAll(p => p.storeId));
            plugins.Remove(_mvr.storeId);
            return plugins;
        }

        private void OnMasterAtomChange(string uid)
        {
            // _masterPluginJSON.SetValToDefault();
        }
        private void OnMasterPluginChange(string pid)
        {
            // JSONStorable plugin = Scene_Utils.GetThrusterPlugins(_masterAtomJSON.val).First(p => p.storeId == _masterPluginJSON.val);
            // if (plugin == null)
            // {
            //     SuperController.LogError($"Could not find thruster plugin: {_masterAtomJSON.val}: {_masterPluginJSON.val}");
            //     _masterPluginJSON.SetValToDefault();
            //     return;
            // }
        }

        public void SetupUI(MVRScript script)
        {
            _masterAtomJSON = new JSONStorableStringChooser("_masterAtomJSON", _GetMasterAtomList(), string.Empty, "Master Atom");
            _masterAtomJSON.setCallbackFunction += OnMasterAtomChange;
            script.RegisterStringChooser(_masterAtomJSON);
            script.CreateFilterablePopup(_masterAtomJSON).popup.onOpenPopupHandlers += () => { _masterAtomJSON.choices = _GetMasterAtomList(); };
            _masterPluginJSON = new JSONStorableStringChooser("_masterPluginJSON", _GetMasterPluginList(), string.Empty, "Master Plugin");
            _masterPluginJSON.setCallbackFunction += OnMasterPluginChange;
            script.RegisterStringChooser(_masterPluginJSON);
            script.CreateFilterablePopup(_masterPluginJSON).popup.onOpenPopupHandlers += () => { _masterPluginJSON.choices = _GetMasterPluginList(); };
        }
    }

    public static class Easings
    {
        // Constants for easing functions
        private static readonly float BACK_C1 = 1.70158f;
        private static readonly float BACK_C2 = BACK_C1 + 1;
        private static readonly float BACK_C3 = BACK_C1 * 1.525f;
        private static readonly float BOUNCE_C1 = 7.5625f;
        private static readonly float BOUNCE_C2 = 2.75f;

        // Easing functions
        public static Dictionary<string, Func<float, float>> EasingFuncs = new Dictionary<string, Func<float, float>>
    {
        // Reference: https://easings.net/
        {"Linear", f => f},
        {"Quadratic In", f => (float)Math.Pow(f, 2) },
        {"Quadratic Out", f => (float)(1 - Math.Pow(1 - f, 2)) },
        {"Quadratic InOut", f => f < 0.5 ? (float)(2 * Math.Pow(f, 2)) : (float)(1 - Math.Pow(2 * (1 - f), 2) / 2) },
        {"Cubic In", f => (float)Math.Pow(f, 3) },
        {"Cubic Out", f => (float)(1 - Math.Pow(1 - f, 3)) },
        {"Cubic InOut", f => f < 0.5 ? (float)(4 * Math.Pow(f, 3)) : (float)(1 - Math.Pow(2 * (1 - f), 3) / 2) },
        {"Sinusoidal In", f => (float)(1 - Math.Cos(f * Math.PI / 2)) },
        {"Sinusoidal Out", f => (float)Math.Sin(f * Math.PI / 2) },
        {"Sinusoidal InOut", f => -(float)(Math.Cos(Math.PI * f) - 1) / 2 },
        {"Exponential In", f => f == 0 ? 0 : (float)Math.Pow(2, 10 * f - 10) },
        {"Exponential Out", f => f == 1 ? 1 : (float)(1 - Math.Pow(2, -10 * f)) },
        {"Exponential InOut", f => f == 0 || f == 1 ? f :  f < 0.5 ? (float)Math.Pow(2, 20 * f - 10) / 2 : (float)(2 - Math.Pow(2, -20 * f + 10)) / 2 },
        {"Back In", f => (float)(BACK_C2 * Math.Pow(f, 3) - BACK_C1 * Math.Pow(f, 2)) },
        {"Back Out", f => (float)(1 + BACK_C2 * Math.Pow(f - 1, 3) + BACK_C1 * Math.Pow(f - 1, 2)) },
        {"Back InOut", f => f < 0.5 ? (float)(Math.Pow(2 * f, 2) * ((BACK_C3 + 1) * 2 * f - BACK_C3)) / 2 : (float)(Math.Pow(2 * f - 2, 2) * ((BACK_C3 + 1) * (f * 2 - 2) + BACK_C3) + 2) / 2 },
        {"Bounce In", f => 1 - _bounceOut(1 - f) },
        {"Bounce Out", _bounceOut },
        {"Bounce InOut", f => f < 0.5 ? (1 - _bounceOut(1 - 2*f)) / 2 : (1 + _bounceOut(2*f - 1)) / 2 }
    };

        private static float _bounceOut(float f)
        {
            if (f < 1 / BOUNCE_C2)
                return BOUNCE_C1 * f * f;

            if (f < 2 / BOUNCE_C2)
                return BOUNCE_C1 * (f -= 1.5f / BOUNCE_C2) * f + 0.75f;

            if (f < 2.5 / BOUNCE_C2)
                return BOUNCE_C1 * (f -= 2.25f / BOUNCE_C2) * f + 0.9375f;

            return BOUNCE_C1 * (f -= 2.625f / BOUNCE_C2) * f + 0.984375f;
        }

        public static List<string> EasingTypes => EasingFuncs.Keys.ToList();
    }

    public static class Scene_Utils
    {
        public static Vector3 PositionFromMatrix(Matrix4x4 mat)
        {
            if (mat == null) return Vector3.zero;

            return new Vector3(mat[0, 3], mat[1, 3], mat[2, 3]);
        }
        public static List<string> ControlNames(Atom hostAtom)
        {
            if (hostAtom == null) return new List<string> { };

            return hostAtom.freeControllers.Select(c => c.name).ToList(); ;
        }

        public static List<string> PersonNames => SuperController.singleton.GetAtoms().Where(atom => atom.category == "People").Select(atom => atom.name).ToList();

        public static List<JSONStorable> GetThrusterPlugins(Atom hostAtom = null)
        {
            List<Atom> atoms = hostAtom ? new List<Atom>() { hostAtom } : SuperController.singleton.GetAtoms();
            List<JSONStorable> plugins = new List<JSONStorable>();
            foreach (var atom in atoms)
            {
                if (atom != null)
                {
                    plugins.AddRange(atom.GetStorableIDs().FindAll(id => id.EndsWith("AutoThruster") || id.EndsWith("AutoGensThruster")).ConvertAll<JSONStorable>(sid => atom.GetStorableByID(sid)));
                }
            }
            return plugins;
        }

        public static List<JSONStorable> GetThrusterPlugins(string atomId)
        {
            return GetThrusterPlugins(SuperController.singleton.GetAtomByUid(atomId));
        }

        public static List<string> GetThrusterAtomIds(MVRScript caller = null)
        {
            List<string> atomIds = new List<string>();
            foreach (var plugin in GetThrusterPlugins())
            {
                if (ReferenceEquals(plugin, caller)) continue;
                string pluginAtomId = plugin.containingAtom.uid;
                if (!atomIds.Contains(pluginAtomId)) atomIds.Add(pluginAtomId);
            }
            return atomIds;
        }

        public static void BroadcastEvent(string method, List<string> ev, MVRScript exclude = null)
        {
            foreach (var thruster in GetThrusterPlugins())
            {
                if (!ReferenceEquals(exclude, thruster)) thruster.SendMessage(method, ev);
            }
        }

    }

    public static class Serializer
    {
        public static JSONClass Serialize(Matrix4x4 mat)
        {
            if (mat == null) mat = Matrix4x4.identity;

            var jc = new JSONClass
            {
                ["0"] = Serialize(mat.GetColumn(0)),
                ["1"] = Serialize(mat.GetColumn(1)),
                ["2"] = Serialize(mat.GetColumn(2)),
                ["3"] = Serialize(mat.GetColumn(3))
            };
            return jc;
        }
        public static JSONClass Serialize(Quaternion rot)
        {
            return Serialize(new Vector4(rot.x, rot.y, rot.z, rot.w));
        }

        public static JSONClass Serialize(Vector3 pos)
        {
            var jc = new JSONClass
            {
                ["x"] = { AsFloat = pos.x },
                ["y"] = { AsFloat = pos.y },
                ["z"] = { AsFloat = pos.z }
            };
            return jc;
        }

        public static JSONClass Serialize(Vector4 vec)
        {
            var jc = new JSONClass
            {
                ["x"] = { AsFloat = vec.x },
                ["y"] = { AsFloat = vec.y },
                ["z"] = { AsFloat = vec.z },
                ["w"] = { AsFloat = vec.w },
            };
            return jc;
        }

        public static JSONClass Serialize(CtrlSnapshot snapshot)
        {
            if (snapshot == null || snapshot.name == string.Empty) return null;

            return new JSONClass
            {
                ["name"] = snapshot.name,
                ["currentPositionState"] = snapshot.currentPositionState.ToString(),
                ["currentRotationState"] = snapshot.currentRotationState.ToString(),
                ["RBHoldPositionSpring"] = snapshot.RBHoldPositionSpring.ToString(),
                ["RBHoldRotationSpring"] = snapshot.RBHoldRotationSpring.ToString(),
                ["localPosition"] = Serialize(snapshot.localPosition),
                ["localRotation"] = Serialize(snapshot.localRotation),
                ["worldMatrix"] = Serialize(snapshot.worldMatrix)
            };
        }

        public static Matrix4x4 DeserializeMatrix4x4(JSONClass jc)
        {
            return new Matrix4x4
            (
                DeserializeVector4(jc["0"] as JSONClass),
                DeserializeVector4(jc["1"] as JSONClass),
                DeserializeVector4(jc["2"] as JSONClass),
                DeserializeVector4(jc["3"] as JSONClass)
            );
        }

        public static Quaternion DeserializeQuaternion(JSONClass jc)
        {
            return new Quaternion
            (
                jc["x"].AsFloat,
                jc["y"].AsFloat,
                jc["z"].AsFloat,
                jc["w"].AsFloat
            );
        }

        public static Vector3 DeserializeVector3(JSONClass jc)
        {
            return new Vector3
            (
                jc["x"].AsFloat,
                jc["y"].AsFloat,
                jc["z"].AsFloat
            );
        }
        public static Vector4 DeserializeVector4(JSONClass jc)
        {
            return new Vector4
            (
                jc["x"].AsFloat,
                jc["y"].AsFloat,
                jc["z"].AsFloat,
                jc["w"].AsFloat
            );
        }

        public static CtrlSnapshot DeserializeCtrlSnapshot(JSONClass jc)
        {

            try
            {
                var settings = new CtrlSnapshot
                {
                    name = jc["name"].Value,
                    currentPositionState = jc["currentPositionState"].Value,
                    currentRotationState = jc["currentRotationState"].Value,
                    RBHoldPositionSpring = jc["RBHoldPositionSpring"].AsFloat,
                    RBHoldRotationSpring = jc["RBHoldRotationSpring"].AsFloat,
                    localPosition = DeserializeVector3(jc["localPosition"] as JSONClass),
                    localRotation = DeserializeQuaternion(jc["localRotation"] as JSONClass),
                    worldMatrix = DeserializeMatrix4x4(jc["worldMatrix"] as JSONClass)
                };

                return settings;
            }
            catch (Exception e)
            {
                SuperController.LogError($"{e}");
                return null;
            }
        }
    }

    public class TargetManager
    {
        private readonly bool __DEBUG__ = false;
        private JSONStorableStringChooser _targetAtomJSON;
        private JSONStorableFloat _anusOffsetXJSON;
        private JSONStorableFloat _anusOffsetYJSON;
        private JSONStorableFloat _anusOffsetZJSON;
        private JSONStorableFloat _mouthOffsetXJSON;
        private JSONStorableFloat _mouthOffsetYJSON;
        private JSONStorableFloat _mouthOffsetZJSON;
        private JSONStorableFloat _vaginaOffsetXJSON;
        private JSONStorableFloat _vaginaOffsetYJSON;
        private JSONStorableFloat _vaginaOffsetZJSON;
        private JSONStorableBool _showTargetPointsJSON;
        private GameObject _targetPoint1Viz;
            private GameObject _targetPoint2Viz;

            private Atom _targetAtom;
            private static readonly List<string> _targetChoices = new List<string> { "Anus", "Mouth", "Vagina" };
        private static readonly Dictionary<string, List<string>> _targetNames = new Dictionary<string, List<string>>
        {
            {"Anus", new List<string> {"LabiaTrigger", "DeepVaginaTrigger"}},
            {"Mouth", new List<string> {"LipTrigger", "MouthTrigger"}},
            {"Vagina", new List<string> {"VaginaTrigger", "DeepVaginaTrigger"}}
        };
        private Rigidbody _target1;
        private Rigidbody _target2;
        public JSONStorableStringChooser _targetJSON;
        public Vector3 TargetPos1 => _target1.transform.position - OffsetDirY * (_targetJSON.val == "Anus" ? 0.04f : _targetJSON.val == "Mouth" ? 0.01f : 0f) + _posOffset;
        public Vector3 TargetPos2 => _target2.transform.position + _posOffset;
        public Vector3 PosOffset => _posOffset;

        public Rigidbody TargetRB1 => _target1;
        public Rigidbody TargetRB2 => _target2;
        public Vector3 OffsetDirX => _target1.transform.right;
        public Vector3 OffsetDirY => _targetJSON.val == "Mouth" ? _target1.transform.up : _target1.transform.forward;
        public Vector3 OffsetDirZ => (_target2.transform.position - _target1.transform.position).normalized;
        private Vector3 _posOffset
        {
            get
            {
                if (_targetJSON == null) return Vector3.zero;
                float x = 0f, y = 0f, z = 0f;
                if (_targetJSON.val == "Anus") { x = _anusOffsetXJSON?.val ?? 0f; y = _anusOffsetYJSON?.val ?? 0f; z = _anusOffsetZJSON?.val ?? 0f; }
                else if (_targetJSON.val == "Mouth") { x = _mouthOffsetXJSON?.val ?? 0f; y = _mouthOffsetYJSON?.val ?? 0f; z = _mouthOffsetZJSON?.val ?? 0f; }
                else if (_targetJSON.val == "Vagina") { x = _vaginaOffsetXJSON?.val ?? 0f; y = _vaginaOffsetYJSON?.val ?? 0f; z = _vaginaOffsetZJSON?.val ?? 0f; }
                return (OffsetDirX * x + OffsetDirY * y + OffsetDirZ * z) / 10;
            }
        }

        public JSONStorableStringChooser.SetStringCallback OnTargetAtomChange;
        public JSONStorableStringChooser.SetStringCallback OnTargetChange;

        public TargetManager(JSONStorableStringChooser.SetStringCallback OnTargetAtomChange = null, JSONStorableStringChooser.SetStringCallback OnTargetChange = null)
        {
            this.OnTargetAtomChange = OnTargetAtomChange ?? (_ => { });
            this.OnTargetChange = OnTargetChange ?? (_ => { });
        }

        private void _onTargetAtomChange(string atomUID)
        {
            if (__DEBUG__) SuperController.LogMessage($"Loading target Atom {atomUID}");

            if (atomUID == null || atomUID == string.Empty)
            {
                _targetAtom = null;
                _target1 = null;
                _target2 = null;
                return;
            }

            _targetAtom = SuperController.singleton.GetAtomByUid(atomUID);
            if (__DEBUG__) SuperController.LogMessage($"Loaded target atom {atomUID} ({_targetAtom})");

            OnTargetAtomChange(atomUID);

            _onTargetChange(_targetJSON.val);
        }

        private void _onTargetChange(string targetName)
        {
            if (__DEBUG__) SuperController.LogMessage($"Loading target {targetName}");

            if (_targetAtom != null && _targetNames.ContainsKey(targetName))
            {
                    try
                    {
                        _target1 = _targetAtom.rigidbodies.First(rb => rb.name == _targetNames[targetName][0]);
                        _target2 = _targetAtom.rigidbodies.First(rb => rb.name == _targetNames[targetName][1]);
                        OnTargetChange(targetName);
                        return;
                    }
                catch (Exception e)
                {
                    SuperController.LogError($"Failed to load target rigidbodies: {e}");
                }
            }

            _target1 = null;
            _target2 = null;
        }

        private void _createVisualization()
        {
            if (_targetPoint1Viz == null)
            {
                _targetPoint1Viz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _targetPoint1Viz.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
                GameObject.Destroy(_targetPoint1Viz.GetComponent<Collider>());
                var renderer1 = _targetPoint1Viz.GetComponent<Renderer>();
                renderer1.material = new Material(Shader.Find("Battlehub/RTGizmos/Handles"));
                renderer1.material.color = new Color(0f, 1f, 0f, 0.6f);
                renderer1.material.renderQueue = 3001;
                renderer1.material.SetFloat("_Offset", 1f);
                renderer1.material.SetFloat("_MinAlpha", 1f);
            }

            if (_targetPoint2Viz == null)
            {
                _targetPoint2Viz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _targetPoint2Viz.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
                GameObject.Destroy(_targetPoint2Viz.GetComponent<Collider>());
                var renderer2 = _targetPoint2Viz.GetComponent<Renderer>();
                renderer2.material = new Material(Shader.Find("Battlehub/RTGizmos/Handles"));
                renderer2.material.color = new Color(1f, 0f, 0f, 0.6f);
                renderer2.material.renderQueue = 3001;
                renderer2.material.SetFloat("_Offset", 1f);
                renderer2.material.SetFloat("_MinAlpha", 1f);
            }
        }

        private void _destroyVisualization()
            {
                if (_targetPoint1Viz != null)
                {
                    GameObject.Destroy(_targetPoint1Viz);
                    _targetPoint1Viz = null;
                }

                if (_targetPoint2Viz != null)
                {
                    GameObject.Destroy(_targetPoint2Viz);
                    _targetPoint2Viz = null;
                }
            }

            public void UpdateVisualization()
            {
                if (_showTargetPointsJSON != null && _showTargetPointsJSON.val && _target1 != null && _target2 != null)
                {
                    if (_targetPoint1Viz == null || _targetPoint2Viz == null)
                        _createVisualization();

                    _targetPoint1Viz.transform.position = TargetPos1;
                    _targetPoint2Viz.transform.position = TargetPos2;
                    _targetPoint1Viz.SetActive(true);
                    _targetPoint2Viz.SetActive(true);
                }
                else
                {
                    if (_targetPoint1Viz != null) _targetPoint1Viz.SetActive(false);
                    if (_targetPoint2Viz != null) _targetPoint2Viz.SetActive(false);
                }
            }

            public void Cleanup()
            {
                _destroyVisualization();
            }

        public void SetupUI(MVRScript script, bool targetSelect = true, bool targetAdjust = true)
        {
            if (targetSelect)
            {
                _targetAtomJSON = new JSONStorableStringChooser("TargetAtom", Scene_Utils.PersonNames.FindAll(n => n != script.containingAtom.name), string.Empty, "<size=28><color=#000000>Target Atom</color></size>");
                script.RegisterStringChooser(_targetAtomJSON);
                _targetAtomJSON.setCallbackFunction += _onTargetAtomChange;
                script.CreateScrollablePopup(_targetAtomJSON).popup.onOpenPopupHandlers += () => { _targetAtomJSON.choices = Scene_Utils.PersonNames.FindAll(n => n != script.containingAtom.name); };

                _targetJSON = new JSONStorableStringChooser("Target", _targetChoices, _targetChoices.Last(), "<size=28><color=#000000>Target</color></size>");
                script.RegisterStringChooser(_targetJSON);
                _targetJSON.setCallbackFunction += _onTargetChange;
                script.CreateScrollablePopup(_targetJSON).popup.onOpenPopupHandlers += () => { _targetJSON.choices = _targetChoices; };
            }

            if (targetAdjust)
            {
                _showTargetPointsJSON = new JSONStorableBool("<size=24>Show Target Points</size>", false)
                {
                    storeType = JSONStorableParam.StoreType.Physical
                };
                script.CreateToggle(_showTargetPointsJSON);
                script.RegisterBool(_showTargetPointsJSON);

                _anusOffsetZJSON = UI_Utils.SetupSlider(script, "<size=24><b>Anus Adj. Back-Forward</b></size>", 0f, -1f, 1f);
                _mouthOffsetZJSON = UI_Utils.SetupSlider(script, "<size=24><b>Mouth Adj. Back-Forward</b></size>", 0f, -1f, 1f);
                _vaginaOffsetZJSON = UI_Utils.SetupSlider(script, "<size=24><b>Vagina Adj. Back-Forward</b></size>", 0f, -1f, 1f);

                script.CreateSpacer(false).height = 20f;

                _anusOffsetXJSON = UI_Utils.SetupSlider(script, "<size=24>Anus Adj. Left-Right</size>", 0f, -1f, 1f);
                _anusOffsetYJSON = UI_Utils.SetupSlider(script, "<size=24>Anus Adj. Down-Up</size>", 0f, -1f, 1f);

                _mouthOffsetXJSON = UI_Utils.SetupSlider(script, "<size=24>Mouth Adj. Left-Right</size>", 0f, -1f, 1f);
                _mouthOffsetYJSON = UI_Utils.SetupSlider(script, "<size=24>Mouth Adj. Down-Up</size>", 0f, -1f, 1f);

                _vaginaOffsetXJSON = UI_Utils.SetupSlider(script, "<size=24>Vagina Adj. Left-Right</size>", 0f, -1f, 1f);
                _vaginaOffsetYJSON = UI_Utils.SetupSlider(script, "<size=24>Vagina Adj. Down-Up</size>", 0f, -1f, 1f);
            }
        }

    }

    public static class UI_Utils
    {
        public static JSONStorableFloat SetupSlider(MVRScript script, string name, float defaultValue, float minValue, float maxValue, bool constrain = true, bool rightSide = false)
        {
            JSONStorableFloat storable = new JSONStorableFloat(name, defaultValue, minValue, maxValue, constrain)
            {
                storeType = JSONStorableParam.StoreType.Full
            };
            script.CreateSlider(storable, rightSide);
            script.RegisterFloat(storable);
            return storable;
        }

        public static JSONStorableBool SetupToggle(MVRScript script, string label, bool defaultValue, bool rightSide = false)
        {
            JSONStorableBool storable = new JSONStorableBool(label, defaultValue)
            {
                storeType = JSONStorableParam.StoreType.Full
            };
            script.CreateToggle(storable, rightSide);
            script.RegisterBool(storable);
            return storable;
        }

        // Stolen from MacGruber
        public static UIDynamicButton SetupButton(MVRScript script, string label, UnityEngine.Events.UnityAction callback, bool rightSide = false)
        {
            UIDynamicButton button = script.CreateButton(label, rightSide);
            button.button.onClick.AddListener(callback);
            return button;
        }
    }

}