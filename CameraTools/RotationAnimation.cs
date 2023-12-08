using UnityEngine;

using CameraTools.Utils;

namespace CameraTools
{
	public enum RotationInterpolationType
	{
		Linear, // Linear interpolation component-wise
		CubicSpline, // Cubic spline interpolation component-wise
		Slerp // spherical linear interpolation
	};
	public class RotationAnimation
	{
		Quaternion[] rotations;
		float[] times;
		RotationInterpolationType[] interpolationTypes;

		public RotationAnimation(Quaternion[] rots, float[] times, RotationInterpolationType[] interpolationTypes)
		{
			this.rotations = rots;
			this.times = times;
			this.interpolationTypes = interpolationTypes;
			SetShortestRotations();
		}

		public void SetShortestRotations()
		{
			for (int i = 1; i < rotations.Length; ++i)
			{
				if (QuaternionDiffSqr(rotations[i - 1], rotations[i]) > QuaternionDiffSqr(rotations[i - 1], NegateQuaternion(rotations[i])))
				{
					rotations[i] = NegateQuaternion(rotations[i]);
				}
			}
		}

		float QuaternionDiffSqr(Quaternion q1, Quaternion q2)
		{
			Quaternion d = new Quaternion(q2.x - q1.x, q2.y - q1.y, q2.z - q1.z, q2.w - q1.w);
			return d.x * d.x + d.y * d.y + d.z * d.z + d.w * d.w;
		}

		Quaternion NegateQuaternion(Quaternion q)
		{
			return new Quaternion(-q.x, -q.y, -q.z, -q.w);
		}

		public Quaternion Evaluate(float t)
		{
			// Sanity checks
			if (rotations.Length == 0) return Quaternion.identity;
			if (rotations.Length == 1) return rotations[0];

			int startIndex = 0;
			for (startIndex = 0; startIndex < times.Length - 1; ++startIndex)
				if (t < times[startIndex + 1])
					break;

			// Edge case 0: at the end of the path.
			if (startIndex == rotations.Length - 1)
			{ return rotations[startIndex]; }

			switch (interpolationTypes[startIndex])
			{
				case RotationInterpolationType.Linear:
					{
						int nextIndex = Mathf.RoundToInt(Mathf.Min(startIndex + 1, times.Length - 1));

						float overTime = t - times[startIndex];
						float intervalTime = times[nextIndex] - times[startIndex];
						if (intervalTime <= 0) return rotations[nextIndex];

						float normTime = overTime / intervalTime;
						return Quaternion.Lerp(rotations[startIndex], rotations[nextIndex], normTime);
					}
				case RotationInterpolationType.CubicSpline:
					{
						Quaternion slope1, slope2;
						// Edge case 1: entire curve is just two points.
						if (rotations.Length == 2)
						{
							slope1 = SplineUtils.EstimateSlope(rotations[0], rotations[1], times[1] - times[0]);
							slope2 = slope1;
						}
						// Edge case 2: first section of the curve.
						else if (startIndex == 0)
						{
							slope1 = SplineUtils.EstimateSlope(rotations[0], rotations[1], times[1] - times[0]);
							slope2 = SplineUtils.EstimateSlope(rotations[0], rotations[1], rotations[2], times[1] - times[0], times[2] - times[1]);
						}
						else if (startIndex == rotations.Length - 2)// Edge case 3: last section of the curve.
						{
							slope1 = SplineUtils.EstimateSlope(rotations[startIndex - 1], rotations[startIndex], rotations[startIndex + 1], times[startIndex] - times[startIndex - 1], times[startIndex + 1] - times[startIndex]);
							slope2 = SplineUtils.EstimateSlope(rotations[startIndex], rotations[startIndex + 1], times[startIndex + 1] - times[startIndex]);
						}
						else // General case: in the middle of the curve.
						{
							slope1 = SplineUtils.EstimateSlope(rotations[startIndex - 1], rotations[startIndex], rotations[startIndex + 1], times[startIndex] - times[startIndex - 1], times[startIndex + 1] - times[startIndex]);
							slope2 = SplineUtils.EstimateSlope(rotations[startIndex], rotations[startIndex + 1], rotations[startIndex + 2], times[startIndex + 1] - times[startIndex], times[startIndex + 2] - times[startIndex + 1]);
						}
						return SplineUtils.EvaluateSpline(rotations[startIndex], slope1, rotations[startIndex + 1], slope2, t, times[startIndex], times[startIndex + 1]);
					}
				case RotationInterpolationType.Slerp:
					{
						return Quaternion.Slerp(rotations[startIndex], rotations[startIndex + 1], (t - times[startIndex]) / (times[startIndex + 1] - times[startIndex]));
					}
				default:
					Debug.LogError($"[CameraTools.RotationAnimation]: Invalid interpolation type {interpolationTypes[startIndex]}");
					return Quaternion.identity;
			}
		}
	}
}

