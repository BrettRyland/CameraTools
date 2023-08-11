using KSP.UI.Screens;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System;
using UnityEngine;

using CameraTools.ModIntegration;

namespace CameraTools
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class CamTools : MonoBehaviour
	{
		#region Fields
		public static CamTools fetch;

		string Version = "unknown";
		GameObject cameraParent;
		public Vessel vessel;
		List<ModuleEngines> engines = new();
		List<ModuleCommand> cockpits = new();
		public static HashSet<VesselType> ignoreVesselTypesForAudio = new() { VesselType.Debris, VesselType.SpaceObject, VesselType.Unknown, VesselType.Flag }; // Ignore some vessel types to avoid using up all the SoundManager's channels.
		Vector3 origPosition;
		Quaternion origRotation;
		Vector3 origLocalPosition;
		Quaternion origLocalRotation;
		Transform origParent;
		float origNearClip;
		float origDistance;
		FlightCamera.Modes origMode;
		float origFov = 60;
		FlightCamera flightCamera;
		Part camTarget = null;
		Vector3 cameraUp = Vector3.up;
		public bool cameraToolActive = false;
		bool cameraParentWasStolen = false;
		bool autoEnableOverriden = false; // Override auto-enabling for various integrations, e.g., BDArmory.
		bool revertWhenInFlightMode = false; // Revert the camera on returning to flight mode (if triggered in a different mode).
		bool activateWhenInFlightMode = false; // Activate the camera on returning to flight mode (if triggered in a different mode).
		System.Random rng;
		Vessel.Situations lastVesselSituation = Vessel.Situations.FLYING;
		[CTPersistantField] public static bool DEBUG = false;
		[CTPersistantField] public static bool DEBUG2 = false;

		string message;
		bool vesselSwitched = false;
		float PositionInterpolationTypeMax = Enum.GetNames(typeof(PositionInterpolationType)).Length - 1;
		float RotationInterpolationTypeMax = Enum.GetNames(typeof(RotationInterpolationType)).Length - 1;

		Vector3 upAxis;
		Vector3 forwardAxis;
		Vector3 rightAxis;

		bool freeLook = false;
		Vector2 freeLookStartUpDistance = Vector2.zero;
		[CTPersistantField] public float freeLookThresholdSqr = 0.1f; // Mouse movement threshold for starting free look (units unknown).

		#region Input
		[CTPersistantField] public string cameraKey = "home";
		[CTPersistantField] public string revertKey = "end";
		[CTPersistantField] public string toggleMenu = "[0]";
		[CTPersistantField] public bool enableKeypad = false;
		[CTPersistantField] public string fmUpKey = "[7]";
		[CTPersistantField] public string fmDownKey = "[1]";
		[CTPersistantField] public string fmForwardKey = "[8]";
		[CTPersistantField] public string fmBackKey = "[5]";
		[CTPersistantField] public string fmLeftKey = "[4]";
		[CTPersistantField] public string fmRightKey = "[6]";
		[CTPersistantField] public string fmZoomInKey = "[9]";
		[CTPersistantField] public string fmZoomOutKey = "[3]";
		[CTPersistantField] public string fmMovementModifier = "enter";
		[CTPersistantField] public string fmModeToggleKey = "[2]";
		[CTPersistantField] public string resetRollKey = "";
		bool waitingForTarget = false;
		bool waitingForPosition = false;
		bool mouseUp = false;
		bool editingKeybindings = false;
		enum fmModeTypes { Position, Speed };
		fmModeTypes fmMode = fmModeTypes.Position;
		Vector4 fmSpeeds = Vector4.zero; // x,y,z,zoom.
		#endregion

		#region GUI
		public static bool guiEnabled = false;
		public static bool hasAddedButton = false;
		[CTPersistantField] public static bool textInput = false;
		bool updateFOV = false;
		float windowWidth = 250;
		float windowHeight = 400;
		float draggableHeight = 40;
		float leftIndent = 12;
		float entryHeight = 20;
		float contentTop = 20;
		float contentWidth;
		float keyframeEditorWindowHeight = 160f;
		[CTPersistantField] public ToolModes toolMode = ToolModes.StationaryCamera;
		[CTPersistantField] public bool randomMode = false;
		[CTPersistantField] public float randomModeDogfightChance = 75f;
		[CTPersistantField] public float randomModeIVAChance = 5f;
		[CTPersistantField] public float randomModeStationaryChance = 20f;
		[CTPersistantField] public float randomModePathingChance = 0f;
		Rect windowRect = new Rect(0, 0, 0, 0);
		bool gameUIToggle = true;
		float incrButtonWidth = 26;
		[CTPersistantField] public bool manualOffset = false;
		[CTPersistantField] public float manualOffsetForward = 500;
		[CTPersistantField] public float manualOffsetRight = 50;
		[CTPersistantField] public float manualOffsetUp = 5;
		string guiOffsetForward = "500";
		string guiOffsetRight = "50";
		string guiOffsetUp = "5";
		[CTPersistantField] public bool targetCoM = false;
		static List<Tuple<double, string>> debugMessages = new();
		public static void DebugLog(string m) => debugMessages.Add(new Tuple<double, string>(Time.time, m));
		Rect cShadowRect = new Rect(Screen.width * 3 / 5, 100, Screen.width / 3 - 50, 100);
		Rect cDebugRect = new Rect(Screen.width * 3 / 5 + 2, 100 + 2, Screen.width / 3 - 50, 100);
		GUIStyle cStyle;
		GUIStyle cShadowStyle;
		GUIStyle centerLabel;
		GUIStyle leftLabel;
		GUIStyle rightLabel;
		GUIStyle leftLabelBold;
		GUIStyle titleStyle;
		GUIStyle inputFieldStyle;
		GUIStyle watermarkStyle;
		Dictionary<string, FloatInputField> inputFields;
		List<Tuple<double, string>> debug2Messages = new();
		void Debug2Log(string m) => debug2Messages.Add(new Tuple<double, string>(Time.time, m));
		float lastSavedTime = 0;

		#endregion

		#region Revert/Reset
		bool setPresetOffset = false;
		Vector3 presetOffset = Vector3.zero;
		[CTPersistantField] bool saveRotation = false;
		bool hasSavedRotation = false;
		Quaternion savedRotation;
		bool wasActiveBeforeModeChange = false;
		Vector3 lastTargetPosition = Vector3.zero;
		bool hasTarget = false;
		bool hasDied = false;
		float diedTime = 0;
		//retaining position and rotation after vessel destruction
		GameObject deathCam;
		Vector3 deathCamVelocity;
		Vector3d floatingKrakenAdjustment = Vector3d.zero; // Position adjustment for Floating origin and Krakensbane velocity changes.
		public delegate void ResetCTools();
		public static event ResetCTools OnResetCTools;
		#endregion

		#region Recording
		//recording input for key binding
		bool isRecordingInput = false;
		bool boundThisFrame = false;
		string currentlyBinding = "";
		#endregion

		#region Audio Fields
		AudioSource[] audioSources;
		List<(int index, float dopplerLevel, AudioVelocityUpdateMode velocityUpdateMode, bool bypassEffects, bool spatialize, float spatialBlend)> originalAudioSourceSettings = new();
		HashSet<string> excludeAudioSources = new() { "MusicLogic", "windAS", "windHowlAS", "windTearAS", "sonicBoomAS" }; // Don't adjust music or atmospheric audio.
		bool hasSetDoppler = false;
		[CTPersistantField] public bool useAudioEffects = true;
		public static double speedOfSound = 340;
		#endregion

		#region Camera Shake
		Vector3 shakeOffset = Vector3.zero;
		float shakeMagnitude = 0;
		[CTPersistantField] public float shakeMultiplier = 1;
		#endregion

		#region Dogfight Camera Fields
		Vessel dogfightPrevTarget;
		public Vessel dogfightTarget;
		[CTPersistantField] public float dogfightDistance = 30f;
		[CTPersistantField] public float dogfightMaxDistance = 100;
		[CTPersistantField] public float dogfightOffsetX = 10f;
		[CTPersistantField] public float dogfightOffsetY = 4f;
		[CTPersistantField] public float dogfightMaxOffset = 50;
		[CTPersistantField] public bool dogfightInertialChaseMode = false;
		[CTPersistantField] public float dogfightLerp = 0.2f;
		[CTPersistantField] public float dogfightRoll = 0f;
		[CTPersistantField] public float dogfightInertialFactor = 0f;
		Vector3 dogfightLerpDelta = default;
		Vector3 dogfightLerpMomentum = default;
		Quaternion dogfightCameraRoll = Quaternion.identity;
		Vector3 dogfightCameraRollUp = Vector3.up;
		[CTPersistantField] public float autoZoomMarginDogfight = 20;
		[CTPersistantField] public float autoZoomMarginStationary = 20;
		[CTPersistantField] public float autoZoomMarginMax = 50f;
		public float autoZoomMargin
		{
			get
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera: return autoZoomMarginDogfight;
					case ToolModes.StationaryCamera: return autoZoomMarginStationary;
					default: return 20f;
				}
			}
			set
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						autoZoomMarginDogfight = value;
						break;
					case ToolModes.StationaryCamera:
						autoZoomMarginStationary = value;
						break;
				}
			}
		}
		List<Vessel> loadedVessels;
		bool showingVesselList = false;
		bool dogfightLastTarget = false;
		Vector3 dogfightLastTargetPosition;
		Vector3 dogfightLastTargetVelocity;
		bool dogfightVelocityChase = false;
		bool cockpitView = false;
		Vector3 mouseAimFlightTarget = default;
		Vector3 mouseAimFlightTargetLocal = default;
		#endregion

		#region Stationary Camera Fields
		[CTPersistantField] public bool autoLandingPosition = false;
		bool autoLandingCamEnabled = false;
		[CTPersistantField] public bool autoFlybyPosition = false;
		[CTPersistantField] public bool autoFOV = false;
		float manualFOV = 60;
		float currentFOV = 60;
		Vector3 manualPosition = Vector3.zero;
		Vector3 lastVesselCoM = Vector3.zero;
		[CTPersistantField] public float freeMoveSpeed = 10;
		string guiFreeMoveSpeed = "10";
		float freeMoveSpeedRaw;
		float freeMoveSpeedMinRaw;
		float freeMoveSpeedMaxRaw;
		[CTPersistantField] public float freeMoveSpeedMin = 0.1f;
		[CTPersistantField] public float freeMoveSpeedMax = 100f;
		[CTPersistantField] public float keyZoomSpeed = 1;
		string guiKeyZoomSpeed = "1";
		float zoomSpeedRaw;
		float zoomSpeedMinRaw;
		float zoomSpeedMaxRaw;
		public float zoomFactor = 1;
		[CTPersistantField] public float keyZoomSpeedMin = 0.01f;
		[CTPersistantField] public float keyZoomSpeedMax = 10f;
		[CTPersistantField] public float zoomExpDogfight = 1f;
		[CTPersistantField] public float zoomExpStationary = 1f;
		[CTPersistantField] public float zoomMax = 1000f;
		float zoomMaxExp = 8f;
		public float zoomExp
		{
			get
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera: return zoomExpDogfight;
					case ToolModes.StationaryCamera: return zoomExpStationary;
					case ToolModes.Pathing: return zoomExpPathing;
					default: return 1f;
				}
			}
			set
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						zoomExpDogfight = value;
						break;
					case ToolModes.StationaryCamera:
						zoomExpStationary = value;
						break;
					case ToolModes.Pathing:
						zoomExpPathing = value;
						break;
				}
			}
		}
		[CTPersistantField] public float zoomExpPathing = 1f;
		[CTPersistantField] public float maxRelV = 2500;
		[CTPersistantField] public bool maintainInitialVelocity = false;
		Vector3d initialVelocity = Vector3d.zero;
		Orbit initialOrbit;
		[CTPersistantField] public bool useOrbital = false;
		float signedMaxRelVSqr;
		#endregion

		#region Pathing Camera Fields
		[CTPersistantField] public int selectedPathIndex = -1;
		List<CameraPath> availablePaths;
		CameraPath currentPath
		{
			get
			{
				if (selectedPathIndex >= 0 && selectedPathIndex < availablePaths.Count)
				{
					return availablePaths[selectedPathIndex];
				}
				else
				{
					return null;
				}
			}
		}
		int currentKeyframeIndex = -1;
		float currentKeyframeTime;
		PositionInterpolationType currentKeyframePositionInterpolationType = PositionInterpolationType.CubicSpline; // Default to CubicSpline
		RotationInterpolationType currentKeyframeRotationInterpolationType = RotationInterpolationType.CubicSpline; // Default to CubicSpline
		string currKeyTimeString;
		bool showKeyframeEditor = false;
		float pathStartTime;
		public float pathingSecondarySmoothing = 0f;
		public float pathingLerpRate = 1; // Lerp rate corresponding to the secondary smoothing factor.
		public float pathingTimeScale = 1f;
		bool isPlayingPath = false;
		float pathTime
		{
			get
			{
				return GetTime() - pathStartTime;
			}
		}
		Vector2 keysScrollPos;
		public bool interpolationType = false;
		[CTPersistantField] public bool useRealTime = true;
		#endregion

		#region Mod Integration
		BDArmory bdArmory;
		BetterTimeWarp betterTimeWarp;
		TimeControl timeControl;
		#endregion
		#endregion

		void Awake()
		{
			if (fetch)
			{
				Destroy(fetch);
			}

			fetch = this;

			GetVersion();
			Load();

			rng = new System.Random();
		}

		void Start()
		{
			windowRect = new Rect(Screen.width - windowWidth - 40, 0, windowWidth, windowHeight);
			flightCamera = FlightCamera.fetch;
			if (flightCamera == null)
			{
				Debug.LogError("[CameraTools.CamTools]: Flight Camera is null! Unable to start CameraTools!");
				Destroy(this);
				return;
			}
			cameraToolActive = false;
			SaveOriginalCamera();

			AddToolbarButton();

			GameEvents.onHideUI.Add(GameUIDisable);
			GameEvents.onShowUI.Add(GameUIEnable);
			//GameEvents.onGamePause.Add (PostDeathRevert);
			GameEvents.OnVesselRecoveryRequested.Add(PostDeathRevert);
			GameEvents.onGameSceneLoadRequested.Add(PostDeathRevert);

			cameraParent = new GameObject("CameraToolsCameraParent");
			deathCam = new GameObject("CameraToolsDeathCam");

			bdArmory = BDArmory.instance;
			betterTimeWarp = BetterTimeWarp.instance;
			timeControl = TimeControl.instance;

			if (FlightGlobals.ActiveVessel != null)
			{
				cameraParent.transform.position = FlightGlobals.ActiveVessel.transform.position;
				vessel = FlightGlobals.ActiveVessel;
				deathCam.transform.position = vessel.transform.position;
				deathCam.transform.rotation = vessel.transform.rotation;
			}
			GameEvents.onVesselChange.Add(SwitchToVessel);
			GameEvents.onVesselWillDestroy.Add(CurrentVesselWillDestroy);
			GameEvents.OnCameraChange.Add(CameraModeChange);
			TimingManager.FixedUpdateAdd(TimingManager.TimingStage.BetterLateThanNever, KrakensbaneWarpCorrection); // Perform our Krakensbane corrections after KSP's floating origin/Krakensbane corrections have run.

			// Styles and rects.
			cStyle = new GUIStyle(HighLogic.Skin.label);
			cStyle.fontStyle = UnityEngine.FontStyle.Bold;
			cStyle.fontSize = 18;
			cStyle.alignment = TextAnchor.UpperLeft;
			cShadowStyle = new GUIStyle(cStyle);
			cShadowRect = new Rect(cDebugRect);
			cShadowRect.x += 2;
			cShadowRect.y += 2;
			cShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
			centerLabel = new GUIStyle();
			centerLabel.alignment = TextAnchor.UpperCenter;
			centerLabel.normal.textColor = Color.white;
			leftLabel = new GUIStyle();
			leftLabel.alignment = TextAnchor.UpperLeft;
			leftLabel.normal.textColor = Color.white;
			rightLabel = new GUIStyle(leftLabel);
			rightLabel.alignment = TextAnchor.UpperRight;
			leftLabelBold = new GUIStyle(leftLabel);
			leftLabelBold.fontStyle = FontStyle.Bold;
			titleStyle = new GUIStyle(centerLabel);
			titleStyle.fontSize = 24;
			titleStyle.alignment = TextAnchor.MiddleCenter;
			watermarkStyle = new GUIStyle(leftLabel);
			watermarkStyle.normal.textColor = XKCDColors.LightBlueGrey;
			watermarkStyle.fontSize = 12;
			contentWidth = (windowWidth) - (2 * leftIndent);

			inputFields = new Dictionary<string, FloatInputField> {
				{"autoZoomMargin", gameObject.AddComponent<FloatInputField>().Initialise(0, autoZoomMargin, 0f, autoZoomMarginMax, 4)},
				{"zoomFactor", gameObject.AddComponent<FloatInputField>().Initialise(0, zoomFactor, 1f, zoomMax, 4)},
				{"shakeMultiplier", gameObject.AddComponent<FloatInputField>().Initialise(0, shakeMultiplier, 0f, 10f, 1)},
				{"dogfightDistance", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightDistance, 1f, dogfightMaxDistance, 3)},
				{"dogfightOffsetX", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset, 3)},
				{"dogfightOffsetY", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset, 3)},
				{"dogfightLerp", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightLerp, 0.01f, 0.5f, 3)},
				{"dogfightRoll", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightRoll, 0f, 1f, 3)},
				{"dogfightInertialFactor", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightInertialFactor, 0f, 0.1f, 3)},
				{"pathingSecondarySmoothing", gameObject.AddComponent<FloatInputField>().Initialise(0, pathingSecondarySmoothing, 0f, 1f, 4)},
				{"pathingTimeScale", gameObject.AddComponent<FloatInputField>().Initialise(0, pathingTimeScale, 0.05f, 4f, 4)},
				{"randomModeDogfightChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeDogfightChance, 0f, 100f, 3)},
				{"randomModeIVAChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeIVAChance, 0f, 100f, 3)},
				{"randomModeStationaryChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeStationaryChance, 0f, 100f, 3)},
				{"randomModePathingChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModePathingChance, 0f, 100f, 3)},
				{"freeMoveSpeed", gameObject.AddComponent<FloatInputField>().Initialise(0, freeMoveSpeed, freeMoveSpeedMin, freeMoveSpeedMax, 4)},
				{"keyZoomSpeed", gameObject.AddComponent<FloatInputField>().Initialise(0, keyZoomSpeed, keyZoomSpeedMin, keyZoomSpeedMax, 4)},
				{"maxRelV", gameObject.AddComponent<FloatInputField>().Initialise(0, maxRelV, float.MinValue, float.MaxValue, 6)},
				{"freeLookThresholdSqr", gameObject.AddComponent<FloatInputField>().Initialise(0, freeLookThresholdSqr, 0, 1, 4)},
			};
		}

		void OnDestroy()
		{
			GameEvents.onHideUI.Remove(GameUIDisable);
			GameEvents.onShowUI.Remove(GameUIEnable);
			GameEvents.OnVesselRecoveryRequested.Remove(PostDeathRevert);
			GameEvents.onGameSceneLoadRequested.Remove(PostDeathRevert);
			GameEvents.onVesselChange.Remove(SwitchToVessel);
			GameEvents.onVesselWillDestroy.Remove(CurrentVesselWillDestroy);
			GameEvents.OnCameraChange.Remove(CameraModeChange);
			TimingManager.FixedUpdateRemove(TimingManager.TimingStage.BetterLateThanNever, KrakensbaneWarpCorrection);
			Save();
		}

		void CameraModeChange(CameraManager.CameraMode mode)
		{
			if (mode != CameraManager.CameraMode.Flight && CameraManager.Instance.previousCameraMode == CameraManager.CameraMode.Flight)
			{
				wasActiveBeforeModeChange = cameraToolActive;
				cameraToolActive = false;
				if (wasActiveBeforeModeChange) Debug.Log($"[CameraTools]: Deactivating due to switching to {mode} camera mode.");
			}
			else if (mode == CameraManager.CameraMode.Flight)
			{
				if ((wasActiveBeforeModeChange || activateWhenInFlightMode) && !autoEnableOverriden && !bdArmory.autoEnableOverride)
				{
					Debug.Log($"[CameraTools]: Camera mode changed to {mode}, reactivating {toolMode}.");
					cockpitView = false; // Don't go back into cockpit view in case it was triggered by the user.
					cameraToolActive = true;
					RevertCamera();
					flightCamera.transform.position = deathCam.transform.position;
					flightCamera.transform.rotation = deathCam.transform.rotation;
					if (!revertWhenInFlightMode)
					{
						if (CameraManager.Instance.previousCameraMode == CameraManager.CameraMode.Map) StartCoroutine(DelayActivation(1, false)); // Something messes with the camera position on the first frame after switching.
						else cameraActivate();
					}
				}
				else if (revertWhenInFlightMode)
				{
					Debug.Log($"[CameraTools]: Camera mode changed to {mode}, applying delayed revert.");
					cockpitView = false; // Don't go back into cockpit view in case it was triggered by the user.
					cameraToolActive = true;
					RevertCamera();
				}
			}
		}

		IEnumerator DelayActivation(int frames, bool fixedUpdate = false)
		{
			if (fixedUpdate)
			{
				var wait = new WaitForFixedUpdate();
				for (int i = 0; i < frames; ++i) yield return wait;
			}
			else
			{
				var wait = new WaitForEndOfFrame();
				for (int i = 0; i < frames; ++i) yield return wait;
			}
			cameraActivate();
		}

		bool wasUsingObtVel = false;
		bool wasInHighWarp = false;
		bool wasAbove1e5 = false;
		float previousWarpFactor = 1;
		// float δt = 0f;
		void KrakensbaneWarpCorrection()
		{
			// Compensate for floating origin and Krakensbane velocity shifts under warp.
			// Notes:
			//   This runs in the BetterLateThanNever timing phase after the flight integrator and floating origin/Krakensbane corrections have been applied.
			//   There is a small direction change in dogfight mode when leaving atmospheres due to switching between surface velocity and orbital velocity.
			//   At an altitude of 100km above a body (except on Jool, where it's in atmosphere), the Krakensbane velocity changes from the active vessel's surface velocity to its orbital velocity. I suspect this corresponds to KrakensbaneInstance.extraAltOffsetForVel, but Krakensbane doesn't provide an instance property.
			//   The stationary camera has a visible jitter when the target is a large distance away. I believe this is due to half-precision rounding of the graphics and there is a slow drift, which I believe is due to single-precision rounding of the camera position.
			//   Dogfight camera is now working perfectly.
			// FIXME Stationary camera - maintain velocity
			// - When changing low warp at >100km there is a slow drift, like the orbit calculation position is slightly wrong. I.e., starting at a given low warp and staying there is fine, but once changed the drift begins.
			// - Below 100km, there is a small unsteady drift when not in high warp (exagerated by low warp) and once present continues after entering high warp.
			// - Switching in and out of map mode isn't showing the vessel on returning.
			if (GameIsPaused) return;
			if (vessel == null) return;
			if (cameraToolActive)
			{
				var inHighWarp = (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > 1);
				var inLowWarp = !inHighWarp && TimeWarp.CurrentRate != 1;
				var inOrbit = vessel.InOrbit();
				var useObtVel = inHighWarp || (inOrbit && vessel.altitude > 1e5); // Unknown if this should be >= or not. Unlikely to be an issue though.
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						{
							floatingKrakenAdjustment = -FloatingOrigin.Offset;
							if (!inOrbit)
								floatingKrakenAdjustment += (vessel.srf_velocity - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime;
							else if (!inHighWarp && useObtVel != wasUsingObtVel) // Only needed when crossing the boundary.
								floatingKrakenAdjustment += ((useObtVel ? vessel.obt_velocity : vessel.srf_velocity) - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime;
							cameraParent.transform.position += floatingKrakenAdjustment;
							if (DEBUG2 && !GameIsPaused)
							{
								var cmb = FlightGlobals.currentMainBody;
								Debug2Log("situation: " + vessel.situation);
								Debug2Log("warp mode: " + TimeWarp.WarpMode + ", warp factor: " + TimeWarp.CurrentRate);
								Debug2Log($"radius: {cmb.Radius}, radiusAtmoFactor: {cmb.radiusAtmoFactor}, atmo: {cmb.atmosphere}, atmoDepth: {cmb.atmosphereDepth}");
								Debug2Log("speed: " + vessel.Speed().ToString("G3") + ", vel: " + vessel.Velocity().ToString("G3"));
								Debug2Log("offset from vessel CoM: " + (flightCamera.transform.position - vessel.CoM).ToString("G3"));
								Debug2Log("camParentPos - flightCamPos: " + (cameraParent.transform.position - flightCamera.transform.position).ToString("G3"));
								Debug2Log($"inOrbit: {inOrbit}, inHighWarp: {inHighWarp}, useObtVel: {useObtVel}");
								Debug2Log($"altitude: {vessel.altitude}");
								Debug2Log("vessel velocity: " + vessel.Velocity().ToString("G3") + ", Kraken velocity: " + Krakensbane.GetFrameVelocity().ToString("G3"));
								Debug2Log("(vv - kv): " + (vessel.Velocity() - Krakensbane.GetFrameVelocity()).ToString("G3") + ", ΔKv: " + Krakensbane.GetLastCorrection().ToString("G3"));
								Debug2Log("(vv - kv)*Δt: " + ((vessel.Velocity() - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime).ToString("G3"));
								Debug2Log("(sv - kv)*Δt: " + ((vessel.srf_velocity - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime).ToString("G3"));
								Debug2Log("floating origin offset: " + FloatingOrigin.Offset.ToString("G3") + ", offsetNonKB: " + FloatingOrigin.OffsetNonKrakensbane.ToString("G3"));
								Debug2Log($"ΔKv*Δt: {(Krakensbane.GetLastCorrection() * TimeWarp.fixedDeltaTime).ToString("G3")}");
								Debug2Log($"onKb - kv*Δt: {(FloatingOrigin.OffsetNonKrakensbane - Krakensbane.GetFrameVelocity() * TimeWarp.fixedDeltaTime).ToString("G3")}");
								Debug2Log("floatingKrakenAdjustment: " + floatingKrakenAdjustment.ToString("G3"));
							}
							break;
						}
					case ToolModes.StationaryCamera:
						{
							if (maintainInitialVelocity && !randomMode && !autoLandingCamEnabled) // Don't maintain velocity when using random mode or auto landing camera.
							{
								if (useOrbital && initialOrbit != null)
								{
									// Situations: {high warp, low warp, normal} x {inOrbit && >100km, inOrbit && <100km, !inOrbit}
									lastVesselCoM += initialOrbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime() + ((inOrbit && !inHighWarp) ? -0.5f : 0) * TimeWarp.fixedDeltaTime).xzy * TimeWarp.fixedDeltaTime;
								}
								else
								{ lastVesselCoM += initialVelocity * TimeWarp.fixedDeltaTime; }

								if (inHighWarp) // This exactly corrects for motion when >100km and is correct up to floating precision for <100km.
								{
									floatingKrakenAdjustment = -(useObtVel ? vessel.obt_velocity : vessel.srf_velocity) * TimeWarp.fixedDeltaTime;
									lastVesselCoM += floatingKrakenAdjustment;
									lastCamParentPosition += floatingKrakenAdjustment;
								}
								else if (wasInHighWarp)
								{
									if (vessel.altitude > 1e5) // This is correct for >100km.
									{
										floatingKrakenAdjustment = -floatingKrakenAdjustment - (useObtVel ? vessel.obt_velocity : vessel.srf_velocity) * TimeWarp.fixedDeltaTime; // Correction reverts the previous correction and adjusts for current velocity.
										lastVesselCoM += floatingKrakenAdjustment;
										lastCamParentPosition += floatingKrakenAdjustment;
									}
									else
									{
										floatingKrakenAdjustment = (previousWarpFactor * vessel.srf_velocity - vessel.obt_velocity) * TimeWarp.fixedDeltaTime;
										lastVesselCoM += floatingKrakenAdjustment;
										lastCamParentPosition += floatingKrakenAdjustment;
									}
								}
								else
								{
									if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
									{
										if (vessel.altitude > 1e5)
											floatingKrakenAdjustment = -FloatingOrigin.OffsetNonKrakensbane;
										else if (wasAbove1e5)
											floatingKrakenAdjustment = -FloatingOrigin.OffsetNonKrakensbane + (vessel.srf_velocity - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime;
										else if (inOrbit)
											floatingKrakenAdjustment = -vessel.obt_velocity * TimeWarp.fixedDeltaTime - FloatingOrigin.Offset;
										else
											floatingKrakenAdjustment = -vessel.srf_velocity * TimeWarp.fixedDeltaTime - FloatingOrigin.Offset;
										lastVesselCoM += floatingKrakenAdjustment;
										lastCamParentPosition += floatingKrakenAdjustment;
									}
								}
							}
							else
							{
								if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
								{
									floatingKrakenAdjustment = -FloatingOrigin.OffsetNonKrakensbane;
									lastVesselCoM += floatingKrakenAdjustment;
									lastCamParentPosition += floatingKrakenAdjustment;
								}
							}
							break;
						}
				}
				if (DEBUG && vessel.situation != lastVesselSituation)
				{
					DebugLog($"Vessel Situation changed from {lastVesselSituation} to {vessel.situation}");
					lastVesselSituation = vessel.situation;
				}
				if (DEBUG && TimeWarp.WarpMode == TimeWarp.Modes.LOW && FloatingOrigin.Offset.sqrMagnitude > 10)
				{
					DebugLog("Floating origin offset: " + FloatingOrigin.Offset.ToString("0.0") + ", Krakensbane velocity correction: " + Krakensbane.GetLastCorrection().ToString("0.0"));
				}
				// #if DEBUG
				// 				if (DEBUG && (flightCamera.transform.position - (vessel.CoM - lastVesselCoM) - lastCameraPosition).sqrMagnitude > 1)
				// 				{
				// 					DebugLog("situation: " + vessel.situation + " inOrbit " + inOrbit + " useObtVel " + useObtVel);
				// 					DebugLog("warp mode: " + TimeWarp.WarpMode + ", fixedDeltaTime: " + TimeWarp.fixedDeltaTime + ", was: " + previousWarpFactor);
				// 					DebugLog($"high warp: {inHighWarp} | {wasInHighWarp}");
				// 					DebugLog($">100km: {vessel.altitude > 1e5} | {wasAbove1e5} ({vessel.altitude.ToString("G8")})");
				// 					DebugLog("floating origin offset: " + FloatingOrigin.Offset.ToString("G6"));
				// 					DebugLog("KB frame vel: " + Krakensbane.GetFrameVelocity().ToString("G6"));
				// 					DebugLog("offsetNonKB: " + FloatingOrigin.OffsetNonKrakensbane.ToString("G6"));
				// 					DebugLog("vv*Δt: " + (vessel.obt_velocity * TimeWarp.fixedDeltaTime).ToString("G6"));
				// 					DebugLog("sv*Δt: " + (vessel.srf_velocity * TimeWarp.fixedDeltaTime).ToString("G6"));
				// 					DebugLog("kv*Δt: " + (Krakensbane.GetFrameVelocity() * TimeWarp.fixedDeltaTime).ToString("G6"));
				// 					DebugLog("ΔKv: " + Krakensbane.GetLastCorrection().ToString("G6"));
				// 					DebugLog("(sv-kv)*Δt" + ((vessel.srf_velocity - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime).ToString("G6"));
				// 					DebugLog("floatingKrakenAdjustment: " + floatingKrakenAdjustment.ToString("G6"));
				// 					DebugLog("Camera pos: " + (flightCamera.transform.position - (vessel.CoM - lastVesselCoM)).ToString("G6"));
				// 					DebugLog("ΔCamera: " + (flightCamera.transform.position - (vessel.CoM - lastVesselCoM) - lastCameraPosition).ToString("G6"));

				// 				}
				// #endif
				wasUsingObtVel = useObtVel;
				wasAbove1e5 = vessel.altitude > 1e5;
				wasInHighWarp = inHighWarp;
				previousWarpFactor = TimeWarp.CurrentRate;
			}
		}

		void Update()
		{
			if (!isRecordingInput && !boundThisFrame)
			{
				if (Input.GetKeyDown(toggleMenu))
				{
					ToggleGui();
				}

				if (Input.GetKeyDown(revertKey))
				{
					autoEnableOverriden = true;
					RevertCamera();
				}
				else if (Input.GetKeyDown(cameraKey))
				{
					autoEnableOverriden = false;
					if (!cameraToolActive && randomMode)
					{
						ChooseRandomMode();
					}
					cameraActivate();
				}

				if (Input.GetKeyDown(fmModeToggleKey))
				{
					if (!textInput)
					{
						// Cycle through the free move modes.
						var fmModes = (fmModeTypes[])Enum.GetValues(typeof(fmModeTypes));
						var fmModeIndex = (fmModes.IndexOf(fmMode) + 1) % fmModes.Length;
						fmMode = fmModes[fmModeIndex];
						fmSpeeds = Vector4.zero;
						if (DEBUG) DebugLog($"Switching to free move mode {fmMode}");
					}
					else
					{
						if (DEBUG) DebugLog($"Unable to switch to free move mode {fmModeTypes.Speed} while in numeric input mode.");
					}
				}
			}

			if (MapView.MapIsEnabled) return; // Don't do anything else in map mode.

			if (Input.GetMouseButtonUp(0))
			{
				mouseUp = true;
			}

			//get target transform from mouseClick
			if (waitingForTarget && mouseUp && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Part tgt = GetPartFromMouse();
				if (tgt != null)
				{
					camTarget = tgt;
					hasTarget = true;
				}
				else
				{
					Vector3 pos = GetPosFromMouse();
					if (pos != Vector3.zero)
					{
						lastTargetPosition = pos;
						hasTarget = true;
					}
				}

				waitingForTarget = false;
			}

			//set position from mouseClick
			if (waitingForPosition && mouseUp && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Vector3 pos = GetPosFromMouse();
				if (pos != Vector3.zero)// && isStationaryCamera)
				{
					presetOffset = pos;
					setPresetOffset = true;
				}
				else Debug.Log("[CameraTools]: No pos from mouse click");

				waitingForPosition = false;
			}

			if (cameraToolActive)
			{
				switch (toolMode)
				{
					case ToolModes.StationaryCamera:
						UpdateStationaryCamera();
						break;
					case ToolModes.Pathing:
						if (useRealTime)
							UpdatePathingCam();
						break;
					case ToolModes.DogfightCamera: // Dogfight mode is mostly handled in FixedUpdate due to relying on interpolation of positions updated in the physics update.
						break;
					default:
						break;
				}
			}
		}

		void FixedUpdate()
		{
			// Note: we have to perform several of the camera adjustments during FixedUpdate to avoid jitter in the Lerps in the camera position and rotation due to inconsistent numbers of physics updates per frame.
			if (!FlightGlobals.ready || GameIsPaused) return;
			if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight) return;
			if (MapView.MapIsEnabled) return; // Don't do anything in map mode.
			if (DEBUG2 && !GameIsPaused) debug2Messages.Clear();

			if (cameraToolActive)
			{
				if ((!hasDied && flightCamera.transform.parent != cameraParent.transform) || (hasDied && flightCamera.transform.parent != deathCam.transform))
				{
					if (flightCamera.transform.parent == origParent)
					{
						message = "Camera parent got reverted to the main camera parent! Stealing it back!";
						Debug.Log("[CameraTools]: " + message);
						if (DEBUG) DebugLog(message);
						flightCamera.transform.parent = hasDied ? deathCam.transform : cameraParent.transform; // KSP reverted the camera parent (e.g., when spawning a new missile or kerbal), steal it back.
					}
					else
					{
						message = "Someone has stolen the camera parent! Abort!";
						Debug.Log("[CameraTools]: " + message);
						if (DEBUG) DebugLog(message);
						cameraToolActive = false;
						cameraParentWasStolen = true;
						RevertCamera();
					}
				}
			}

			if (hasDied && cameraToolActive) return; // Do nothing until we have an active vessel.

			if (vessel == null || vessel != FlightGlobals.ActiveVessel)
			{
				vessel = FlightGlobals.ActiveVessel;
			}

			if (!autoEnableOverriden && bdArmory.autoEnableForBDA && (toolMode != ToolModes.Pathing || (selectedPathIndex >= 0 && currentPath.keyframeCount > 0)))
			{
				bdArmory.AutoEnableForBDA();
			}
			if (cameraToolActive)
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						UpdateDogfightCamera();
						if (dogfightTarget && dogfightTarget.isActiveVessel)
						{
							dogfightTarget = null;
						}
						if (fmMode == fmModeTypes.Speed)
						{
							dogfightOffsetY = Mathf.Clamp(dogfightOffsetY + fmSpeeds.y, -dogfightMaxOffset, dogfightMaxOffset);
							if (Mathf.Abs(dogfightOffsetY) >= dogfightMaxOffset) fmSpeeds.y = 0;
							dogfightOffsetX = Mathf.Clamp(dogfightOffsetX + fmSpeeds.x, -dogfightMaxOffset, dogfightMaxOffset);
							if (Mathf.Abs(dogfightOffsetX) >= dogfightMaxOffset) fmSpeeds.x = 0;
							dogfightDistance = Mathf.Clamp(dogfightDistance + fmSpeeds.z, 1f, dogfightMaxDistance);
							if (dogfightDistance <= 1f || dogfightDistance >= dogfightMaxDistance) fmSpeeds.z = 0;
							if (!autoFOV)
							{
								zoomExp = Mathf.Clamp(zoomExp + fmSpeeds.w, 1, zoomMaxExp);
								if (zoomExp <= 1 || zoomExp >= zoomMaxExp) fmSpeeds.w = 0;
							}
							else
							{
								autoZoomMargin = Mathf.Clamp(autoZoomMargin + 10 * fmSpeeds.w, 0, autoZoomMarginMax);
								if (autoZoomMargin <= 0 || autoZoomMargin >= autoZoomMarginMax) fmSpeeds.w = 0;
							}
						}
						break;
					case ToolModes.StationaryCamera:
						// Updating of the stationary camera is handled in Update.
						if (fmMode == fmModeTypes.Speed)
						{
							manualPosition += upAxis * fmSpeeds.y + forwardAxis * fmSpeeds.z + rightAxis * fmSpeeds.x;
							if (!autoFOV)
							{
								zoomExp = Mathf.Clamp(zoomExp + fmSpeeds.w, 1f, zoomMaxExp);
								if (zoomExp <= 1f || zoomExp >= zoomMaxExp) fmSpeeds.w = 0;
							}
							else
							{
								autoZoomMargin = Mathf.Clamp(autoZoomMargin + 10 * fmSpeeds.w, 0f, autoZoomMarginMax);
								if (autoZoomMargin <= 0f || autoZoomMargin >= autoZoomMarginMax) fmSpeeds.w = 0;
							}
						}
						break;
					case ToolModes.Pathing:
						if (!useRealTime) UpdatePathingCam();
						if (fmMode == fmModeTypes.Speed)
						{
							flightCamera.transform.position += upAxis * fmSpeeds.y + forwardAxis * fmSpeeds.z + rightAxis * fmSpeeds.x; // Note: for vessel relative movement, the modifier key will need to be held.
							zoomExp = Mathf.Clamp(zoomExp + fmSpeeds.w, 1f, zoomMaxExp);
							if (zoomExp <= 1f || zoomExp >= zoomMaxExp) fmSpeeds.w = 0;
						}
						break;
					default:
						break;
				}
			}
			else
			{
				if (!autoFOV)
				{
					zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				}
			}
		}

		void LateUpdate()
		{
			boundThisFrame = false;
			UpdateCameraShake(); // Update camera shake each frame so that it dies down.
			if (hasDied && cameraToolActive)
			{
				deathCam.transform.position += deathCamVelocity * TimeWarp.deltaTime;
				deathCamVelocity *= 0.95f;
				if (flightCamera.transform.parent != deathCam.transform) // Something else keeps trying to steal the camera after the vessel has died, so we need to keep overriding it.
				{
					SetDeathCam();
				}
			}
			else if (!vesselSwitched)
			{
				switch (CameraManager.Instance.currentCameraMode)
				{
					case CameraManager.CameraMode.IVA:
						var IVACamera = CameraManager.GetCurrentCamera();
						deathCam.transform.position = IVACamera.transform.position;
						deathCam.transform.rotation = IVACamera.transform.rotation;
						break;
					case CameraManager.CameraMode.Flight:
						deathCam.transform.position = flightCamera.transform.position;
						deathCam.transform.rotation = flightCamera.transform.rotation;
						break;
				}
			}
			if (cameraToolActive && vesselSwitched) // We perform this here instead of waiting for the next frame to avoid a flicker of the camera being switched during a FixedUpdate.
			{
				vesselSwitched = false;
				cameraActivate();
			}
		}

		public void cameraActivate()
		{
			if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight)
			{
				activateWhenInFlightMode = true;
				revertWhenInFlightMode = false;
				return; // Don't activate if we're not in Flight mode.
			}
			activateWhenInFlightMode = false;
			if (DEBUG) { Debug.Log("[CameraTools]: Activating camera."); DebugLog("Activating camera"); }
			if (!cameraToolActive)
			{
				timeControl.SetTimeControlCameraZoomFix(false);
				betterTimeWarp.SetBetterTimeWarpScaleCameraSpeed(false);
				freeLook = false;
				freeLookStartUpDistance = Vector2.zero;
			}
			if (!cameraToolActive && !cameraParentWasStolen)
			{
				SaveOriginalCamera();
			}
			deathCam.transform.position = flightCamera.transform.position;
			deathCam.transform.rotation = flightCamera.transform.rotation;
			if (toolMode == ToolModes.StationaryCamera)
			{
				StartStationaryCamera();
			}
			else if (toolMode == ToolModes.DogfightCamera)
			{
				StartDogfightCamera();
			}
			else if (toolMode == ToolModes.Pathing)
			{
				StartPathingCam();
				PlayPathingCam();
			}
		}

		#region Dogfight Camera
		void StartDogfightCamera()
		{
			toolMode = ToolModes.DogfightCamera;
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.Log("[CameraTools]: No active vessel.");
				return;
			}
			if (DEBUG) { Debug.Log("[CameraTools]: Starting dogfight camera."); DebugLog("Starting dogfight camera"); }

			if (MouseAimFlight.IsMouseAimActive())
			{
				dogfightTarget = null;
				dogfightLastTarget = true;
				dogfightVelocityChase = false;
			}
			else if (bdArmory.hasBDA && bdArmory.useCentroid && bdArmory.bdWMVessels.Count > 1)
			{
				dogfightLastTarget = true;
				dogfightVelocityChase = false;
			}
			else if (dogfightTarget)
			{
				dogfightVelocityChase = false;
			}
			else
			{
				if (false && randomMode && rng.Next(3) == 0)
				{
					dogfightVelocityChase = false; // sometimes throw in a non chase angle
				}
				else
				{
					dogfightVelocityChase = true;
				}
			}

			dogfightPrevTarget = dogfightTarget;

			hasDied = false;
			vessel = FlightGlobals.ActiveVessel;
			cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.CoM).normalized;

			if (flightCamera.transform.parent != cameraParent.transform)
			{
				SetCameraParent(deathCam.transform, true); // First update the cameraParent to the last deathCam configuration
			}

			cameraToolActive = true;

			ResetDoppler();
			if (OnResetCTools != null)
			{ OnResetCTools(); }
			SetDoppler(false);
			AddAtmoAudioControllers(false);
		}

		void UpdateDogfightCamera()
		{
			if (!vessel || (!dogfightTarget && !dogfightLastTarget && !dogfightVelocityChase))
			{
				if (DEBUG) { Debug.Log("[CameraTools]: Reverting during UpdateDogfightCamera"); }
				RevertCamera();
				return;
			}

			if (cockpitView)
			{
				if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) // Already enabled, do nothing.
				{ return; }
				// Check that there's still a kerbal to switch to.
				if (cockpits.Any(cockpit => cockpit != null && cockpit.part != null && cockpit.part.protoModuleCrew.Count > 0))
				{
					try
					{
						CameraManager.Instance.SetCameraIVA(); // Try to enable IVA camera.
					}
					catch (Exception e)
					{
						Debug.LogWarning($"[CameraTools.CamTools]: Exception thrown trying to set IVA camera mode, aborting. {e.Message}");
						cockpitView = false;
					}
				}
				else
					cockpitView = false;
				if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) // Success!
				{ return; }
			}

			var cameraTransform = flightCamera.transform;
			if (MouseAimFlight.IsMouseAimActive())
			{ // We need to set these each time as MouseAimFlight can be enabled/disabled while CameraTools is active.
				dogfightTarget = null;
				dogfightLastTarget = true;
				dogfightVelocityChase = false;
				dogfightLastTargetVelocity = Vector3.zero;
				mouseAimFlightTarget = MouseAimFlight.GetMouseAimTarget();
				mouseAimFlightTargetLocal = cameraTransform.InverseTransformDirection(mouseAimFlightTarget);
				dogfightLastTargetPosition = (mouseAimFlightTarget.normalized + vessel.srf_vel_direction) * 5000f + vessel.CoM;
			}
			else if (bdArmory.hasBDA && bdArmory.useCentroid && bdArmory.bdWMVessels.Count > 1)
			{
				dogfightLastTarget = true;
				dogfightLastTargetVelocity = Vector3.zero;
				dogfightLastTargetPosition = bdArmory.GetCentroid();
				if (DEBUG2 && !GameIsPaused) Debug2Log($"Centroid: {dogfightLastTargetPosition:G3}");
			}
			else if (dogfightTarget)
			{
				if (loadedVessels == null) UpdateLoadedVessels();
				dogfightLastTarget = true;
				dogfightLastTargetPosition = dogfightTarget.CoM;
				dogfightLastTargetVelocity = dogfightTarget.Velocity();
			}
			else if (dogfightLastTarget)
			{
				if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
				{ dogfightLastTargetPosition -= FloatingOrigin.OffsetNonKrakensbane; }
				dogfightLastTargetPosition += dogfightLastTargetVelocity * TimeWarp.fixedDeltaTime;
			}
			cameraParent.transform.position = vessel.CoM;

			if (dogfightVelocityChase)
			{
				var lastDogfightLastTargetPosition = dogfightLastTargetPosition;
				if (vessel.Speed() > 1)
				{
					dogfightLastTargetPosition = vessel.CoM + vessel.Velocity().normalized * 5000f;
				}
				else
				{
					dogfightLastTargetPosition = vessel.CoM + vessel.ReferenceTransform.up * 5000f;
				}
				if (vessel.Splashed && vessel.Speed() < 10) // Don't bob around lots if the vessel is in water.
				{
					dogfightLastTargetPosition = Vector3.Lerp(lastDogfightLastTargetPosition, Vector3.ProjectOnPlane(dogfightLastTargetPosition, cameraUp), (float)vessel.Speed() * 0.01f); // Slow lerp to a horizontal position.
				}
			}

			//roll
			if (dogfightRoll > 0 && !vessel.LandedOrSplashed && !vessel.isEVA && !bdArmory.isBDMissile)
			{
				var vesselRollTarget = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(cameraUp, -vessel.ReferenceTransform.forward), dogfightRoll * Vector3.Angle(cameraUp, -vessel.ReferenceTransform.forward));
				dogfightCameraRoll = Quaternion.Lerp(dogfightCameraRoll, vesselRollTarget, dogfightLerp);
				dogfightCameraRollUp = dogfightCameraRoll * cameraUp;
			}
			else
			{
				dogfightCameraRollUp = cameraUp;
			}
			Vector3 lagDirection = (dogfightLastTargetPosition - vessel.CoM).normalized;
			Vector3 offsetDirectionY = dogfightInertialChaseMode ? Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(cameraUp, -vessel.ReferenceTransform.forward), Vector3.Angle(cameraUp, -vessel.ReferenceTransform.forward)) * cameraUp : dogfightCameraRollUp;
			Vector3 offsetDirectionX = Vector3.Cross(offsetDirectionY, lagDirection).normalized;
			Vector3 camPos = vessel.CoM + (-lagDirection * dogfightDistance);
			if (!vessel.isEVA) camPos += (dogfightOffsetX * offsetDirectionX) + (dogfightOffsetY * offsetDirectionY);

			Vector3 localCamPos = cameraParent.transform.InverseTransformPoint(camPos);
			if (dogfightInertialChaseMode)
			{
				dogfightLerpMomentum /= dogfightLerpMomentum.sqrMagnitude * 2f / dogfightDistance + 1f;
				dogfightLerpMomentum += dogfightLerpDelta * dogfightInertialFactor;
				dogfightLerpDelta = -cameraTransform.localPosition;
			}
			cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, localCamPos, dogfightLerp);
			if (dogfightInertialChaseMode)
			{
				cameraTransform.localPosition += dogfightLerpMomentum;
				dogfightLerpDelta += cameraTransform.localPosition;
				if (dogfightLerpDelta.sqrMagnitude > dogfightDistance * dogfightDistance) dogfightLerpDelta *= dogfightDistance / dogfightLerpDelta.magnitude;
			}
			if (DEBUG2 && !GameIsPaused)
			{
				Debug2Log("time scale: " + Time.timeScale.ToString("G3") + ", Δt: " + Time.fixedDeltaTime.ToString("G3"));
				Debug2Log("offsetDirection: " + offsetDirectionX.ToString("G3"));
				Debug2Log("target offset: " + ((vessel.CoM - dogfightLastTargetPosition).normalized * dogfightDistance).ToString("G4"));
				Debug2Log("xOff: " + (dogfightOffsetX * offsetDirectionX).ToString("G3"));
				Debug2Log("yOff: " + (dogfightOffsetY * dogfightCameraRollUp).ToString("G3"));
				Debug2Log("camPos - vessel.CoM: " + (camPos - vessel.CoM).ToString("G3"));
				Debug2Log("localCamPos: " + localCamPos.ToString("G3") + ", " + cameraTransform.localPosition.ToString("G3"));
				Debug2Log($"lerp momentum: {dogfightLerpMomentum:G3}");
				Debug2Log($"lerp delta: {dogfightLerpDelta:G3}");
			}

			//rotation
			if (Input.GetKey(KeyCode.Mouse1)) // Free-look
			{
				if (!freeLook)
				{
					freeLookStartUpDistance.x += Input.GetAxis("Mouse X");
					freeLookStartUpDistance.y += -Input.GetAxis("Mouse Y");
					if (freeLookStartUpDistance.sqrMagnitude > freeLookThresholdSqr)
						freeLook = true;
				}
			}
			else
			{
				freeLookStartUpDistance = Vector2.zero;
				if (freeLook)
				{
					freeLook = false;
					if (MouseAimFlight.IsMouseAimActive()) MouseAimFlight.SetFreeLookCooldown(1); // Give it 1s for the camera orientation to recover before resuming applying our modification to the MouseAimFlight target.
				}
			}
			if (freeLook)
			{
				cameraTransform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f, Vector3.up);
				cameraTransform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f, Vector3.right);
				cameraTransform.rotation = Quaternion.LookRotation(cameraTransform.forward, dogfightCameraRollUp);
			}
			else
			{
				Quaternion vesselLook = Quaternion.LookRotation(vessel.CoM - cameraTransform.position, dogfightCameraRollUp);
				Quaternion targetLook = Quaternion.LookRotation(dogfightLastTargetPosition - cameraTransform.position, dogfightCameraRollUp);
				Quaternion camRot = Quaternion.Lerp(vesselLook, targetLook, 0.5f);
				cameraTransform.rotation = Quaternion.Lerp(cameraTransform.rotation, camRot, dogfightLerp);
				if (MouseAimFlight.IsMouseAimActive())
				{
					if (!MouseAimFlight.IsInFreeLookRecovery)
					{
						// mouseAimFlightTarget keeps the target stationary (i.e., no change from the default)
						// cameraTransform.TransformDirection(mouseAimFlightTargetLocal) moves the target fully with the camera
						var newMouseAimFlightTarget = cameraTransform.TransformDirection(mouseAimFlightTargetLocal);
						newMouseAimFlightTarget = Vector3.Lerp(newMouseAimFlightTarget, mouseAimFlightTarget, Mathf.Min((newMouseAimFlightTarget - mouseAimFlightTarget).magnitude * 0.01f, 0.5f));
						MouseAimFlight.SetMouseAimTarget(newMouseAimFlightTarget); // Adjust how MouseAimFlight updates the target position for easier control in combat.
					}
				}
			}

			//autoFov
			if (autoFOV)
			{
				float targetFoV;
				if (dogfightVelocityChase)
				{
					targetFoV = Mathf.Clamp((7000 / (dogfightDistance + 100)) - 14 + autoZoomMargin, 2, 60);
				}
				else
				{
					float angle = Vector3.Angle(dogfightLastTargetPosition - cameraTransform.position, vessel.CoM - cameraTransform.position);
					targetFoV = Mathf.Clamp(angle + autoZoomMargin, 0.1f, 60f);
				}
				manualFOV = targetFoV;
			}
			//FOV
			if (!autoFOV)
			{
				zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				manualFOV = 60 / zoomFactor;
				updateFOV = (currentFOV != manualFOV);
				if (updateFOV)
				{
					currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
					flightCamera.SetFoV(currentFOV);
					updateFOV = false;
				}
			}
			else
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				zoomFactor = 60 / currentFOV;
			}

			//free move
			if (enableKeypad && !boundThisFrame)
			{
				switch (fmMode)
				{
					case fmModeTypes.Position:
						{
							if (Input.GetKey(fmUpKey))
							{
								dogfightOffsetY += freeMoveSpeed * Time.fixedDeltaTime;
								dogfightOffsetY = Mathf.Clamp(dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
								if (textInput) inputFields["dogfightOffsetY"].currentValue = dogfightOffsetY;
							}
							else if (Input.GetKey(fmDownKey))
							{
								dogfightOffsetY -= freeMoveSpeed * Time.fixedDeltaTime;
								dogfightOffsetY = Mathf.Clamp(dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
								if (textInput) inputFields["dogfightOffsetY"].currentValue = dogfightOffsetY;
							}
							if (Input.GetKey(fmForwardKey))
							{
								dogfightDistance -= freeMoveSpeed * Time.fixedDeltaTime;
								dogfightDistance = Mathf.Clamp(dogfightDistance, 1f, dogfightMaxDistance);
								if (textInput) inputFields["dogfightDistance"].currentValue = dogfightDistance;
							}
							else if (Input.GetKey(fmBackKey))
							{
								dogfightDistance += freeMoveSpeed * Time.fixedDeltaTime;
								dogfightDistance = Mathf.Clamp(dogfightDistance, 1f, dogfightMaxDistance);
								if (textInput) inputFields["dogfightDistance"].currentValue = dogfightDistance;
							}
							if (Input.GetKey(fmLeftKey))
							{
								dogfightOffsetX -= freeMoveSpeed * Time.fixedDeltaTime;
								dogfightOffsetX = Mathf.Clamp(dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
								if (textInput) inputFields["dogfightOffsetX"].currentValue = dogfightOffsetX;
							}
							else if (Input.GetKey(fmRightKey))
							{
								dogfightOffsetX += freeMoveSpeed * Time.fixedDeltaTime;
								dogfightOffsetX = Mathf.Clamp(dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
								if (textInput) inputFields["dogfightOffsetX"].currentValue = dogfightOffsetX;
							}

							//keyZoom
							if (!autoFOV)
							{
								if (Input.GetKey(fmZoomInKey))
								{
									zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
							}
							else
							{
								if (Input.GetKey(fmZoomInKey))
								{
									autoZoomMargin = Mathf.Clamp(autoZoomMargin + (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, autoZoomMarginMax);
									if (textInput) inputFields["autoZoomMargin"].currentValue = autoZoomMargin;
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									autoZoomMargin = Mathf.Clamp(autoZoomMargin - (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, autoZoomMarginMax);
									if (textInput) inputFields["autoZoomMargin"].currentValue = autoZoomMargin;
								}
							}
						}
						break;
					case fmModeTypes.Speed:
						{
							if (Input.GetKey(fmUpKey))
							{
								fmSpeeds.y += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmDownKey))
							{
								fmSpeeds.y -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmForwardKey))
							{
								fmSpeeds.z -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmBackKey))
							{
								fmSpeeds.z += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmLeftKey))
							{
								fmSpeeds.x -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmRightKey))
							{
								fmSpeeds.x += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmZoomInKey))
							{
								fmSpeeds.w += keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmZoomOutKey))
							{
								fmSpeeds.w -= keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
						}
						break;
				}
			}

			//vessel camera shake
			if (shakeMultiplier > 0)
			{
				foreach (var v in FlightGlobals.Vessels)
				{
					if (!v || !v.loaded || v.packed || v.isActiveVessel) continue;
					VesselCameraShake(v);
				}
			}

			if (bdArmory.hasBDA && bdArmory.hasBDAI && (bdArmory.useBDAutoTarget || (bdArmory.useCentroid && bdArmory.bdWMVessels.Count < 2)))
			{
				bdArmory.UpdateAIDogfightTarget(); // Using delegates instead of reflection allows us to check every frame.
				if (vessel.LandedOrSplashed)
				{
					var cameraRadarAltitude = GetRadarAltitudeAtPos(cameraTransform.position);
					if (cameraRadarAltitude < 2f && (vessel.Landed || cameraRadarAltitude > -dogfightDistance)) cameraTransform.position += (2f - cameraRadarAltitude) * cameraUp; // Prevent viewing from under the surface if near the surface.
					if (DEBUG2 && !GameIsPaused) Debug2Log($"camera altitude: {GetRadarAltitudeAtPos(cameraTransform.position):G3} ({cameraRadarAltitude:G3})");
				}
				else if (DEBUG2 && !GameIsPaused) Debug2Log($"vessel not landed");
			}

			if (dogfightTarget != dogfightPrevTarget)
			{
				StartDogfightCamera();
			}
		}
		#endregion

		#region Stationary Camera
		Quaternion stationaryCameraRoll = Quaternion.identity;
		void StartStationaryCamera()
		{
			toolMode = ToolModes.StationaryCamera;
			if (FlightGlobals.ActiveVessel != null)
			{
				if (DEBUG)
				{
					message = "Starting stationary camera.";
					Debug.Log("[CameraTools]: " + message);
					DebugLog(message);
				}
				hasDied = false;
				vessel = FlightGlobals.ActiveVessel;
				cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).normalized;
				if (origMode == FlightCamera.Modes.ORBITAL || (origMode == FlightCamera.Modes.AUTO && FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL))
				{
					cameraUp = Vector3.up;
				}
				rightAxis = -Vector3.Cross(vessel.Velocity(), vessel.upAxis).normalized;
				stationaryCameraRoll = Quaternion.identity;

				if (flightCamera.transform.parent != cameraParent.transform)
				{
					SetCameraParent(deathCam.transform, true); // First update the cameraParent to the last deathCam configuration
				}

				manualPosition = Vector3.zero;
				if (randomMode)
				{
					camTarget = FlightGlobals.ActiveVessel.GetReferenceTransformPart();
					if (camTarget == null) // Sometimes the vessel doesn't have the reference transform part set up. It ought to be the root part usually.
						camTarget = FlightGlobals.ActiveVessel.rootPart;
				}
				hasTarget = (camTarget != null) ? true : false;
				lastVesselCoM = vessel.CoM;

				// Camera position.
				if (!randomMode && autoLandingPosition && GetAutoLandingPosition()) // Set up a landing shot if possible or fall back on other methods.
				{ }
				else if (autoFlybyPosition || randomMode)
				{
					setPresetOffset = false;

					float clampedSpeed = Mathf.Clamp((float)vessel.srfSpeed, 0, Mathf.Abs(maxRelV));
					float sideDistance = Mathf.Clamp(20 + (clampedSpeed / 10), 20, 150);
					float distanceAhead = Mathf.Clamp(4 * clampedSpeed, 30, 3500) * Mathf.Sign(maxRelV);

					if (vessel.Velocity().sqrMagnitude > 1)
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.Velocity().normalized; }
					else
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.vesselTransform.up; }

					if (flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (15 * cameraUp);
					}
					else if (flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (15 * Vector3.up);
					}

					var cameraRadarAltitude = GetRadarAltitudeAtPos(flightCamera.transform.position);
					if (vessel.radarAltitude > 0f && vessel.radarAltitude < -3d * vessel.verticalSpeed) // 3s to impact
					{
						flightCamera.transform.position += (35f - cameraRadarAltitude) * cameraUp;
						cameraRadarAltitude = GetRadarAltitudeAtPos(flightCamera.transform.position);
					}

					// Correct for being below terrain/water (min of 30m AGL).
					if (cameraRadarAltitude < 30f)
					{
						flightCamera.transform.position += (30f - cameraRadarAltitude) * cameraUp;
					}
					if (vessel.radarAltitude > 0f) // Make sure terrain isn't in the way (as long as the target is above ground).
					{
						int count = 0;
						Ray ray;
						RaycastHit hit;
						do
						{
							ray = new Ray(flightCamera.transform.position, vessel.transform.position - flightCamera.transform.position);
							if (Physics.Raycast(ray, out hit, (flightCamera.transform.position - vessel.transform.position).magnitude, 1 << 15)) // Just terrain.
							{
								flightCamera.transform.position += 50f * cameraUp; // Try 50m higher.
							}
							else
							{
								break;
							} // We're clear.
						} while (hit.collider != null && ++count < 100); // Max 5km higher.
					}
				}
				else if (manualOffset)
				{
					setPresetOffset = false;
					float sideDistance = manualOffsetRight;
					float distanceAhead = manualOffsetForward;

					if (vessel.Velocity().sqrMagnitude > 1)
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.Velocity().normalized; }
					else
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.vesselTransform.up; }

					if (flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (manualOffsetUp * cameraUp);
					}
					else if (flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (manualOffsetUp * Vector3.up);
					}
				}
				else if (setPresetOffset)
				{
					flightCamera.transform.position = presetOffset;
					//setPresetOffset = false;
				}

				// Camera rotation.
				if (hasTarget)
				{ flightCamera.transform.rotation = Quaternion.LookRotation(vessel.transform.position - flightCamera.transform.position, cameraUp); }

				// Initial velocity
				initialVelocity = vessel.Velocity();
				initialOrbit = new Orbit(vessel.orbit);

				cameraToolActive = true;

				ResetDoppler();
				if (OnResetCTools != null)
				{ OnResetCTools(); }
				SetDoppler(true);
				AddAtmoAudioControllers(true);
			}
			else
			{
				Debug.Log("[CameraTools]: Stationary Camera failed. Active Vessel is null.");
			}
			if (hasSavedRotation) { flightCamera.transform.rotation = savedRotation; }
		}

		/// <summary>
		/// Get the auto-landing position.
		/// This is the vessel's current position + the manual offset.
		/// If maintain velocity is enabled, then add an additional horizontal component for where the craft would land if it follows a ballistic trajectory, assuming flat terrain.
		/// </summary>
		/// <returns></returns>
		bool GetAutoLandingPosition()
		{
			if (maintainInitialVelocity && !(vessel.situation == Vessel.Situations.FLYING || vessel.situation == Vessel.Situations.SUB_ORBITAL)) return false; // In orbit or on the surface already.
			var velForwardAxis = Vector3.ProjectOnPlane(vessel.srf_vel_direction, cameraUp).normalized;
			var velRightAxis = Vector3.Cross(cameraUp, velForwardAxis);
			var position = vessel.transform.position + velForwardAxis * manualOffsetForward + velRightAxis * manualOffsetRight;
			var heightAboveTerrain = GetRadarAltitudeAtPos(position);
			if (maintainInitialVelocity) // Predict where the landing is going to be assuming it follows a ballistic trajectory.
			{
				var gravity = -FlightGlobals.getGeeForceAtPosition(vessel.transform.position).magnitude;
				int count = 0;
				float velOffset = 0;
				float lastVelOffset = velOffset;
				do
				{
					var timeToLanding = (-vessel.verticalSpeed - MathUtils.Sqrt(vessel.verticalSpeed * vessel.verticalSpeed - 2 * gravity * heightAboveTerrain)) / gravity; // G is <0, so - branch is always the right one.
					lastVelOffset = velOffset;
					velOffset = (float)(vessel.horizontalSrfSpeed * timeToLanding);
					position = vessel.transform.position + velForwardAxis * (manualOffsetForward + velOffset) + velRightAxis * manualOffsetRight;
					heightAboveTerrain = GetRadarAltitudeAtPos(position);
				} while (++count < 10 && Mathf.Abs(velOffset - lastVelOffset) > 1f); // Up to 10 iterations to find a somewhat stable solution (within 1m).
			}
			flightCamera.transform.position = position + (manualOffsetUp - heightAboveTerrain) * cameraUp; // Correct the camera altitude.
			autoLandingCamEnabled = true;
			return true;
		}

		Vector3 lastOffset = Vector3.zero;
		Vector3 offsetSinceLastFrame = Vector3.zero;
		Vector3 lastOffsetSinceLastFrame = Vector3.zero;
		Vector3 lastCameraPosition = Vector3.zero;
		Vector3 lastCamParentPosition = Vector3.zero;
		void UpdateStationaryCamera()
		{
			if (useAudioEffects)
			{
				speedOfSound = 233 * MathUtils.Sqrt(1 + (FlightGlobals.getExternalTemperature(vessel.GetWorldPos3D(), vessel.mainBody) / 273.15));
				// if (DEBUG) Debug.Log($"[CameraTools]: Speed of sound is {speedOfSound:G5}m/s");
			}

			if (flightCamera.Target != null) flightCamera.SetTargetNone(); // Don't go to the next vessel if the vessel is destroyed.

			if (Input.GetKey(fmMovementModifier))
			{
				upAxis = flightCamera.transform.up;
				forwardAxis = flightCamera.transform.forward;
				rightAxis = flightCamera.transform.right;
			}
			else
			{
				upAxis = stationaryCameraRoll * cameraUp;
				forwardAxis = Vector3.RotateTowards(upAxis, flightCamera.transform.forward, Mathf.Deg2Rad * 90, 0).normalized;
				rightAxis = Vector3.Cross(upAxis, forwardAxis);
			}

			// Set camera position before rotation to avoid jitter.
			if (vessel != null)
			{
				lastCameraPosition = flightCamera.transform.position;
				offsetSinceLastFrame = vessel.CoM - lastVesselCoM;
				if (DEBUG2 && !GameIsPaused && !offsetSinceLastFrame.IsZero())
				{
					lastOffsetSinceLastFrame = offsetSinceLastFrame;
				}
				lastVesselCoM = vessel.CoM;
				cameraParent.transform.position = manualPosition + vessel.CoM;
				if (!randomMode && vessel.srfSpeed > maxRelV / 2 && offsetSinceLastFrame.sqrMagnitude > signedMaxRelVSqr * TimeWarp.fixedDeltaTime * TimeWarp.fixedDeltaTime) // Account for maxRelV. Note: we use fixedDeltaTime here as we're interested in how far it jumped on the physics update. Also check for srfSpeed to account for changes in CoM when on launchpad (srfSpeed < maxRelV/2 should be good for maxRelV down to around 1 in most cases). Also, ignore this when using random mode.
				{
					offsetSinceLastFrame = maxRelV * TimeWarp.fixedDeltaTime * offsetSinceLastFrame.normalized;
				}
				if (!offsetSinceLastFrame.IsZero()) flightCamera.transform.position -= offsetSinceLastFrame;
			}

			// Set camera rotation.
			if (camTarget != null)
			{
				Vector3 lookPosition = camTarget.transform.position;
				if (targetCoM)
				{
					lookPosition = camTarget.vessel.CoM;
				}

				flightCamera.transform.rotation = Quaternion.LookRotation(lookPosition - flightCamera.transform.position, upAxis);
				lastTargetPosition = lookPosition;
			}
			else if (hasTarget)
			{
				flightCamera.transform.rotation = Quaternion.LookRotation(lastTargetPosition - flightCamera.transform.position, upAxis);
			}

			if (DEBUG2 && !GameIsPaused)
			{
				var Δ = lastOffset - (vessel.transform.position - flightCamera.transform.position);
				Debug2Log("situation: " + vessel.situation);
				Debug2Log("warp mode: " + TimeWarp.WarpMode + ", fixedDeltaTime: " + TimeWarp.fixedDeltaTime);
				Debug2Log("floating origin offset: " + FloatingOrigin.Offset.ToString("G6"));
				Debug2Log("offsetNonKB: " + FloatingOrigin.OffsetNonKrakensbane.ToString("G6"));
				Debug2Log("vv*Δt: " + (vessel.obt_velocity * TimeWarp.fixedDeltaTime).ToString("G6"));
				Debug2Log("sv*Δt: " + (vessel.srf_velocity * TimeWarp.fixedDeltaTime).ToString("G6"));
				Debug2Log("kv*Δt: " + (Krakensbane.GetFrameVelocity() * TimeWarp.fixedDeltaTime).ToString("G6"));
				Debug2Log("ΔKv: " + Krakensbane.GetLastCorrection().ToString("G6"));
				Debug2Log("sv*Δt-onkb: " + (vessel.srf_velocity * TimeWarp.fixedDeltaTime - FloatingOrigin.OffsetNonKrakensbane).ToString("G6"));
				Debug2Log("kv*Δt-onkb: " + (Krakensbane.GetFrameVelocity() * TimeWarp.fixedDeltaTime - FloatingOrigin.OffsetNonKrakensbane).ToString("G6"));
				Debug2Log("floatingKrakenAdjustment: " + floatingKrakenAdjustment.ToString("G6"));
				Debug2Log("(sv-kv)*Δt" + ((vessel.srf_velocity - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime).ToString("G6"));
				Debug2Log("Parent pos: " + cameraParent.transform.position.ToString("G6"));
				Debug2Log("Camera pos: " + flightCamera.transform.position.ToString("G6"));
				Debug2Log("ΔCamera: " + (flightCamera.transform.position - lastCameraPosition).ToString("G6"));
				Debug2Log("δp: " + (cameraParent.transform.position - lastCamParentPosition).ToString("G6"));
				Debug2Log("ΔCamera + δp: " + (flightCamera.transform.position - lastCameraPosition + cameraParent.transform.position - lastCamParentPosition).ToString("G6"));
				Debug2Log("δ: " + lastOffsetSinceLastFrame.ToString("G6"));
				Debug2Log("Δ: " + Δ.ToString("G6"));
				Debug2Log("δ + Δ: " + (lastOffsetSinceLastFrame + Δ).ToString("G6"));
				lastOffset = vessel.transform.position - flightCamera.transform.position;
				lastCamParentPosition = cameraParent.transform.position;
			}

			//free move
			if (enableKeypad && !boundThisFrame)
			{
				switch (fmMode)
				{
					case fmModeTypes.Position:
						{
							if (Input.GetKey(fmUpKey))
							{
								manualPosition += upAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmDownKey))
							{
								manualPosition -= upAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmForwardKey))
							{
								manualPosition += forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmBackKey))
							{
								manualPosition -= forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmLeftKey))
							{
								manualPosition -= rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmRightKey))
							{
								manualPosition += rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
							}

							//keyZoom
							if (!autoFOV)
							{
								if (Input.GetKey(fmZoomInKey))
								{
									zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
							}
							else
							{
								if (Input.GetKey(fmZoomInKey))
								{
									autoZoomMargin = Mathf.Clamp(autoZoomMargin + (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, autoZoomMarginMax);
									if (textInput) inputFields["autoZoomMargin"].currentValue = autoZoomMargin;
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									autoZoomMargin = Mathf.Clamp(autoZoomMargin - (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, autoZoomMarginMax);
									if (textInput) inputFields["autoZoomMargin"].currentValue = autoZoomMargin;
								}
							}
						}
						break;
					case fmModeTypes.Speed:
						{
							if (Input.GetKey(fmUpKey))
							{
								fmSpeeds.y += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmDownKey))
							{
								fmSpeeds.y -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmForwardKey))
							{
								fmSpeeds.z += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmBackKey))
							{
								fmSpeeds.z -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmLeftKey))
							{
								fmSpeeds.x -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmRightKey))
							{
								fmSpeeds.x += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							if (Input.GetKey(fmZoomInKey))
							{
								fmSpeeds.w += keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
							else if (Input.GetKey(fmZoomOutKey))
							{
								fmSpeeds.w -= keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
							}
						}
						break;
				}

				if (!string.IsNullOrEmpty(resetRollKey) && Input.GetKeyDown(resetRollKey))
				{
					stationaryCameraRoll = Quaternion.identity;
					flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, stationaryCameraRoll * cameraUp);
				}
			}

			if (Input.GetKey(KeyCode.Mouse1) && Input.GetKey(KeyCode.Mouse2))
			{
				stationaryCameraRoll = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * -1.7f, flightCamera.transform.forward) * stationaryCameraRoll;
				flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, stationaryCameraRoll * cameraUp);
			}
			else
			{
				if (camTarget == null && Input.GetKey(KeyCode.Mouse1))
				{
					flightCamera.transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f, Vector3.up); //*(Mathf.Abs(Mouse.delta.x)/7)
					flightCamera.transform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f, Vector3.right);
					flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, stationaryCameraRoll * cameraUp);
				}
				if (Input.GetKey(KeyCode.Mouse2))
				{
					manualPosition += rightAxis * Input.GetAxis("Mouse X") * 2;
					manualPosition += forwardAxis * Input.GetAxis("Mouse Y") * 2;
				}
			}
			manualPosition += upAxis * 10 * Input.GetAxis("Mouse ScrollWheel");

			//autoFov
			if (camTarget != null && autoFOV)
			{
				float cameraDistance = Vector3.Distance(camTarget.transform.position, flightCamera.transform.position);
				float targetFoV = Mathf.Clamp((7000 / (cameraDistance + 100)) - 14 + autoZoomMargin, 2, 60);
				//flightCamera.SetFoV(targetFoV);	
				manualFOV = targetFoV;
			}
			//FOV
			if (!autoFOV)
			{
				zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				manualFOV = 60 / zoomFactor;
				updateFOV = (currentFOV != manualFOV);
				if (updateFOV)
				{
					currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
					flightCamera.SetFoV(currentFOV);
					updateFOV = false;
				}
			}
			else
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				zoomFactor = 60 / currentFOV;
			}

			//vessel camera shake
			if (shakeMultiplier > 0)
			{
				foreach (var v in FlightGlobals.Vessels)
				{
					if (!v || !v.loaded || v.packed) continue;
					VesselCameraShake(v);
				}
			}
		}
		#endregion

		#region Pathing Camera
		void StartPathingCam()
		{
			toolMode = ToolModes.Pathing;
			if (selectedPathIndex < 0 || currentPath.keyframeCount <= 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: Unable to start pathing camera due to no valid paths.");
				RevertCamera();
				return;
			}
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.LogWarning("[CameraTools]: Unable to start pathing camera due to no active vessel.");
				RevertCamera();
				return;
			}
			if (DEBUG)
			{
				message = "Starting pathing camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			hasDied = false;
			vessel = FlightGlobals.ActiveVessel;
			cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).normalized;
			if (FlightCamera.fetch.mode == FlightCamera.Modes.ORBITAL || (FlightCamera.fetch.mode == FlightCamera.Modes.AUTO && FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL))
			{
				cameraUp = Vector3.up;
			}
			pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing);

			SetCameraParent(vessel.transform);
			cameraToolActive = true;
		}

		void UpdatePathingCam()
		{
			cameraParent.transform.position = vessel.transform.position;
			cameraParent.transform.rotation = vessel.transform.rotation;

			if (isPlayingPath)
			{
				CameraTransformation tf = currentPath.Evaulate(pathTime * currentPath.timeScale);
				flightCamera.transform.localPosition = Vector3.Lerp(flightCamera.transform.localPosition, tf.position, pathingLerpRate);
				flightCamera.transform.localRotation = Quaternion.Slerp(flightCamera.transform.localRotation, tf.rotation, pathingLerpRate);
				zoomExp = Mathf.Lerp(zoomExp, tf.zoom, pathingLerpRate);

			}
			else
			{
				//move
				//mouse panning, moving
				if (Input.GetKey(fmMovementModifier))
				{
					// Note: The forwardAxis and rightAxis are reversed as this is more convenient when viewing the vessel from the front (which is a more typical use-case).
					upAxis = -vessel.ReferenceTransform.forward;
					forwardAxis = -vessel.ReferenceTransform.up;
					rightAxis = -vessel.ReferenceTransform.right;
				}
				else
				{
					upAxis = flightCamera.transform.up;
					forwardAxis = flightCamera.transform.forward;
					rightAxis = flightCamera.transform.right;
				}

				if (enableKeypad && !boundThisFrame)
				{
					switch (fmMode)
					{
						case fmModeTypes.Position:
							{
								if (Input.GetKey(fmUpKey))
								{
									flightCamera.transform.position += upAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmDownKey))
								{
									flightCamera.transform.position -= upAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}
								if (Input.GetKey(fmForwardKey))
								{
									flightCamera.transform.position += forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmBackKey))
								{
									flightCamera.transform.position -= forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}
								if (Input.GetKey(fmLeftKey))
								{
									flightCamera.transform.position -= rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmRightKey))
								{
									flightCamera.transform.position += rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
								}

								//keyZoom Note: pathing doesn't use autoZoomMargin
								if (Input.GetKey(fmZoomInKey))
								{
									zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, zoomMaxExp);
									if (textInput) inputFields["zoomFactor"].currentValue = Mathf.Exp(zoomExp) / Mathf.Exp(1);
								}
							}
							break;
						case fmModeTypes.Speed:
							{
								if (Input.GetKey(fmUpKey))
								{
									fmSpeeds.y += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmDownKey))
								{
									fmSpeeds.y -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								if (Input.GetKey(fmForwardKey))
								{
									fmSpeeds.z += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmBackKey))
								{
									fmSpeeds.z -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								if (Input.GetKey(fmLeftKey))
								{
									fmSpeeds.x -= freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmRightKey))
								{
									fmSpeeds.x += freeMoveSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								if (Input.GetKey(fmZoomInKey))
								{
									fmSpeeds.w += keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
								else if (Input.GetKey(fmZoomOutKey))
								{
									fmSpeeds.w -= keyZoomSpeed * Time.fixedDeltaTime * Time.fixedDeltaTime;
								}
							}
							break;
					}
				}

				if (Input.GetKey(KeyCode.Mouse1) && Input.GetKey(KeyCode.Mouse2)) // Middle & right: tilt left/right
				{
					flightCamera.transform.rotation = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * -1.7f, flightCamera.transform.forward) * flightCamera.transform.rotation;
				}
				else if (Input.GetKey(KeyCode.Mouse0) && Input.GetKey(KeyCode.Mouse2)) // Left & middle: move up/down
				{
					flightCamera.transform.position += upAxis * Input.GetAxis("Mouse Y") * 2;
				}
				else
				{
					if (Input.GetKey(KeyCode.Mouse1)) // Right: rotate (pitch/yaw) around the camera pivot
					{
						flightCamera.transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f / (zoomExp * zoomExp), Vector3.up);
						flightCamera.transform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f / (zoomExp * zoomExp), Vector3.right);
					}
					if (Input.GetKey(KeyCode.Mouse2)) // Middle: move left/right and forward/backward
					{
						flightCamera.transform.position += rightAxis * Input.GetAxis("Mouse X") * 2;
						flightCamera.transform.position += forwardAxis * Input.GetAxis("Mouse Y") * 2;
					}
				}
				if (freeMoveSpeedRaw != (freeMoveSpeedRaw = Mathf.Clamp(freeMoveSpeedRaw + 0.5f * Input.GetAxis("Mouse ScrollWheel"), freeMoveSpeedMinRaw, freeMoveSpeedMaxRaw)))
				{
					freeMoveSpeed = Mathf.Pow(10f, freeMoveSpeedRaw);
					if (textInput) inputFields["freeMoveSpeed"].currentValue = freeMoveSpeed;
				}
			}

			//zoom
			zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
			manualFOV = 60 / zoomFactor;
			updateFOV = (currentFOV != manualFOV);
			if (updateFOV)
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				updateFOV = false;
			}
		}

		void CreateNewPath()
		{
			showKeyframeEditor = false;
			availablePaths.Add(new CameraPath());
			selectedPathIndex = availablePaths.Count - 1;
			if (isPlayingPath) StopPlayingPath();
		}

		void DeletePath(int index)
		{
			if (index < 0) return;
			if (index >= availablePaths.Count) return;
			availablePaths.RemoveAt(index);
			if (index <= selectedPathIndex) { --selectedPathIndex; }
			if (isPlayingPath) StopPlayingPath();
		}

		void SelectPath(int index)
		{
			selectedPathIndex = index;
		}

		void SelectKeyframe(int index)
		{
			if (isPlayingPath)
			{
				StopPlayingPath();
			}
			currentKeyframeIndex = index;
			UpdateCurrentValues();
			showKeyframeEditor = true;
			ViewKeyframe(currentKeyframeIndex);
		}

		void DeselectKeyframe()
		{
			currentKeyframeIndex = -1;
			showKeyframeEditor = false;
		}

		void DeleteKeyframe(int index)
		{
			currentPath.RemoveKeyframe(index);
			if (index == currentKeyframeIndex)
			{
				DeselectKeyframe();
			}
			if (currentPath.keyframeCount > 0 && currentKeyframeIndex >= 0)
			{
				SelectKeyframe(Mathf.Clamp(currentKeyframeIndex, 0, currentPath.keyframeCount - 1));
			}
			else
			{
				if (isPlayingPath) StopPlayingPath();
			}
		}

		void UpdateCurrentValues()
		{
			if (currentPath == null) return;
			if (currentKeyframeIndex < 0 || currentKeyframeIndex >= currentPath.keyframeCount)
			{
				return;
			}
			CameraKeyframe currentKey = currentPath.GetKeyframe(currentKeyframeIndex);
			currentKeyframeTime = currentKey.time;
			currentKeyframePositionInterpolationType = currentKey.positionInterpolationType;
			currentKeyframeRotationInterpolationType = currentKey.rotationInterpolationType;

			currKeyTimeString = currentKeyframeTime.ToString();
		}

		void CreateNewKeyframe()
		{
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.LogWarning("[CameraTools]: Unable to create new pathing keyframe without an active vessel.");
			}
			showPathSelectorWindow = false;

			float time = 0;
			PositionInterpolationType positionInterpolationType = PositionInterpolationType.CubicSpline;
			RotationInterpolationType rotationInterpolationType = RotationInterpolationType.CubicSpline;
			if (currentPath.keyframeCount > 0)
			{
				CameraKeyframe previousKeyframe = currentPath.GetKeyframe(currentPath.keyframeCount - 1);
				positionInterpolationType = previousKeyframe.positionInterpolationType;
				rotationInterpolationType = previousKeyframe.rotationInterpolationType;

				if (isPlayingPath)
				{
					time = pathTime * currentPath.timeScale;
				}
				else
				{
					time = previousKeyframe.time + 10;
				}
			}

			if (!cameraToolActive)
			{
				if (flightCamera.FieldOfView != flightCamera.fovDefault)
				{
					zoomFactor = 60 / flightCamera.FieldOfView;
					zoomExp = Mathf.Log(zoomFactor) + 1f;
				}

				if (!cameraParentWasStolen)
					SaveOriginalCamera();
				SetCameraParent(FlightGlobals.ActiveVessel.transform);
				cameraToolActive = true;
			}

			currentPath.AddTransform(flightCamera.transform, zoomExp, time, positionInterpolationType, rotationInterpolationType);

			SelectKeyframe(currentPath.times.IndexOf(time));

			if (currentPath.keyframeCount > 6)
			{
				keysScrollPos.y += entryHeight;
			}
		}

		void ViewKeyframe(int index)
		{
			if (!cameraToolActive)
			{
				StartPathingCam();
			}
			CameraKeyframe currentKey = currentPath.GetKeyframe(index);
			flightCamera.transform.localPosition = currentKey.position;
			flightCamera.transform.localRotation = currentKey.rotation;
			SetZoomImmediate(currentKey.zoom);
		}

		void PlayPathingCam()
		{
			if (DEBUG)
			{
				message = "Playing pathing camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			if (selectedPathIndex < 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: selectedPathIndex < 0, reverting.");
				RevertCamera();
				return;
			}

			if (currentPath.keyframeCount <= 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: keyframeCount <= 0, reverting.");
				RevertCamera();
				return;
			}

			float startTime = 0;
			if (currentKeyframeIndex > -1)
			{
				startTime = currentPath.GetKeyframe(currentKeyframeIndex).time;
			}

			DeselectKeyframe();

			if (!cameraToolActive)
			{
				StartPathingCam();
			}

			CameraTransformation firstFrame = currentPath.Evaulate(startTime);
			flightCamera.transform.localPosition = firstFrame.position;
			flightCamera.transform.localRotation = firstFrame.rotation;
			SetZoomImmediate(firstFrame.zoom);

			isPlayingPath = true;
			pathStartTime = GetTime() - (startTime / currentPath.timeScale);
		}

		void StopPlayingPath()
		{
			isPlayingPath = false;
		}

		void TogglePathList()
		{
			showKeyframeEditor = false;
			showPathSelectorWindow = !showPathSelectorWindow;
		}
		#endregion

		#region Shake
		public void ShakeCamera(float magnitude)
		{
			shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
		}

		void UpdateCameraShake()
		{
			if (shakeMultiplier > 0)
			{
				if (shakeMagnitude > 0.1f)
				{
					Vector3 shakeAxis = UnityEngine.Random.onUnitSphere;
					shakeOffset = Mathf.Sin(shakeMagnitude * 20 * Time.time) * (shakeMagnitude / 10) * shakeAxis;
				}


				flightCamera.transform.rotation = Quaternion.AngleAxis((shakeMultiplier / 2) * shakeMagnitude / 50f, Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, flightCamera.transform.forward)) * flightCamera.transform.rotation;
			}

			shakeMagnitude = Mathf.Lerp(shakeMagnitude, 0, 0.1f);
		}

		public void VesselCameraShake(Vessel vessel)
		{
			if (vessel.vesselType == VesselType.Debris) return; // Ignore debris

			//shake
			float camDistance = Vector3.Distance(flightCamera.transform.position, vessel.CoM);

			float distanceFactor = 50f / camDistance;
			float fovFactor = 2f / zoomFactor;
			float thrustFactor = GetTotalThrust() / 1000f;

			float atmosphericFactor = (float)vessel.dynamicPressurekPa / 2f;

			float angleToCam = Vector3.Angle(vessel.Velocity(), FlightCamera.fetch.mainCamera.transform.position - vessel.transform.position);
			angleToCam = Mathf.Clamp(angleToCam, 1, 180);

			float srfSpeed = (float)vessel.srfSpeed;

			float lagAudioFactor = (75000 / (Vector3.Distance(vessel.transform.position, FlightCamera.fetch.mainCamera.transform.position) * srfSpeed * angleToCam / 90));
			lagAudioFactor = Mathf.Clamp(lagAudioFactor * lagAudioFactor * lagAudioFactor, 0, 4);
			lagAudioFactor += srfSpeed / 230;

			float waveFrontFactor = ((3.67f * angleToCam) / srfSpeed);
			waveFrontFactor = Mathf.Clamp(waveFrontFactor * waveFrontFactor * waveFrontFactor, 0, 2);
			if (vessel.srfSpeed > 330)
			{
				waveFrontFactor = (srfSpeed / (angleToCam) < 3.67f) ? srfSpeed / 15 : 0;
			}

			lagAudioFactor *= waveFrontFactor;

			lagAudioFactor = Mathf.Clamp01(lagAudioFactor) * distanceFactor * fovFactor;

			atmosphericFactor *= lagAudioFactor;

			thrustFactor *= distanceFactor * fovFactor * lagAudioFactor;

			ShakeCamera(atmosphericFactor + thrustFactor);
		}

		float GetTotalThrust()
		{
			float total = 0;
			using (var engine = engines.GetEnumerator())
				while (engine.MoveNext())
				{
					if (engine.Current == null) continue;
					total += engine.Current.finalThrust;
				}
			return total;
		}
		#endregion

		#region Atmospherics
		void AddAtmoAudioControllers(bool includeActiveVessel)
		{
			if (!useAudioEffects)
			{
				return;
			}

			foreach (var vessel in FlightGlobals.Vessels)
			{
				if (!vessel || !vessel.loaded || vessel.packed || (!includeActiveVessel && vessel.isActiveVessel))
				{
					continue;
				}
				if (ignoreVesselTypesForAudio.Contains(vessel.vesselType)) continue;

				if (vessel.gameObject.GetComponent<CTAtmosphericAudioController>() == null)
				{ vessel.gameObject.AddComponent<CTAtmosphericAudioController>(); }
			}
		}

		void SetDoppler(bool includeActiveVessel)
		{
			if (hasSetDoppler)
			{
				return;
			}

			if (!useAudioEffects)
			{
				return;
			}

			// Debug.Log($"DEBUG Setting doppler");
			// Debug.Log($"DEBUG audio spatializer: {AudioSettings.GetSpatializerPluginName()}"); // This is an empty string, so doppler effects using Unity's built-in settings are not available.
			// Manually handling doppler effects won't work either as there's no events for newly added audioSources and no way to check when the pitch is adjusted for other reasons.

			audioSources = FindObjectsOfType<AudioSource>();
			// Debug.Log("CameraTools.DEBUG audioSources: " + string.Join(", ", audioSources.Select(a => a.name)));
			originalAudioSourceSettings.Clear();

			for (int i = 0; i < audioSources.Length; i++)
			{
				if (excludeAudioSources.Contains(audioSources[i].name)) continue;

				if (!includeActiveVessel)
				{
					Part p = audioSources[i].GetComponentInParent<Part>();
					if (p && p.vessel.isActiveVessel) continue;
				}

				var part = audioSources[i].gameObject.GetComponentInParent<Part>();
				if (part != null)
				{
					if (part.vessel != null && !ignoreVesselTypesForAudio.Contains(part.vessel.vesselType))
					{
						CTPartAudioController pa = audioSources[i].gameObject.GetComponent<CTPartAudioController>();
						if (pa == null) pa = audioSources[i].gameObject.AddComponent<CTPartAudioController>();
						pa.audioSource = audioSources[i];
						pa.StoreOriginalSettings();
						pa.ApplyEffects();
						// if (DEBUG && audioSources[i].isPlaying) Debug.Log($"DEBUG adding part audio controller for {part} on {part.vessel.vesselName} for audiosource {i} ({audioSources[i].name}) with priority: {audioSources[i].priority}, doppler level {audioSources[i].dopplerLevel}, rollOff: {audioSources[i].rolloffMode}, spatialize: {audioSources[i].spatialize}, spatial blend: {audioSources[i].spatialBlend}, min/max dist:{audioSources[i].minDistance}/{audioSources[i].maxDistance}, clip: {audioSources[i].clip?.name}, output group: {audioSources[i].outputAudioMixerGroup}");
					}
				}
				else // Set/reset part audio separately from others.
				{
					originalAudioSourceSettings.Add((i, audioSources[i].dopplerLevel, audioSources[i].velocityUpdateMode, audioSources[i].bypassEffects, audioSources[i].spatialize, audioSources[i].spatialBlend));
					audioSources[i].dopplerLevel = 1;
					audioSources[i].velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
					audioSources[i].bypassEffects = false;
					audioSources[i].spatialize = true;
					audioSources[i].spatialBlend = 1;
				}
			}

			hasSetDoppler = true;
		}

		void ResetDoppler()
		{
			if (!hasSetDoppler)
			{
				return;
			}

			foreach (var (index, dopplerLevel, velocityUpdateMode, bypassEffects, spatialize, spatialBlend) in originalAudioSourceSettings) // Set/reset part audio separately from others.
			{
				if (audioSources[index] == null) continue;
				audioSources[index].dopplerLevel = dopplerLevel;
				audioSources[index].velocityUpdateMode = velocityUpdateMode;
				audioSources[index].bypassEffects = bypassEffects;
				audioSources[index].spatialBlend = spatialBlend;
				audioSources[index].spatialize = spatialize;
			}
			for (int i = 0; i < audioSources.Length; i++)
			{
				if (audioSources[i] == null || excludeAudioSources.Contains(audioSources[i].name)) continue;
				CTPartAudioController pa = audioSources[i].gameObject.GetComponent<CTPartAudioController>();
				if (pa == null) continue;
				pa.RestoreOriginalSettings();
			}

			hasSetDoppler = false;
		}
		#endregion

		#region Revert/Reset
		void SwitchToVessel(Vessel v)
		{
			if (v == null)
			{
				RevertCamera();
				return;
			}
			if (DEBUG)
			{
				message = "Switching to vessel " + v.vesselName;
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			vessel = v;
			// Switch to a usable camera mode if necessary.
			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
			{
				CameraManager.Instance.SetCameraFlight();
			}
			cockpitView = false;
			cockpits.Clear();
			engines.Clear();

			if (bdArmory.hasBDA)
			{
				bdArmory.CheckForBDAI(v);
				bdArmory.CheckForBDWM(v);
				if (!bdArmory.hasBDAI) bdArmory.CheckForBDMissile(FlightGlobals.ActiveVessel);
				bdArmory.UpdateAIDogfightTarget();
			}
			if (cameraToolActive)
			{
				if (randomMode)
				{
					var lowAlt = Math.Max(30d, -5d * vessel.verticalSpeed * Mathf.Max(0, Vector3.Dot(vessel.srf_vel_direction, -cameraUp))); // 30m or up to 5s to impact (depending on angle), whichever is higher.
					var stationarySurfaceVessel = (vessel.Landed && vessel.Speed() < 1) || (vessel.Splashed && vessel.Speed() < 5); // Land or water vessel that isn't moving much.
					if (stationarySurfaceVessel || (bdArmory.hasPilotAI && vessel.radarAltitude < lowAlt))
					{
						StartStationaryCamera();
					}
					else if (bdArmory.hasBDA && bdArmory.isBDMissile)
					{
						dogfightTarget = null;
						StartDogfightCamera(); // Use dogfight chase mode for BDA missiles.
					}
					else
					{
						ChooseRandomMode();
						// Actual switching is delayed until the LateUpdate to avoid a flicker.
					}
				}

				engines = vessel.FindPartModulesImplementing<ModuleEngines>();
				vesselSwitched = true;
			}
		}

		void ChooseRandomMode()
		{
			cockpits.Clear();
			var rand = rng.Next(100);
			if (rand < randomModeDogfightChance)
			{
				toolMode = ToolModes.DogfightCamera;
			}
			else if (rand < randomModeDogfightChance + randomModeIVAChance)
			{
				toolMode = ToolModes.DogfightCamera;
				cockpits = vessel.FindPartModulesImplementing<ModuleCommand>();
				if (cockpits.Any(c => c.part.protoModuleCrew.Count > 0))
				{ cockpitView = true; }
			}
			else if (rand < randomModeDogfightChance + randomModeIVAChance + randomModeStationaryChance)
			{
				toolMode = ToolModes.StationaryCamera;
			}
			else
			{
				toolMode = ToolModes.Pathing;
			}
		}

		public void RevertCamera()
		{
			if (!(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Flight || CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)) // Don't revert if not in Flight or IVA mode, it's already been deactivated, but the flight camera isn't available to be reconfigured.
			{
				revertWhenInFlightMode = true;
				activateWhenInFlightMode = false;
				return;
			}
			revertWhenInFlightMode = false;
			if (DEBUG)
			{
				message = "Reverting camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) // If we were in IVA mode, go back to Flight mode and pretend we were active.
			{
				CameraManager.Instance.SetCameraFlight();
				cameraToolActive = true;
			}

			if (cameraToolActive)
			{
				presetOffset = flightCamera.transform.position;
				if (camTarget == null && saveRotation)
				{
					savedRotation = flightCamera.transform.rotation;
					hasSavedRotation = true;
				}
				else
				{
					hasSavedRotation = false;
				}
			}
			hasDied = false;
			if (FlightGlobals.ActiveVessel != null && HighLogic.LoadedScene == GameScenes.FLIGHT && flightCamera.vesselTarget != FlightGlobals.ActiveVessel)
			{
				flightCamera.SetTarget(FlightGlobals.ActiveVessel.transform, FlightCamera.TargetMode.Vessel);
			}
			if (cameraToolActive)
			{
				flightCamera.transform.parent = origParent;
				if (origParent != null) // Restore the camera to the original local offsets from the original gameObject.
				{
					flightCamera.transform.localPosition = origLocalPosition;
					flightCamera.transform.localRotation = origLocalRotation;
					flightCamera.SetDistanceImmediate(origDistance);
				}
				else // Otherwise, restore the camera to the original absolute position and rotation as the original gameObject no longer exists (if it even existed to begin with).
				{
					flightCamera.transform.position = origPosition;
					flightCamera.transform.rotation = origRotation;
				}
				flightCamera.mode = origMode; // Restore the camera mode. Note: using flightCamera.setModeImmediate(origMode); causes the annoying camera mode change messages to appear, simply setting the value doesn't do this and seems to work fine.
				flightCamera.SetFoV(origFov);
				currentFOV = origFov;
				cameraParentWasStolen = false;
				dogfightLastTarget = false;
			}
			if (HighLogic.LoadedSceneIsFlight)
				flightCamera.mainCamera.nearClipPlane = origNearClip;
			else
				Camera.main.nearClipPlane = origNearClip;

			flightCamera.ActivateUpdate();

			cameraToolActive = false;

			StopPlayingPath();

			ResetDoppler();

			try
			{
				if (OnResetCTools != null)
				{ OnResetCTools(); }
			}
			catch (Exception e)
			{ Debug.Log("[CameraTools]: Caught exception resetting CameraTools " + e.ToString()); }

			// Reset the parameters we set in other mods so as not to mess with them while we're not active.
			timeControl.SetTimeControlCameraZoomFix(true);
			betterTimeWarp.SetBetterTimeWarpScaleCameraSpeed(true);
		}

		void SaveOriginalCamera()
		{
			origPosition = flightCamera.transform.position;
			origRotation = flightCamera.transform.rotation;
			origLocalPosition = flightCamera.transform.localPosition;
			origLocalRotation = flightCamera.transform.localRotation;
			origParent = flightCamera.transform.parent;
			origNearClip = HighLogic.LoadedSceneIsFlight ? flightCamera.mainCamera.nearClipPlane : Camera.main.nearClipPlane;
			origDistance = flightCamera.Distance;
			origMode = flightCamera.mode;
			origFov = flightCamera.FieldOfView;
		}

		void PostDeathRevert()
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}

		void PostDeathRevert(GameScenes f)
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}

		void PostDeathRevert(Vessel v)
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}
		#endregion

		#region GUI
		//GUI
		void OnGUI()
		{
			if (guiEnabled && gameUIToggle && HighLogic.LoadedSceneIsFlight)
			{
				if (inputFieldStyle == null) SetupInputFieldStyle();
				windowRect = GUI.Window(320, windowRect, GuiWindow, "");

				if (showKeyframeEditor)
				{
					KeyframeEditorWindow();
				}
				if (showPathSelectorWindow)
				{
					PathSelectorWindow();
				}
			}
			if (DEBUG)
			{
				if (debugMessages.Count > 0)
				{
					var now = Time.time;
					debugMessages = debugMessages.Where(m => now - m.Item1 < 5f).ToList();
					if (debugMessages.Count > 0)
					{
						var messages = string.Join("\n", debugMessages.Select(m => m.Item1.ToString("0.000") + " " + m.Item2));
						GUI.Label(cShadowRect, messages, cShadowStyle);
						GUI.Label(cDebugRect, messages, cStyle);
					}
				}
			}
			if (DEBUG2)
			{
				if (debug2Messages.Count > 0)
				{
					GUI.Label(new Rect(Screen.width - 750, 100, 700, Screen.height / 2), string.Join("\n", debug2Messages.Select(m => m.Item1.ToString("0.000") + " " + m.Item2)));
				}
			}
		}

		Rect LabelRect(float line)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, contentWidth, entryHeight); }
		Rect HalfRect(float line, int pos = 0)
		{ return new Rect(leftIndent + pos * contentWidth / 2f, contentTop + line * entryHeight, contentWidth / 2, entryHeight); }
		Rect LeftRect(float line)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, windowWidth / 2f + leftIndent * 2f, entryHeight); }
		Rect RightRect(float line)
		{ return new Rect(windowWidth / 2f + 3f * leftIndent, contentTop + line * entryHeight, contentWidth / 2f - 3f * leftIndent, entryHeight); }
		Rect QuarterRect(float line, int quarter)
		{ return new Rect(leftIndent + quarter * contentWidth / 4, contentTop + line * entryHeight, contentWidth / 4, entryHeight); }
		Rect ThinRect(float line)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, contentWidth, entryHeight - 2); }
		Rect ThinHalfRect(float line, int pos = 0)
		{ return new Rect(leftIndent + pos * (contentWidth / 2f + 2f), contentTop + line * entryHeight, contentWidth / 2 - 2, entryHeight - 2); }
		Rect SliderLabelLeft(float line, float indent)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, indent, entryHeight); }
		Rect SliderLabelRight(float line, float widthAdjust = 0)
		{ return new Rect(leftIndent + contentWidth - 25f - widthAdjust, contentTop + line * entryHeight, 25f + widthAdjust, entryHeight); }
		Rect SliderRect(float line, float indent, float widthAdjust = 0)
		{ return new Rect(leftIndent + indent, contentTop + line * entryHeight + 6f, contentWidth - indent - 30f + widthAdjust, entryHeight); }
		Rect RightSliderRect(float line)
		{ return new Rect(windowWidth / 2f + 3f * leftIndent, contentTop + line * entryHeight + 6f, contentWidth / 2f - 3f * leftIndent, entryHeight); }
		void SetupInputFieldStyle()
		{
			inputFieldStyle = new GUIStyle(GUI.skin.textField);
			inputFieldStyle.alignment = TextAnchor.UpperRight;
		}
		void GuiWindow(int windowID)
		{
			GUI.DragWindow(new Rect(0, 0, windowWidth, draggableHeight));

			GUI.Label(new Rect(0, contentTop, windowWidth, 40), "Camera Tools", titleStyle);
			GUI.Label(new Rect(windowWidth / 2f, contentTop + 35f, windowWidth / 2f - leftIndent - entryHeight, entryHeight), $"Version: {Version}", watermarkStyle);
			if (GUI.Toggle(new Rect(windowWidth - leftIndent - 14f, contentTop + 31f, 20f, 20f), cameraToolActive, "") != cameraToolActive)
			{
				if (cameraToolActive)
				{
					autoEnableOverriden = true;
					RevertCamera();
				}
				else
				{
					autoEnableOverriden = false;
					if (randomMode)
					{
						ChooseRandomMode();
					}
					cameraActivate();
				}
			}

			float line = 1.75f;
			float parseResult;

			//tool mode switcher
			GUI.Label(LabelRect(++line), "Tool: " + toolMode.ToString(), leftLabelBold);
			if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), 25, entryHeight - 2), "<"))
			{
				CycleToolMode(false);
				if (cameraToolActive) cameraActivate();
			}
			if (GUI.Button(new Rect(leftIndent + 25 + 4, contentTop + (line * entryHeight), 25, entryHeight - 2), ">"))
			{
				CycleToolMode(true);
				if (cameraToolActive) cameraActivate();
			}
			if (GUI.Button(new Rect(windowWidth - leftIndent - 25, contentTop + (line * entryHeight), 25, entryHeight - 2), "#", textInput ? GUI.skin.box : GUI.skin.button))
			{
				textInput = !textInput;
				if (!textInput) // Set the fields to their currently showing values.
				{
					foreach (var field in inputFields.Keys)
					{
						inputFields[field].tryParseValueNow();
						var fieldInfo = typeof(CamTools).GetField(field);
						if (fieldInfo != null) { fieldInfo.SetValue(this, inputFields[field].currentValue); }
						else
						{
							var propInfo = typeof(CamTools).GetProperty(field);
							propInfo.SetValue(this, inputFields[field].currentValue);
						}
					}
					if (currentPath != null)
					{
						currentPath.secondarySmoothing = pathingSecondarySmoothing;
						currentPath.timeScale = pathingTimeScale;
					}
					freeMoveSpeedRaw = Mathf.Log10(freeMoveSpeed);
					zoomSpeedRaw = Mathf.Log10(keyZoomSpeed);
				}
				else // Set the input fields to their current values.
				{
					if (currentPath != null)
					{
						pathingSecondarySmoothing = currentPath.secondarySmoothing;
						pathingTimeScale = currentPath.timeScale;
					}
					foreach (var field in inputFields.Keys)
					{
						var fieldInfo = typeof(CamTools).GetField(field);
						if (fieldInfo != null) { inputFields[field].currentValue = (float)fieldInfo.GetValue(this); }
						else
						{
							var propInfo = typeof(CamTools).GetProperty(field);
							inputFields[field].currentValue = (float)propInfo.GetValue(this);
						}
					}
					if (DEBUG && fmMode == fmModeTypes.Speed) DebugLog("Disabling speed free move mode due to switching to numeric inputs.");
					fmMode = fmModeTypes.Position; // Disable speed free move mode when using numeric inputs.
				}
				bdArmory.ToggleInputFields(textInput);
			}
			line++;

			useAudioEffects = GUI.Toggle(LabelRect(++line), useAudioEffects, "Use Audio Effects");
			if (bdArmory.hasBDA) bdArmory.autoEnableForBDA = GUI.Toggle(LabelRect(++line), bdArmory.autoEnableForBDA, "Auto-Enable for BDArmory");

			line++;
			if (autoFOV && toolMode != ToolModes.Pathing)
			{
				GUI.Label(LeftRect(++line), "Autozoom Margin: ");
				if (!textInput)
				{
					autoZoomMargin = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + ((++line) * entryHeight), contentWidth - 45, entryHeight), autoZoomMargin, 0, autoZoomMarginMax);
					if (!enableKeypad) autoZoomMargin = Mathf.RoundToInt(autoZoomMargin * 2f) / 2f;
					GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.15f) * entryHeight), 40, entryHeight), autoZoomMargin.ToString("G4"), leftLabel);
				}
				else
				{
					inputFields["autoZoomMargin"].tryParseValue(GUI.TextField(RightRect(line), inputFields["autoZoomMargin"].possibleValue, 8, inputFieldStyle));
					autoZoomMargin = inputFields["autoZoomMargin"].currentValue;
				}
			}
			else
			{
				GUI.Label(LeftRect(++line), "Zoom:", leftLabel);
				if (!textInput)
				{
					zoomExp = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + ((++line) * entryHeight), contentWidth - 45, entryHeight), zoomExp, 1, zoomMaxExp);
					GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.15f) * entryHeight), 40, entryHeight), zoomFactor.ToString("G5") + "x", leftLabel);
				}
				else
				{
					inputFields["zoomFactor"].tryParseValue(GUI.TextField(RightRect(line), inputFields["zoomFactor"].possibleValue, 8, inputFieldStyle));
					zoomExp = Mathf.Log(inputFields["zoomFactor"].currentValue) + 1f;
				}
			}
			if (toolMode != ToolModes.Pathing)
			{
				autoFOV = GUI.Toggle(LabelRect(++line), autoFOV, "Auto Zoom");
			}

			++line;
			GUI.Label(LeftRect(++line), "Camera Shake:");
			if (!textInput)
			{
				shakeMultiplier = Mathf.Round(GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth - 45, entryHeight), shakeMultiplier, 0f, 10f) * 10f) / 10f;
				GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.25f) * entryHeight), 40, entryHeight), shakeMultiplier.ToString("G3") + "x");
			}
			else
			{
				inputFields["shakeMultiplier"].tryParseValue(GUI.TextField(RightRect(line), inputFields["shakeMultiplier"].possibleValue, 8, inputFieldStyle));
				shakeMultiplier = inputFields["shakeMultiplier"].currentValue;
			}
			++line;

			//Stationary camera GUI
			if (toolMode == ToolModes.StationaryCamera)
			{
				GUI.Label(LeftRect(++line), "Max Relative Vel.: ", leftLabel);
				inputFields["maxRelV"].tryParseValue(GUI.TextField(RightRect(line), inputFields["maxRelV"].possibleValue, 12, inputFieldStyle));
				maxRelV = inputFields["maxRelV"].currentValue;
				signedMaxRelVSqr = Mathf.Abs(maxRelV) * maxRelV;

				maintainInitialVelocity = GUI.Toggle(LeftRect(++line), maintainInitialVelocity, "Maintain Vel.");
				if (maintainInitialVelocity) useOrbital = GUI.Toggle(RightRect(line), useOrbital, "Use Orbital");

				// GUI.Label(LeftRect(++line), $"time offset: {δt}", leftLabel);
				// δt = Mathf.Round(GUI.HorizontalSlider(RightRect(line), δt, -2f, 2f) * 4f) / 4f;

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Camera Position:", leftLabel);
				string posButtonText = "Set Position w/ Click";
				if (setPresetOffset) posButtonText = "Clear Position";
				if (waitingForPosition) posButtonText = "Waiting...";
				if (FlightGlobals.ActiveVessel != null && GUI.Button(ThinRect(++line), posButtonText))
				{
					if (setPresetOffset)
					{
						setPresetOffset = false;
					}
					else
					{
						waitingForPosition = true;
						mouseUp = false;
					}
				}
				autoFlybyPosition = GUI.Toggle(LabelRect(++line), autoFlybyPosition, "Auto Flyby Position");
				autoLandingPosition = GUI.Toggle(LabelRect(++line), autoLandingPosition, "Auto Landing Position"); ;
				if (autoFlybyPosition || autoLandingPosition) { manualOffset = false; }
				manualOffset = GUI.Toggle(LabelRect(++line), manualOffset, "Manual Flyby Position");
				Color origGuiColor = GUI.color;
				if (manualOffset)
				{ autoFlybyPosition = false; autoLandingPosition = false; }
				else if (!autoLandingPosition)
				{ GUI.color = new Color(0.5f, 0.5f, 0.5f, origGuiColor.a); }

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Fwd:", leftLabel);
				float textFieldWidth = 42;
				Rect fwdFieldRect = new Rect(leftIndent + contentWidth - textFieldWidth - (3 * incrButtonWidth), contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetForward = GUI.TextField(fwdFieldRect, guiOffsetForward.ToString());
				if (float.TryParse(guiOffsetForward, out parseResult))
				{
					manualOffsetForward = parseResult;
				}
				DrawIncrementButtons(fwdFieldRect, ref manualOffsetForward);
				guiOffsetForward = manualOffsetForward.ToString();

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Right:", leftLabel);
				Rect rightFieldRect = new Rect(fwdFieldRect.x, contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetRight = GUI.TextField(rightFieldRect, guiOffsetRight);
				if (float.TryParse(guiOffsetRight, out parseResult))
				{
					manualOffsetRight = parseResult;
				}
				DrawIncrementButtons(rightFieldRect, ref manualOffsetRight);
				guiOffsetRight = manualOffsetRight.ToString();

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Up:", leftLabel);
				Rect upFieldRect = new Rect(fwdFieldRect.x, contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetUp = GUI.TextField(upFieldRect, guiOffsetUp);
				if (float.TryParse(guiOffsetUp, out parseResult))
				{ manualOffsetUp = parseResult; }
				DrawIncrementButtons(upFieldRect, ref manualOffsetUp);
				guiOffsetUp = manualOffsetUp.ToString();
				GUI.color = origGuiColor;
				line++;

				string targetText = "None";
				if (camTarget != null) targetText = camTarget.gameObject.name;
				GUI.Label(LabelRect(++line), "Camera Target: " + targetText, leftLabel);
				string tgtButtonText = "Set Target w/ Click";
				if (waitingForTarget) tgtButtonText = "waiting...";
				if (GUI.Button(ThinRect(++line), tgtButtonText))
				{
					waitingForTarget = true;
					mouseUp = false;
				}
				if (GUI.Button(ThinHalfRect(++line, 0), "Target Self"))
				{
					camTarget = FlightGlobals.ActiveVessel.GetReferenceTransformPart();
					hasTarget = true;
				}
				if (GUI.Button(ThinHalfRect(line, 1), "Clear Target"))
				{
					camTarget = null;
					hasTarget = false;
				}
				targetCoM = GUI.Toggle(ThinRect(++line), targetCoM, "Vessel Center of Mass");
				if (camTarget == null) saveRotation = GUI.Toggle(ThinRect(++line), saveRotation, "Save Rotation");
				if (!saveRotation) hasSavedRotation = false;
			}
			else if (toolMode == ToolModes.DogfightCamera)
			{
				GUI.Label(ThinRect(++line), "Secondary Target:");
				string tVesselLabel;
				if (MouseAimFlight.IsMouseAimActive())
				{ tVesselLabel = "MouseAimFlight"; }
				else if (showingVesselList)
				{ tVesselLabel = "Clear"; }
				else if (bdArmory.hasBDA && bdArmory.useCentroid)
				{ tVesselLabel = "Centroid"; }
				else if (dogfightTarget)
				{ tVesselLabel = dogfightTarget.vesselName; }
				else
				{ tVesselLabel = "None"; }
				if (GUI.Button(LabelRect(++line), tVesselLabel))
				{
					if (showingVesselList)
					{
						showingVesselList = false;
						dogfightTarget = null;
					}
					else
					{
						UpdateLoadedVessels();
						showingVesselList = true;
					}
				}
				if (showingVesselList)
				{
					if (MouseAimFlight.IsMouseAimActive()) showingVesselList = false;
					foreach (var v in loadedVessels)
					{
						if (!v || !v.loaded) continue;
						if (GUI.Button(new Rect(leftIndent + 10f, contentTop + (++line * entryHeight), contentWidth - 10f, entryHeight), v.vesselName))
						{
							dogfightTarget = v;
							showingVesselList = false;
						}
					}
				}
				if (bdArmory.hasBDA)
				{
					if (bdArmory.hasBDAI)
					{
						if (bdArmory.useBDAutoTarget != (bdArmory.useBDAutoTarget = GUI.Toggle(ThinRect(++line), bdArmory.useBDAutoTarget, "BDA AI Auto Target")) && bdArmory.useBDAutoTarget)
						{ bdArmory.useCentroid = false; }
						GUI.Label(SliderLabelLeft(++line, 110f), "Minimum Interval:");
						if (!textInput)
						{
							bdArmory.AItargetMinimumUpdateInterval = MathUtils.RoundToUnit(GUI.HorizontalSlider(SliderRect(line, 110f), bdArmory.AItargetMinimumUpdateInterval, 0.5f, 5f), 0.5f);
							GUI.Label(SliderLabelRight(line), $"{bdArmory.AItargetMinimumUpdateInterval:F1}s");
						}
						else
						{
							bdArmory.inputFields["AItargetMinimumUpdateInterval"].tryParseValue(GUI.TextField(RightRect(line), bdArmory.inputFields["AItargetMinimumUpdateInterval"].possibleValue, 8, inputFieldStyle));
							bdArmory.AItargetMinimumUpdateInterval = bdArmory.inputFields["AItargetMinimumUpdateInterval"].currentValue;
						}
						bdArmory.autoTargetIncomingMissiles = GUI.Toggle(ThinRect(++line), bdArmory.autoTargetIncomingMissiles, "Target Incoming Missiles");
					}
					if (bdArmory.useCentroid != (bdArmory.useCentroid = GUI.Toggle(ThinRect(++line), bdArmory.useCentroid, "Target Dogfight Centroid")) && bdArmory.useCentroid)
					{ bdArmory.useBDAutoTarget = false; }
				}

				++line;

				GUI.Label(SliderLabelLeft(++line, 55f), $"Distance:");
				if (!textInput)
				{
					dogfightDistance = GUI.HorizontalSlider(SliderRect(++line, 0f, -8f), dogfightDistance, 1f, dogfightMaxDistance);
					if (!enableKeypad) dogfightDistance = MathUtils.RoundToUnit(dogfightDistance, 1f);
					GUI.Label(SliderLabelRight(line, 8f), $"{dogfightDistance:G4}m");
				}
				else
				{
					inputFields["dogfightDistance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["dogfightDistance"].possibleValue, 8, inputFieldStyle));
					dogfightDistance = inputFields["dogfightDistance"].currentValue;
				}

				GUI.Label(LeftRect(++line), "Offset:");
				if (!textInput)
				{
					GUI.Label(SliderLabelLeft(++line, 15f), "X: ");
					dogfightOffsetX = GUI.HorizontalSlider(SliderRect(line, 15f), dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
					if (!enableKeypad) dogfightOffsetX = MathUtils.RoundToUnit(dogfightOffsetX, 1f);
					GUI.Label(SliderLabelRight(line), $"{dogfightOffsetX:G3}m");
					GUI.Label(SliderLabelLeft(++line, 15f), "Y: ");
					dogfightOffsetY = GUI.HorizontalSlider(SliderRect(line, 15f), dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
					if (!enableKeypad) dogfightOffsetY = MathUtils.RoundToUnit(dogfightOffsetY, 1f);
					GUI.Label(SliderLabelRight(line), $"{dogfightOffsetY:G3}m");
					line += 0.5f;

					GUI.Label(SliderLabelLeft(++line, 30f), "Lerp: ");
					dogfightLerp = Mathf.RoundToInt(GUI.HorizontalSlider(SliderRect(line, 30f), dogfightLerp * 100f, 1f, 50f)) / 100f;
					GUI.Label(SliderLabelRight(line), $"{dogfightLerp:G3}");
					GUI.Label(SliderLabelLeft(++line, 30f), "Roll: ");
					dogfightRoll = Mathf.RoundToInt(GUI.HorizontalSlider(SliderRect(line, 30f), dogfightRoll * 20f, 0f, 20f)) / 20f;
					GUI.Label(SliderLabelRight(line), $"{dogfightRoll:G3}");
					line += 0.15f;
				}
				else
				{
					GUI.Label(QuarterRect(++line, 0), "X: ", rightLabel);
					inputFields["dogfightOffsetX"].tryParseValue(GUI.TextField(QuarterRect(line, 1), inputFields["dogfightOffsetX"].possibleValue, 8, inputFieldStyle));
					dogfightOffsetX = inputFields["dogfightOffsetX"].currentValue;
					GUI.Label(QuarterRect(line, 2), "Y: ", rightLabel);
					inputFields["dogfightOffsetY"].tryParseValue(GUI.TextField(QuarterRect(line, 3), inputFields["dogfightOffsetY"].possibleValue, 8, inputFieldStyle));
					dogfightOffsetY = inputFields["dogfightOffsetY"].currentValue;
					GUI.Label(QuarterRect(++line, 0), "Lerp: ", rightLabel);
					inputFields["dogfightLerp"].tryParseValue(GUI.TextField(QuarterRect(line, 1), inputFields["dogfightLerp"].possibleValue, 8, inputFieldStyle));
					dogfightLerp = inputFields["dogfightLerp"].currentValue;
					GUI.Label(QuarterRect(line, 2), "Roll: ", rightLabel);
					inputFields["dogfightRoll"].tryParseValue(GUI.TextField(QuarterRect(line, 3), inputFields["dogfightRoll"].possibleValue, 8, inputFieldStyle));
					dogfightRoll = inputFields["dogfightRoll"].currentValue;
				}

				GUI.Label(SliderLabelLeft(++line, 95f), $"Free-Look Thr.:");
				if (!textInput)
				{
					freeLookThresholdSqr = MathUtils.RoundToUnit(GUI.HorizontalSlider(SliderRect(line, 95f), freeLookThresholdSqr, 0f, 1f), 0.1f);
					GUI.Label(SliderLabelRight(line), $"{freeLookThresholdSqr:G2}");
				}
				else
				{
					inputFields["freeLookThresholdSqr"].tryParseValue(GUI.TextField(RightRect(line), inputFields["freeLookThresholdSqr"].possibleValue, 8, inputFieldStyle));
					freeLookThresholdSqr = inputFields["freeLookThresholdSqr"].currentValue;
				}

				GUI.Label(SliderLabelLeft(++line, 95f), $"Camera Inertia:");
				if (!textInput)
				{
					dogfightInertialFactor = MathUtils.RoundToUnit(GUI.HorizontalSlider(SliderRect(line, 95f), dogfightInertialFactor, 0f, 0.1f), 0.01f);
					GUI.Label(SliderLabelRight(line), $"{dogfightInertialFactor:G3}");
				}
				else
				{
					inputFields["dogfightInertialFactor"].tryParseValue(GUI.TextField(RightRect(line), inputFields["dogfightInertialFactor"].possibleValue, 8, inputFieldStyle));
					dogfightInertialFactor = inputFields["dogfightInertialFactor"].currentValue;
				}

				if (dogfightInertialChaseMode != (dogfightInertialChaseMode = GUI.Toggle(LabelRect(++line), dogfightInertialChaseMode, "Inertial Chase Mode")))
				{ StartDogfightCamera(); }
			}
			else if (toolMode == ToolModes.Pathing)
			{
				if (selectedPathIndex >= 0)
				{
					GUI.Label(LabelRect(++line), "Path:");
					currentPath.pathName = GUI.TextField(new Rect(leftIndent + 34, contentTop + (line * entryHeight), contentWidth - 34, entryHeight), currentPath.pathName);
				}
				else
				{ GUI.Label(LabelRect(++line), "Path: None"); }
				line += 0.25f;
				if (GUI.Button(LabelRect(++line), "Open Path"))
				{ TogglePathList(); }
				if (GUI.Button(HalfRect(++line, 0), "New Path"))
				{ CreateNewPath(); }
				if (GUI.Button(HalfRect(line, 1), "Delete Path"))
				{ DeletePath(selectedPathIndex); }
				line += 0.25f;

				if (selectedPathIndex >= 0)
				{
					if (!textInput)
					{
						GUI.Label(LabelRect(++line), "Secondary Smoothing: " + currentPath.secondarySmoothing.ToString("G2"));
						if (currentPath.secondarySmoothing != (currentPath.secondarySmoothing = Mathf.Round(GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight) + 4f, contentWidth, entryHeight), currentPath.secondarySmoothing, 0f, 1f) * 100f) / 100f))
						{ pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing); }
					}
					else
					{
						GUI.Label(LeftRect(++line), "Secondary Smoothing:");
						inputFields["pathingSecondarySmoothing"].tryParseValue(GUI.TextField(RightRect(line), inputFields["pathingSecondarySmoothing"].possibleValue, 8, inputFieldStyle));
						if (currentPath.secondarySmoothing != (currentPath.secondarySmoothing = inputFields["pathingSecondarySmoothing"].currentValue))
						{ pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing); }
					}
					if (!textInput)
					{
						GUI.Label(LabelRect(++line), "Path Timescale: " + currentPath.timeScale.ToString("G3"));
						currentPath.timeScale = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight) + 4f, contentWidth, entryHeight), currentPath.timeScale, 0.05f, 4f);
						currentPath.timeScale = Mathf.Round(currentPath.timeScale * 20f) / 20f;
					}
					else
					{
						GUI.Label(LeftRect(++line), "Path Timescale:");
						inputFields["pathingTimeScale"].tryParseValue(GUI.TextField(RightRect(line), inputFields["pathingTimeScale"].possibleValue, 8, inputFieldStyle));
						currentPath.timeScale = inputFields["pathingTimeScale"].currentValue;
					}
					if (GUI.Button(LabelRect(++line), useRealTime ? "Real-time" : "In-Game time"))
					{
						useRealTime = !useRealTime;
					}
					++line;
					float viewHeight = Mathf.Max(6f * entryHeight, currentPath.keyframeCount * entryHeight);
					Rect scrollRect = new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, 6 * entryHeight);
					GUI.Box(scrollRect, string.Empty);
					float viewContentWidth = contentWidth - (2f * leftIndent);
					keysScrollPos = GUI.BeginScrollView(scrollRect, keysScrollPos, new Rect(0f, 0f, viewContentWidth, viewHeight));
					if (currentPath.keyframeCount > 0)
					{
						Color origGuiColor = GUI.color;
						for (int i = 0; i < currentPath.keyframeCount; ++i)
						{
							if (i == currentKeyframeIndex)
							{
								GUI.color = Color.green;
							}
							else
							{
								GUI.color = origGuiColor;
							}
							string kLabel = "#" + i.ToString() + ": " + currentPath.GetKeyframe(i).time.ToString("G3") + "s";
							if (GUI.Button(new Rect(0f, (i * entryHeight), 3f * viewContentWidth / 4f, entryHeight), kLabel))
							{
								SelectKeyframe(i);
							}
							if (GUI.Button(new Rect((3f * contentWidth / 4f), (i * entryHeight), (viewContentWidth / 4f) - 20f, entryHeight), "X"))
							{
								DeleteKeyframe(i);
								break;
							}
						}
						GUI.color = origGuiColor;
					}
					GUI.EndScrollView();
					line += 5.25f;
					if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), 3f * contentWidth / 4f, entryHeight), "New Key"))
					{ CreateNewKeyframe(); }
				}
			}
			line += 0.25f;

			randomMode = GUI.Toggle(LabelRect(++line), randomMode, "Random Mode");
			if (randomMode)
			{
				float oldValue = randomModeDogfightChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Dogfight ({randomModeDogfightChance:F0}%)");
					randomModeDogfightChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6, contentWidth / 2f, entryHeight), randomModeDogfightChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Dogfight %: ");
					inputFields["randomModeDogfightChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeDogfightChance"].possibleValue, 8, inputFieldStyle));
					randomModeDogfightChance = inputFields["randomModeDogfightChance"].currentValue;
				}
				if (oldValue != randomModeDogfightChance)
				{
					var remainder = 100f - randomModeDogfightChance;
					var total = randomModeIVAChance + randomModeStationaryChance + randomModePathingChance;
					if (total > 0f)
					{
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeDogfightChance = 100f - randomModeIVAChance - randomModeStationaryChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModeIVAChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"IVA ({randomModeIVAChance:F0}%)");
					randomModeIVAChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), randomModeIVAChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "IVA %: ");
					inputFields["randomModeIVAChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeIVAChance"].possibleValue, 8, inputFieldStyle));
					randomModeIVAChance = inputFields["randomModeIVAChance"].currentValue;
				}
				if (oldValue != randomModeIVAChance)
				{
					var remainder = 100f - randomModeIVAChance;
					var total = randomModeDogfightChance + randomModeStationaryChance + randomModePathingChance;
					if (total > 0f)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeIVAChance = 100f - randomModeDogfightChance - randomModeStationaryChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModeStationaryChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Stationary ({randomModeStationaryChance:F0}%)");
					randomModeStationaryChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6, contentWidth / 2f, entryHeight), randomModeStationaryChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Stationary %: ");
					inputFields["randomModeStationaryChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeStationaryChance"].possibleValue, 8, inputFieldStyle));
					randomModeStationaryChance = inputFields["randomModeStationaryChance"].currentValue;
				}
				if (oldValue != randomModeStationaryChance)
				{
					var remainder = 100f - randomModeStationaryChance;
					var total = randomModeDogfightChance + randomModeIVAChance + randomModePathingChance;
					if (total > 0)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeStationaryChance = 100f - randomModeDogfightChance - randomModeIVAChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModePathingChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Pathing ({randomModePathingChance:F0}%)");
					randomModePathingChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), randomModePathingChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Pathing %: ");
					inputFields["randomModePathingChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModePathingChance"].possibleValue, 8, inputFieldStyle));
					randomModePathingChance = inputFields["randomModePathingChance"].currentValue;
				}
				if (oldValue != randomModePathingChance)
				{
					var remainder = 100f - randomModePathingChance;
					var total = randomModeDogfightChance + randomModeIVAChance + randomModeStationaryChance;
					if (total > 0)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
					}
					randomModePathingChance = 100f - randomModeDogfightChance - randomModeIVAChance - randomModeStationaryChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}
			}

			line += 0.25f;
			enableKeypad = GUI.Toggle(LabelRect(++line), enableKeypad, "Keypad Control");
			if (enableKeypad)
			{
				GUI.Label(SliderLabelLeft(++line, contentWidth / 2f - 30f), "Move Speed:");
				if (!textInput)
				{
					freeMoveSpeedRaw = Mathf.RoundToInt(GUI.HorizontalSlider(SliderRect(line, contentWidth / 2f - 30f), freeMoveSpeedRaw, freeMoveSpeedMinRaw, freeMoveSpeedMaxRaw) * 100f) / 100f;
					freeMoveSpeed = Mathf.Pow(10f, freeMoveSpeedRaw);
					GUI.Label(SliderLabelRight(line), freeMoveSpeed.ToString("G4"));
				}
				else
				{
					inputFields["freeMoveSpeed"].tryParseValue(GUI.TextField(RightRect(line), inputFields["freeMoveSpeed"].possibleValue, 8, inputFieldStyle));
					freeMoveSpeed = inputFields["freeMoveSpeed"].currentValue;
				}

				GUI.Label(SliderLabelLeft(++line, contentWidth / 2f - 30f), "Zoom Speed:");
				if (!textInput)
				{
					zoomSpeedRaw = Mathf.RoundToInt(GUI.HorizontalSlider(SliderRect(line, contentWidth / 2f - 30f), zoomSpeedRaw, zoomSpeedMinRaw, zoomSpeedMaxRaw) * 100f) / 100f;
					keyZoomSpeed = Mathf.Pow(10f, zoomSpeedRaw);
					GUI.Label(SliderLabelRight(line), keyZoomSpeed.ToString("G3"));
				}
				else
				{
					inputFields["keyZoomSpeed"].tryParseValue(GUI.TextField(RightRect(line), inputFields["keyZoomSpeed"].possibleValue, 8, inputFieldStyle));
					keyZoomSpeed = inputFields["keyZoomSpeed"].currentValue;
				}

				GUI.Label(SliderLabelLeft(++line, contentWidth / 2f - 30f), "Mode:");
				fmMode = (fmModeTypes)Mathf.RoundToInt(GUI.HorizontalSlider(SliderRect(line, contentWidth / 2f - 30f, -30f), (int)fmMode, 0, 1));
				GUI.Label(SliderLabelRight(line, 30f), fmMode.ToString());
			}
			line++;

			// Key bindings
			if (GUI.Button(LabelRect(++line), "Edit Keybindings"))
			{ editingKeybindings = !editingKeybindings; }
			if (editingKeybindings)
			{
				cameraKey = KeyBinding(cameraKey, "Activate", ++line);
				revertKey = KeyBinding(revertKey, "Revert", ++line);
				toggleMenu = KeyBinding(toggleMenu, "Menu", ++line);
				fmUpKey = KeyBinding(fmUpKey, "Up", ++line);
				fmDownKey = KeyBinding(fmDownKey, "Down", ++line);
				fmForwardKey = KeyBinding(fmForwardKey, "Forward", ++line);
				fmBackKey = KeyBinding(fmBackKey, "Back", ++line);
				fmLeftKey = KeyBinding(fmLeftKey, "Left", ++line);
				fmRightKey = KeyBinding(fmRightKey, "Right", ++line);
				fmZoomInKey = KeyBinding(fmZoomInKey, "Zoom In", ++line);
				fmZoomOutKey = KeyBinding(fmZoomOutKey, "Zoom Out", ++line);
				fmMovementModifier = KeyBinding(fmMovementModifier, "Modifier", ++line);
				fmModeToggleKey = KeyBinding(fmModeToggleKey, "FM Mode", ++line);
				resetRollKey = KeyBinding(resetRollKey, "Reset Roll", ++line);
			}

			Rect saveRect = HalfRect(++line, 0);
			if (GUI.Button(saveRect, "Save"))
			{ Save(); }

			Rect loadRect = HalfRect(line, 1);
			if (GUI.Button(loadRect, "Reload"))
			{
				if (isPlayingPath) StopPlayingPath();
				Load();
			}

			float timeSinceLastSaved = Time.unscaledTime - lastSavedTime;
			if (timeSinceLastSaved < 1)
			{
				++line;
				GUI.Label(LabelRect(++line), timeSinceLastSaved < 0.5 ? "Saving..." : "Saved.", centerLabel);
			}

			//fix length
			windowHeight = contentTop + (line * entryHeight) + entryHeight + entryHeight;
			windowRect.height = windowHeight;// = new Rect(windowRect.x, windowRect.y, windowWidth, windowHeight);
		}

		string KeyBinding(string current, string label, float line)
		{
			GUI.Label(new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth, entryHeight), $"{label}: ", leftLabel);
			GUI.Label(new Rect(leftIndent + 70, contentTop + (line * entryHeight), 50, entryHeight), current, leftLabel);
			if (!isRecordingInput || label != currentlyBinding)
			{
				if (GUI.Button(new Rect(leftIndent + 130, contentTop + (line * entryHeight), 95, entryHeight), "Bind Key"))
				{
					mouseUp = false;
					isRecordingInput = true;
					currentlyBinding = label;
				}
			}
			else if (mouseUp)
			{
				GUI.Label(new Rect(leftIndent + 140, contentTop + (line * entryHeight), 85, entryHeight), "Press a Key", leftLabel);

				string inputString = CCInputUtils.GetInputString();
				if (inputString.Length > 0)
				{
					isRecordingInput = false;
					boundThisFrame = true;
					currentlyBinding = "";
					if (inputString != "escape") // Allow escape key to cancel keybinding.
					{ return inputString; }
				}
			}

			return current;
		}

		void KeyframeEditorWindow()
		{
			float width = 300f;
			float gap = 5;
			float lineHeight = 25f;
			float line = 0f;
			Rect kWindowRect = new Rect(windowRect.x - width, windowRect.y + 365, width, keyframeEditorWindowHeight);
			GUI.Box(kWindowRect, string.Empty);
			GUI.BeginGroup(kWindowRect);
			GUI.Label(new Rect(gap, gap, 100, lineHeight - gap), "Keyframe #" + currentKeyframeIndex);
			if (GUI.Button(new Rect(100 + gap, gap, 200 - 2 * gap, lineHeight), "Revert Pos"))
			{
				ViewKeyframe(currentKeyframeIndex);
			}
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 80, lineHeight - gap), "Time: ");
			currKeyTimeString = GUI.TextField(new Rect(100 + gap, gap + line * lineHeight, 200 - 2 * gap, lineHeight - gap), currKeyTimeString, 16);
			float parsed;
			if (float.TryParse(currKeyTimeString, out parsed))
			{
				currentKeyframeTime = parsed;
			}
			if (currentKeyframeIndex > 1)
			{
				if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Maintain Speed"))
				{
					CameraKeyframe previousKeyframe = currentPath.GetKeyframe(currentKeyframeIndex - 1);
					CameraKeyframe previousPreviousKeyframe = currentPath.GetKeyframe(currentKeyframeIndex - 2);
					float previousKeyframeDistance = Vector3.Distance(previousKeyframe.position, previousPreviousKeyframe.position);
					float previousKeyframeDuration = previousKeyframe.time - previousPreviousKeyframe.time;
					float previousKeyframeSpeed = previousKeyframeDistance / previousKeyframeDuration;
					float currentKeyFrameDistance = Vector3.Distance(flightCamera.transform.localPosition, previousKeyframe.position);
					float adjustedDuration = currentKeyFrameDistance / previousKeyframeSpeed;
					float currentKeyframeDuration = currentKeyframeTime - previousKeyframe.time;
					currentKeyframeTime += adjustedDuration - currentKeyframeDuration;
					currKeyTimeString = currentKeyframeTime.ToString();
				}
			}
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 100, lineHeight - gap), $"Pos: {currentKeyframePositionInterpolationType}");
			currentKeyframePositionInterpolationType = (PositionInterpolationType)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(100 + 2 * gap, gap + line * lineHeight, 200 - 3 * gap, lineHeight - gap), (float)currentKeyframePositionInterpolationType, 0, PositionInterpolationTypeMax));
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 100, lineHeight - gap), $"Rot: {currentKeyframeRotationInterpolationType}");
			currentKeyframeRotationInterpolationType = (RotationInterpolationType)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(100 + 2 * gap, gap + line * lineHeight, 200 - 3 * gap, lineHeight - gap), (float)currentKeyframeRotationInterpolationType, 0, RotationInterpolationTypeMax));
			bool applied = false;
			if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Apply"))
			{
				Debug.Log("[CameraTools]: Applying keyframe at time: " + currentKeyframeTime);
				currentPath.SetTransform(currentKeyframeIndex, flightCamera.transform, zoomExp, currentKeyframeTime, currentKeyframePositionInterpolationType, currentKeyframeRotationInterpolationType);
				applied = true;
			}
			if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Cancel"))
			{
				applied = true;
			}
			GUI.EndGroup();

			if (applied)
			{
				DeselectKeyframe();
			}
			keyframeEditorWindowHeight = 2 * gap + (++line * lineHeight);
		}

		bool showPathSelectorWindow = false;
		Vector2 pathSelectScrollPos;
		void PathSelectorWindow()
		{
			float width = 300;
			float height = 300;
			float indent = 5;
			float scrollRectSize = width - indent - indent;
			Rect pSelectRect = new Rect(windowRect.x - width, windowRect.y + 290, width, height);
			GUI.Box(pSelectRect, string.Empty);
			GUI.BeginGroup(pSelectRect);

			Rect scrollRect = new Rect(indent, indent, scrollRectSize, scrollRectSize);
			float scrollHeight = Mathf.Max(scrollRectSize, entryHeight * availablePaths.Count);
			Rect scrollViewRect = new Rect(0, 0, scrollRectSize - 20, scrollHeight);
			pathSelectScrollPos = GUI.BeginScrollView(scrollRect, pathSelectScrollPos, scrollViewRect);
			bool selected = false;
			for (int i = 0; i < availablePaths.Count; i++)
			{
				if (GUI.Button(new Rect(0, i * entryHeight, scrollRectSize - 90, entryHeight), availablePaths[i].pathName))
				{
					SelectPath(i);
					selected = true;
					if (cameraToolActive && currentPath.keyframeCount > 0) PlayPathingCam();
				}
				if (GUI.Button(new Rect(scrollRectSize - 80, i * entryHeight, 60, entryHeight), "Delete"))
				{
					DeletePath(i);
					break;
				}
			}

			GUI.EndScrollView();

			GUI.EndGroup();
			if (selected)
			{
				showPathSelectorWindow = false;
			}
		}

		void DrawIncrementButtons(Rect fieldRect, ref float val)
		{
			Rect incrButtonRect = new Rect(fieldRect.x - incrButtonWidth, fieldRect.y, incrButtonWidth, entryHeight);
			if (GUI.Button(incrButtonRect, "-"))
			{
				val -= 5;
			}

			incrButtonRect.x -= incrButtonWidth;

			if (GUI.Button(incrButtonRect, "--"))
			{
				val -= 50;
			}

			incrButtonRect.x = fieldRect.x + fieldRect.width;

			if (GUI.Button(incrButtonRect, "+"))
			{
				val += 5;
			}

			incrButtonRect.x += incrButtonWidth;

			if (GUI.Button(incrButtonRect, "++"))
			{
				val += 50;
			}
		}

		//AppLauncherSetup
		void AddToolbarButton()
		{
			if (!hasAddedButton)
			{
				Texture buttonTexture = GameDatabase.Instance.GetTexture("CameraTools/Textures/icon", false);
				ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
				CamTools.hasAddedButton = true;
			}

		}

		void ToggleGui()
		{
			if (guiEnabled)
				DisableGui();
			else
				EnableGui();
		}

		void EnableGui()
		{
			guiEnabled = true;
			// Debug.Log("[CameraTools]: Showing CamTools GUI");
		}

		void DisableGui()
		{
			guiEnabled = false;
			Save();
			// Debug.Log("[CameraTools]: Hiding CamTools GUI");
		}

		void Dummy()
		{ }

		void GameUIEnable()
		{
			gameUIToggle = true;
		}

		void GameUIDisable()
		{
			gameUIToggle = false;
		}

		void CycleToolMode(bool forward)
		{
			var length = System.Enum.GetValues(typeof(ToolModes)).Length;
			if (forward)
			{
				toolMode++;
				if ((int)toolMode == length) toolMode = 0;
			}
			else
			{
				toolMode--;
				if ((int)toolMode == -1) toolMode = (ToolModes)length - 1;
			}
			if (toolMode != ToolModes.Pathing)
			{ DeselectKeyframe(); }
		}
		#endregion

		#region Utils
		void CurrentVesselWillDestroy(Vessel v)
		{
			if (vessel == v && cameraToolActive)
			{
				hasDied = true;
				diedTime = Time.time;

				if (DEBUG)
				{
					message = "Activating death camera.";
					Debug.Log("[CameraTools]: " + message);
					DebugLog(message);
				}
				// Something borks the camera position/rotation when the target/parent is set to none/null. This fixes that.
				deathCamVelocity = (vessel.radarAltitude > 10d ? vessel.Velocity() : Vector3d.zero) / 2f; // Track the explosion a bit.
				SetDeathCam();
			}
		}

		Part GetPartFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 10000, 1 << 0))
			{
				Part p = hit.transform.GetComponentInParent<Part>();
				return p;
			}
			else return null;
		}

		Vector3 GetPosFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 15000, 557057))
			{
				return hit.point - (10 * ray.direction);
			}
			else return Vector3.zero;
		}

		void UpdateLoadedVessels()
		{
			if (loadedVessels == null)
			{
				loadedVessels = new List<Vessel>();
			}
			else
			{
				loadedVessels.Clear();
			}

			foreach (Vessel v in FlightGlobals.Vessels)
			{
				if (v == null || !v.loaded || v.packed) continue;
				if (v.vesselType == VesselType.Debris || v.isActiveVessel) continue; // Ignore debris and the active vessel.
				loadedVessels.Add(v);
			}
		}

		private string GetVersion()
		{
			try
			{
				Version = this.GetType().Assembly.GetName().Version.ToString(3);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[CameraTools]: Failed to get version string: {e.Message}");
			}
			return Version;
		}

		public static float GetRadarAltitudeAtPos(Vector3 position)
		{
			var geoCoords = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(position);
			var altitude = FlightGlobals.currentMainBody.GetAltitude(position);
			var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(geoCoords.x, geoCoords.y);
			return (float)(altitude - Math.Max(terrainAltitude, 0));
		}

		public float GetTime()
		{
			return useRealTime ? Time.unscaledTime : Time.time;
		}

		public void SetZoomImmediate(float zoom)
		{
			zoomExp = zoom;
			zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
			manualFOV = 60 / zoomFactor;
			currentFOV = manualFOV;
			flightCamera.SetFoV(currentFOV);
		}

		void SetCameraParent(Transform referenceTransform, bool resetToCoM = false)
		{
			if (DEBUG) Debug.Log($"[CameraTools]: Setting the camera parent to {referenceTransform.gameObject.name} from {flightCamera.gameObject.name}, reset-to-CoM: {resetToCoM}");
			cameraParent.transform.position = referenceTransform.position;
			cameraParent.transform.rotation = referenceTransform.rotation;
			flightCamera.SetTargetNone();
			flightCamera.transform.parent = cameraParent.transform;
			cameraParentWasStolen = false;
			flightCamera.DeactivateUpdate();
			if (resetToCoM)
			{
				cameraParent.transform.position = vessel.CoM; // Then adjust the flightCamera for the new parent.
				flightCamera.transform.localPosition = cameraParent.transform.InverseTransformPoint(referenceTransform.position);
				flightCamera.transform.localRotation = Quaternion.identity;
			}
		}

		void SetDeathCam()
		{
			flightCamera.SetTargetNone();
			flightCamera.transform.parent = deathCam.transform;
			cameraParentWasStolen = false;
			flightCamera.DeactivateUpdate();
			flightCamera.transform.localPosition = Vector3.zero;
			flightCamera.transform.localRotation = Quaternion.identity;
		}

		public static bool GameIsPaused
		{
			get { return PauseMenu.isOpen || Time.timeScale == 0; }
		}
		#endregion

		#region Load/Save
		void Save()
		{
			CTPersistantField.Save("CToolsSettings", typeof(CamTools), this);

			ConfigNode pathFileNode = ConfigNode.Load(CameraPath.pathSaveURL);

			if (pathFileNode == null)
				pathFileNode = new ConfigNode();

			if (!pathFileNode.HasNode("CAMERAPATHS"))
				pathFileNode.AddNode("CAMERAPATHS");

			ConfigNode pathsNode = pathFileNode.GetNode("CAMERAPATHS");
			pathsNode.RemoveNodes("CAMERAPATH");

			foreach (var path in availablePaths)
			{
				path.Save(pathsNode);
			}
			if (!Directory.GetParent(CameraPath.pathSaveURL).Exists)
			{ Directory.GetParent(CameraPath.pathSaveURL).Create(); }
			var success = pathFileNode.Save(CameraPath.pathSaveURL);
			if (success)
			{
				lastSavedTime = Time.unscaledTime;

				if (File.Exists(CameraPath.oldPathSaveURL))
				{ File.Delete(CameraPath.oldPathSaveURL); } // Remove the old settings if it exists and the new settings were saved.
			}
		}

		void Load()
		{
			CTPersistantField.Load("CToolsSettings", typeof(CamTools), this);
			guiOffsetForward = manualOffsetForward.ToString();
			guiOffsetRight = manualOffsetRight.ToString();
			guiOffsetUp = manualOffsetUp.ToString();
			guiKeyZoomSpeed = keyZoomSpeed.ToString();
			guiFreeMoveSpeed = freeMoveSpeed.ToString();

			DeselectKeyframe();
			availablePaths = new List<CameraPath>();
			ConfigNode pathFileNode = ConfigNode.Load(CameraPath.pathSaveURL);
			if (pathFileNode == null)
			{
				pathFileNode = ConfigNode.Load(CameraPath.oldPathSaveURL);
			}
			if (pathFileNode != null)
			{
				foreach (var node in pathFileNode.GetNode("CAMERAPATHS").GetNodes("CAMERAPATH"))
				{
					availablePaths.Add(CameraPath.Load(node));
				}
			}
			else
			{
				availablePaths.Add(
					new CameraPath
					{
						pathName = "Example Path",
						points = new List<Vector3> {
							new Vector3(13.40305f, -16.60615f, -4.274539f),
							new Vector3(14.48815f, -13.88801f, -4.26651f),
							new Vector3(14.48839f, -13.88819f, -4.267331f),
							new Vector3(15.52922f, -14.25925f, -4.280066f)
						},
						positionInterpolationTypes = new List<PositionInterpolationType>{
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline
						},
						rotations = new List<Quaternion>{
							new Quaternion( 0.5759971f, 0.2491289f,  -0.2965982f, -0.7198553f),
							new Quaternion(-0.6991884f, 0.09197949f, -0.08556388f, 0.7038141f),
							new Quaternion(-0.6991884f, 0.09197949f, -0.08556388f, 0.7038141f),
							new Quaternion(-0.6506922f, 0.2786613f,  -0.271617f,   0.6520521f)
						},
						rotationInterpolationTypes = new List<RotationInterpolationType>{
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp
						},
						times = new List<float> { 0f, 1f, 2f, 6f },
						zooms = new List<float> { 1f, 2.035503f, 3.402367f, 3.402367f },
						timeScale = 0.29f
					}
				);
			}
			selectedPathIndex = Math.Min(selectedPathIndex, availablePaths.Count - 1);
			if (availablePaths.Count > 0 && selectedPathIndex < 0) { selectedPathIndex = 0; }
			// Set some internal and GUI variables.
			freeMoveSpeedRaw = Mathf.Log10(freeMoveSpeed);
			freeMoveSpeedMinRaw = Mathf.Log10(freeMoveSpeedMin);
			freeMoveSpeedMaxRaw = Mathf.Log10(freeMoveSpeedMax);
			zoomSpeedRaw = Mathf.Log10(keyZoomSpeed);
			zoomSpeedMinRaw = Mathf.Log10(keyZoomSpeedMin);
			zoomSpeedMaxRaw = Mathf.Log10(keyZoomSpeedMax);
			zoomMaxExp = Mathf.Log(zoomMax) + 1f;
			signedMaxRelVSqr = Mathf.Abs(maxRelV) * maxRelV;
			guiOffsetForward = manualOffsetForward.ToString();
			guiOffsetRight = manualOffsetRight.ToString();
			guiOffsetUp = manualOffsetUp.ToString();
			guiKeyZoomSpeed = keyZoomSpeed.ToString();
			guiFreeMoveSpeed = freeMoveSpeed.ToString();
			if (inputFields != null)
			{
				if (inputFields.ContainsKey("freeMoveSpeed"))
				{ inputFields["freeMoveSpeed"].UpdateLimits(freeMoveSpeedMin, freeMoveSpeedMax); }
				if (inputFields.ContainsKey("keyZoomSpeed"))
				{ inputFields["keyZoomSpeed"].UpdateLimits(keyZoomSpeedMin, keyZoomSpeedMax); }
			}
			if (DEBUG) { Debug.Log("[CameraTools]: Verbose debugging enabled."); }
		}
		#endregion
	}

	public enum ToolModes { StationaryCamera, DogfightCamera, Pathing };
}
