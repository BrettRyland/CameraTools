using System.Collections.Generic;

namespace CameraTools
{
	public static class VesselExtensions
	{
		public static HashSet<Vessel.Situations> InOrbitSituations = new HashSet<Vessel.Situations> { Vessel.Situations.ORBITING, Vessel.Situations.SUB_ORBITAL, Vessel.Situations.ESCAPING };
		public static bool InOrbit(this Vessel v)
		{
			if (v == null) return false;
			return InOrbitSituations.Contains(v.situation);
		}

		public static Vector3d Velocity(this Vessel v)
		{
			if (v == null) return Vector3d.zero;
			if (!v.InOrbit())
			{
				return v.srf_velocity;
			}
			else
			{
				return v.obt_velocity;
			}
		}

		public static double Speed(this Vessel v)
		{
			if (v == null) return 0;
			if (!v.InOrbit())
			{
				return v.srfSpeed;
			}
			else
			{
				return v.obt_speed;
			}
		}
	}
}