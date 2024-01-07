using System.Runtime.CompilerServices;
using UnityEngine;

namespace CameraTools.Utils
{
    /// <summary>
    /// Optimised access to Krakensbane and FloatingOrigin adjustments.
    /// Krakensbane corrections happen some time between the Late and BetterLateThanNever timing phases (which both occur after the FlightIntegrator phase) (I'm reasonably sure that they occur during the Late phase).
    /// If you need access to these adjustments during the Late phase, then use Krakensbane or FloatingOrigin directly to ensure order of operations.
    ///
    /// With agressive inling, this reduces access time by a factor of ~4 for frequent access per frame.
    /// Note: the access time is already quite small, but these are used frequently every frame.
    /// 
    /// From BDArmory.
    /// </summary>
    public static class CTKrakensbane
    {
        public static Vector3 FrameVelocityV3f => CTKrakensbaneSingleton.Instance.GetFrameVelocityV3f;
        public static Vector3d FrameVelocity => CTKrakensbaneSingleton.Instance.GetFrameVelocity;
        public static Vector3d FloatingOriginOffset => CTKrakensbaneSingleton.Instance.FloatingOriginOffset;
        public static Vector3d FloatingOriginOffsetNonKrakensbane => CTKrakensbaneSingleton.Instance.FloatingOriginOffsetNonKrakensbane;
        public static bool IsActive => CTKrakensbaneSingleton.Instance.IsActive;
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CTKrakensbaneSingleton : MonoBehaviour
    {
        public static CTKrakensbaneSingleton Instance;

        void Awake()
        {
            if (Instance != null) Destroy(this);
            Instance = this;
        }

        void Start()
        {
            TimingManager.FixedUpdateAdd(TimingManager.TimingStage.Earlyish, CheckActiveVessel); // Check for there being an active vessel before the Normal timing phase.
            TimingManager.FixedUpdateAdd(TimingManager.TimingStage.Late, Reset); // Reset the flags at the end of the frame, but before the BetterLateThanNever phase so we can use them in that phase.
            GameEvents.onVesselChange.Add(OnVesselSwitch);
            GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroy);
        }

        void OnDestroy()
        {
            TimingManager.FixedUpdateRemove(TimingManager.TimingStage.Earlyish, CheckActiveVessel); // Check for there being an active vessel before the Normal timing phase.
            TimingManager.FixedUpdateRemove(TimingManager.TimingStage.Late, Reset);
            GameEvents.onVesselChange.Remove(OnVesselSwitch);
            GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);
        }

        void Reset()
        {
            _frameVelocityV3fCheckedThisFrame = false;
            _frameVelocityCheckedThisFrame = false;
            _floatingOriginOffsetCheckedThisFrame = false;
            _floatingOriginOffsetNonKrakensbaneCheckedThisFrame = false;
            _isActiveCheckedThisFrame = false;
            _vesselSwitchedThisFrame = false;
            _activeVesselDied = false;
        }

        void OnVesselSwitch(Vessel v)
        {
            Reset();
            _vesselSwitchedThisFrame = true;
        }
        bool _vesselSwitchedThisFrame = false;

        void OnVesselWillDestroy(Vessel v)
        {
            if (!v.isActiveVessel) return;
            _activeVesselDied = true;
        }
        bool _activeVesselDied = false;

        void CheckActiveVessel()
        {
            _haveActiveVessel = FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.gameObject.activeInHierarchy;
        }
        bool _haveActiveVessel = false;

        public Vector3 GetFrameVelocityV3f
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_frameVelocityV3fCheckedThisFrame) return _frameVelocityV3f;
                _frameVelocityV3f = Krakensbane.GetFrameVelocityV3f();
                _frameVelocityV3fCheckedThisFrame = true;
                return _frameVelocityV3f;
            }
        }
        Vector3 _frameVelocityV3f = default;
        bool _frameVelocityV3fCheckedThisFrame = false;

        public Vector3d GetFrameVelocity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_frameVelocityCheckedThisFrame) return _frameVelocity;
                _frameVelocity = Krakensbane.GetFrameVelocity();
                _frameVelocityCheckedThisFrame = true;
                return _frameVelocity;
            }
        }
        Vector3d _frameVelocity = default;
        bool _frameVelocityCheckedThisFrame = false;

        public Vector3d FloatingOriginOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_floatingOriginOffsetCheckedThisFrame) return _floatingOriginOffset;
                _floatingOriginOffset = FloatingOrigin.Offset;
                _floatingOriginOffsetCheckedThisFrame = true;
                return _floatingOriginOffset;
            }
        }
        Vector3d _floatingOriginOffset = default;
        bool _floatingOriginOffsetCheckedThisFrame = false;

        public Vector3d FloatingOriginOffsetNonKrakensbane
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_floatingOriginOffsetNonKrakensbaneCheckedThisFrame) return _floatingOriginOffsetNonKrakensbane;
                _floatingOriginOffsetNonKrakensbane = FloatingOrigin.OffsetNonKrakensbane;
                _floatingOriginOffsetNonKrakensbaneCheckedThisFrame = true;
                return _floatingOriginOffsetNonKrakensbane;
            }
        }
        Vector3d _floatingOriginOffsetNonKrakensbane = default;
        bool _floatingOriginOffsetNonKrakensbaneCheckedThisFrame = false;

        public bool IsActive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_isActiveCheckedThisFrame) return isActive;
                // If KSP doesn't have an active vessel, it doesn't set the frame velocity to zero immediately. Similarly, the frame velocity isn't immediately set once KSP has an active vessel again.
                isActive = _haveActiveVessel && !_activeVesselDied && (!FloatingOriginOffset.IsZero() || !GetFrameVelocityV3f.IsZero() || _vesselSwitchedThisFrame);
                _isActiveCheckedThisFrame = true;
                return isActive;
            }
        }
        bool isActive = false;
        bool _isActiveCheckedThisFrame = false;
    }
}