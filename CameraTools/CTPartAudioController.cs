using UnityEngine;
namespace CameraTools
{
	public class CTPartAudioController : MonoBehaviour
	{
		Vessel vessel;
		Part part;

		public AudioSource audioSource;

		readonly float minDist = 10;
		readonly float maxDist = 10000;

		void Awake()
		{
			part = GetComponentInParent<Part>();
			vessel = part.vessel;
		}

		void Start()
		{
			if (!audioSource)
			{
				Destroy(this);
				return;
			}

			CamTools.OnResetCTools += OnResetCTools;
		}

		void FixedUpdate()
		{
			if (!audioSource)
			{
				Destroy(this);
				return;
			}

			if (!part || !vessel || !FlightCamera.fetch)
			{
				Destroy(this);
				return;
			}

			if (!origSettingsStored) return; // Do nothing until the original settings get stored.

			float angleToCam = Vector3.Angle(vessel.srf_velocity, FlightCamera.fetch.mainCamera.transform.position - vessel.transform.position);
			angleToCam = Mathf.Clamp(angleToCam, 1, 180);

			float srfSpeed = (float)vessel.srfSpeed;
			srfSpeed = Mathf.Min(srfSpeed, 550f);

			float lagAudioFactor = 75000 / (Vector3.Distance(vessel.transform.position, FlightCamera.fetch.mainCamera.transform.position) * srfSpeed * angleToCam / 90);
			lagAudioFactor = Mathf.Clamp(lagAudioFactor * lagAudioFactor * lagAudioFactor, 0, 4);
			lagAudioFactor += srfSpeed / 230;

			float waveFrontFactor = 3.67f * angleToCam / srfSpeed;
			waveFrontFactor = Mathf.Clamp(waveFrontFactor * waveFrontFactor * waveFrontFactor, 0, 2);
			if (vessel.srfSpeed > CamTools.speedOfSound)
			{
				waveFrontFactor = (srfSpeed / angleToCam < 3.67f) ? waveFrontFactor + (srfSpeed / (float)CamTools.speedOfSound * waveFrontFactor) : 0;
			}

			lagAudioFactor *= waveFrontFactor;

			audioSource.minDistance = Mathf.Lerp(origMinDist, minDist * lagAudioFactor, Mathf.Clamp01((float)vessel.srfSpeed / 30));
			audioSource.maxDistance = Mathf.Lerp(origMaxDist, Mathf.Clamp(maxDist * lagAudioFactor, audioSource.minDistance, 16000), Mathf.Clamp01((float)vessel.srfSpeed / 30));
		}

		void OnDestroy()
		{
			CamTools.OnResetCTools -= OnResetCTools;
		}

		#region Store/Restore Original settings.
		bool origSettingsStored = false;
		// Any settings that get adjusted in ApplyEffects should be added here.
		float origMinDist;
		float origMaxDist;
		bool origBypassEffects;
		bool origSpatialize;
		float origSpatialBlend;
		bool origSpatializePostEffects;
		float origDopplerLevel;
		AudioVelocityUpdateMode origVelocityUpdateMode;
		AudioRolloffMode origRolloffMode;

		public void StoreOriginalSettings()
		{
			if (audioSource == null) return;
			origSettingsStored = true;
			origMinDist = audioSource.minDistance;
			origMaxDist = audioSource.maxDistance;
			origBypassEffects = audioSource.bypassEffects;
			origSpatialize = audioSource.spatialize;
			origSpatialBlend = audioSource.spatialBlend;
			origSpatializePostEffects = audioSource.spatializePostEffects;
			origDopplerLevel = audioSource.dopplerLevel;
			origVelocityUpdateMode = audioSource.velocityUpdateMode;
			origRolloffMode = audioSource.rolloffMode;
		}

		public void RestoreOriginalSettings()
		{
			if (!origSettingsStored || audioSource == null) return;
			audioSource.minDistance = origMinDist;
			audioSource.maxDistance = origMaxDist;
			audioSource.bypassEffects = origBypassEffects;
			audioSource.spatialize = origSpatialize;
			audioSource.spatialBlend = origSpatialBlend;
			audioSource.spatializePostEffects = origSpatializePostEffects;
			audioSource.dopplerLevel = origDopplerLevel;
			audioSource.velocityUpdateMode = origVelocityUpdateMode;
			audioSource.rolloffMode = origRolloffMode;
		}
		#endregion

		public void ApplyEffects()
		{
			if (audioSource == null) return;
			audioSource.bypassEffects = false;
			audioSource.spatialize = true;
			audioSource.spatialBlend = 1;
			audioSource.spatializePostEffects = true;
			audioSource.dopplerLevel = 1;
			audioSource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
			audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
		}

		void OnResetCTools()
		{
			if (audioSource != null)
			{
				RestoreOriginalSettings();
			}
			Destroy(this);
		}
	}
}

