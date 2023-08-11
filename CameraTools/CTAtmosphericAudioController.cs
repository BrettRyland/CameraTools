using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CameraTools
{
	public class CTAtmosphericAudioController : MonoBehaviour
	{
		static readonly Dictionary<string, AudioClip> audioClips = new();

		AudioSource windAudioSource;
		AudioSource windHowlAudioSource;
		AudioSource windTearAudioSource;

		AudioSource sonicBoomSource;
		AudioSource delayedSonicBoomSource;

		Vessel vessel;

		bool playedBoom = true;
		bool sleep = false; // For when the SoundManager freaks out about running out of virtual channels.
		float startedSleepAt = 0f;
		float sleepDuration = 0f;

		void Awake()
		{
			vessel = GetComponent<Vessel>();

			windAudioSource = new GameObject("windAS").AddComponent<AudioSource>();
			windAudioSource.minDistance = 10;
			windAudioSource.maxDistance = 10000;
			windAudioSource.dopplerLevel = .35f;
			windAudioSource.spatialBlend = 1;
			if (!audioClips.TryGetValue("CameraTools/Sounds/windloop", out AudioClip windclip))
			{
				windclip = GameDatabase.Instance.GetAudioClip("CameraTools/Sounds/windloop");
				audioClips["CameraTools/Sounds/windloop"] = windclip;
			}
			if (!windclip)
			{
				Destroy(this);
				return;
			}
			windAudioSource.clip = windclip;
			windAudioSource.transform.parent = vessel.transform;

			windHowlAudioSource = new GameObject("windHowlAS").AddComponent<AudioSource>();
			windHowlAudioSource.minDistance = 10;
			windHowlAudioSource.maxDistance = 7000;
			windHowlAudioSource.dopplerLevel = .5f;
			windHowlAudioSource.pitch = 0.25f;
			if (!audioClips.TryGetValue("CameraTools/Sounds/windhowl", out AudioClip windhowlclip))
			{
				windhowlclip = GameDatabase.Instance.GetAudioClip("CameraTools/Sounds/windhowl");
				audioClips["CameraTools/Sounds/windhowl"] = windhowlclip;
			}
			windHowlAudioSource.clip = windhowlclip;
			windHowlAudioSource.spatialBlend = 1;
			windHowlAudioSource.transform.parent = vessel.transform;

			windTearAudioSource = new GameObject("windTearAS").AddComponent<AudioSource>();
			windTearAudioSource.minDistance = 10;
			windTearAudioSource.maxDistance = 5000;
			windTearAudioSource.dopplerLevel = 0.45f;
			windTearAudioSource.pitch = 0.65f;
			if (!audioClips.TryGetValue("CameraTools/Sounds/windtear", out AudioClip windtearclip))
			{
				windtearclip = GameDatabase.Instance.GetAudioClip("CameraTools/Sounds/windtear");
				audioClips["CameraTools/Sounds/windtear"] = windtearclip;
			}
			windTearAudioSource.clip = windtearclip;
			windTearAudioSource.spatialBlend = 1;
			windTearAudioSource.transform.parent = vessel.transform;

			sonicBoomSource = new GameObject("sonicBoomAS").AddComponent<AudioSource>();
			sonicBoomSource.transform.localPosition = Vector3.zero;
			sonicBoomSource.minDistance = 50;
			sonicBoomSource.maxDistance = 20000;
			sonicBoomSource.dopplerLevel = 0;
			if (!audioClips.TryGetValue("CameraTools/Sounds/sonicBoom", out AudioClip sonicBoomclip))
			{
				sonicBoomclip = GameDatabase.Instance.GetAudioClip("CameraTools/Sounds/sonicBoom");
				audioClips["CameraTools/Sounds/sonicBoom"] = sonicBoomclip;
			}
			sonicBoomSource.clip = sonicBoomclip;
			sonicBoomSource.volume = Mathf.Clamp01(vessel.GetTotalMass() / 4f);
			sonicBoomSource.Stop();
			sonicBoomSource.spatialBlend = 1;
			sonicBoomSource.transform.parent = vessel.transform;
			delayedSonicBoomSource = Instantiate(sonicBoomSource);

			Reset();
			CamTools.OnResetCTools += OnResetCTools;
		}

		/// <summary>
		/// Reset some stuff in case we're not a new module.
		/// Also helps when cleaning up to prevent extra booming.
		/// </summary>
		void Reset()
		{
			playedBoom = true; // Default to true so that it doesn't play accidentally.
			if (windAudioSource.isPlaying) windAudioSource.Stop();
			if (windHowlAudioSource.isPlaying) windHowlAudioSource.Stop();
			if (windTearAudioSource.isPlaying) windTearAudioSource.Stop();
			if (sonicBoomSource.isPlaying) sonicBoomSource.Stop();
			if (delayedSonicBoomSource.isPlaying) delayedSonicBoomSource.Stop();
		}

		void FixedUpdate()
		{
			if (vessel == null || !vessel.loaded || !vessel.isActiveAndEnabled) return; // Vessel is dead or not ready.
			if (CamTools.flightCamera == null) return; // Flight camera is broken.
			if (FlightGlobals.currentMainBody != null && vessel.altitude > FlightGlobals.currentMainBody.atmosphereDepth) return; // Vessel is outside the atmosphere.
			if (sleep && Time.time - startedSleepAt < sleepDuration) return;
			sleep = false;
			if (!PauseMenu.isOpen && Time.timeScale > 0 && vessel.dynamicPressurekPa > 0)
			{
				float srfSpeed = (float)vessel.srfSpeed;
				srfSpeed = Mathf.Min(srfSpeed, 550f);
				float angleToCam = Mathf.Clamp(Vector3.Angle(vessel.srf_velocity, CamTools.flightCamera.transform.position - vessel.transform.position), 1, 180);

				// Some comments on what the lagAudioFactor and waveFrontFactor are based on would have been nice...
				float lagAudioFactor = 75000 / (Vector3.Distance(vessel.transform.position, CamTools.flightCamera.transform.position) * srfSpeed * angleToCam / 90);
				lagAudioFactor = Mathf.Clamp(lagAudioFactor * lagAudioFactor * lagAudioFactor, 0, 4);
				lagAudioFactor += srfSpeed / 230;

				float waveFrontFactor = 3.67f * angleToCam / srfSpeed;
				waveFrontFactor = Mathf.Clamp(waveFrontFactor * waveFrontFactor * waveFrontFactor, 0, 2);

				if (vessel.srfSpeed > CamTools.speedOfSound)
				{
					waveFrontFactor = (srfSpeed / angleToCam < 3.67f) ? waveFrontFactor + srfSpeed / (float)CamTools.speedOfSound * waveFrontFactor : 0;

					var cameraOffset = CamTools.flightCamera.transform.position - vessel.transform.position; // d
					var dDotV = Vector3.Dot(vessel.srf_vel_direction, cameraOffset); // dot(d, v) = |d| * |v| * cos(θ) with normalised v
					var dDotVsqr = dDotV * dDotV;
					var sinAlpha = CamTools.speedOfSound / vessel.srfSpeed; // sin(α) = Vsnd / |v|
					var threshold = cameraOffset.sqrMagnitude * (1f - sinAlpha * sinAlpha); // θ > π/2 && cos²(θ) > cos²(α)
					if (dDotV < 0 && dDotVsqr > threshold) // Behind the wave front.
					{
						if (!playedBoom)
						{
							sonicBoomSource.transform.position = vessel.transform.position - vessel.srf_velocity * cameraOffset.magnitude / CamTools.speedOfSound;
							delayedSonicBoomSource.transform.position = sonicBoomSource.transform.position;
							sonicBoomSource.Play();
							delayedSonicBoomSource.PlayDelayed(vessel.vesselSize.z / (float)vessel.srfSpeed); // Sonic booms are generally N-wave shaped, giving a double boom. (vesselSize.z is vessel length.)
							if (CamTools.DEBUG) Debug.Log($"[CameraTools]: Behind the wavefront, playing N-wave sonic boom with interval {vessel.vesselSize.z / (float)vessel.srfSpeed:G3}s for {vessel.vesselName}.");
							playedBoom = true;
						}
					}
					else if (dDotV > 0 || dDotVsqr < threshold * 0.9f) // In front of the wave front (with enough tolerance to not immediately trigger again).
					{
						if (CamTools.DEBUG && playedBoom) Debug.Log($"[CameraTools]: In front of the wavefront, resetting sonic boom trigger for {vessel.vesselName}.");
						playedBoom = false;
					}
				}
				else if (vessel.srfSpeed < CamTools.speedOfSound * 0.95f) // Subsonic (with hysteresis).
				{
					if (CamTools.DEBUG && !playedBoom) Debug.Log($"[CameraTools]: Disabling sonic boom trigger for {vessel.vesselName} due to being subsonic.");
					playedBoom = true;
				}

				lagAudioFactor *= waveFrontFactor;

				float sqrAccel = (float)vessel.acceleration.sqrMagnitude;
				float vesselMass = vessel.GetTotalMass();
				float dynamicPressurekPa = (float)vessel.dynamicPressurekPa;

				//windloop
				if (!windAudioSource.isPlaying)
				{
					windAudioSource.Play();
					// Debug.Log("[CameraTools]: vessel dynamic pressure: " + vessel.dynamicPressurekPa);
					if (!windAudioSource.isPlaying) { SleepFor(1f); return; }
				}
				float pressureFactor = Mathf.Clamp01(dynamicPressurekPa / 50f);
				float massFactor = Mathf.Clamp01(vesselMass / 60f);
				float gFactor = Mathf.Clamp(sqrAccel / 225, 0, 1.5f);
				windAudioSource.volume = massFactor * pressureFactor * gFactor * lagAudioFactor;

				//windhowl
				if (!windHowlAudioSource.isPlaying)
				{
					windHowlAudioSource.Play();
					if (!windHowlAudioSource.isPlaying) { SleepFor(1f); return; }
				}
				float pressureFactor2 = Mathf.Clamp01(dynamicPressurekPa / 20f);
				float massFactor2 = Mathf.Clamp01(vesselMass / 30f);
				windHowlAudioSource.volume = pressureFactor2 * massFactor2 * lagAudioFactor;
				windHowlAudioSource.maxDistance = Mathf.Clamp(lagAudioFactor * 2500, windTearAudioSource.minDistance, 16000);

				//windtear
				if (!windTearAudioSource.isPlaying)
				{
					windTearAudioSource.Play();
					if (!windTearAudioSource.isPlaying) { SleepFor(1f); return; }
				}
				float pressureFactor3 = Mathf.Clamp01(dynamicPressurekPa / 40f);
				float massFactor3 = Mathf.Clamp01(vesselMass / 10f);
				//float gFactor3 = Mathf.Clamp(sqrAccel / 325, 0.25f, 1f);
				windTearAudioSource.volume = pressureFactor3 * massFactor3 * lagAudioFactor;

				windTearAudioSource.minDistance = lagAudioFactor * 1;
				windTearAudioSource.maxDistance = Mathf.Clamp(lagAudioFactor * 2500, windTearAudioSource.minDistance, 16000);

			}
			else
			{
				if (windAudioSource.isPlaying)
				{
					windAudioSource.Stop();
				}

				if (windHowlAudioSource.isPlaying)
				{
					windHowlAudioSource.Stop();
				}

				if (windTearAudioSource.isPlaying)
				{
					windTearAudioSource.Stop();
				}
			}
		}

		void OnDestroy()
		{
			if (sonicBoomSource) Destroy(sonicBoomSource.gameObject);
			if (windAudioSource) Destroy(windAudioSource.gameObject);
			if (windHowlAudioSource) Destroy(windHowlAudioSource.gameObject);
			if (windTearAudioSource) Destroy(windTearAudioSource.gameObject);
			CamTools.OnResetCTools -= OnResetCTools;
		}

		void OnResetCTools()
		{
			Reset(); // Prevent booming when switching vessels/restarting camera modes.
			Destroy(this);
		}

		/// <summary>
		/// Sleep for a bit to allow the SoundManager to recover from running out of channels.
		/// </summary>
		/// <param name="duration">The duration to sleep for.</param>
		void SleepFor(float duration)
		{
			Debug.LogWarning($"[CameraTools]: Inhibiting wind audio for {duration}s due to technical difficulties.");
			sleep = true;
			startedSleepAt = Time.time;
			sleepDuration = duration;
			if (windAudioSource.isPlaying)
				windAudioSource.Stop();
			if (windHowlAudioSource.isPlaying)
				windHowlAudioSource.Stop();
			if (windTearAudioSource.isPlaying)
				windTearAudioSource.Stop();
		}
	}
}

