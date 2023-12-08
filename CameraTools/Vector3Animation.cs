using UnityEngine;

using CameraTools.Utils;

namespace CameraTools
{
	public enum PositionInterpolationType { Linear, CubicSpline };
	public class Vector3Animation
	{
		Vector3[] positions;
		float[] times;
		PositionInterpolationType[] interpolationTypes;

		public Vector3Animation(Vector3[] pos, float[] times, PositionInterpolationType[] interpolationTypes)
		{
			this.positions = pos;
			this.times = times;
			this.interpolationTypes = interpolationTypes;
		}

		public Vector3 Evaluate(float t)
		{
			// Sanity checks
			if (positions.Length == 0) return Vector3.zero;
			if (positions.Length == 1) return positions[0];

			int startIndex = 0;
			for (startIndex = 0; startIndex < times.Length - 1; ++startIndex)
				if (t < times[startIndex + 1])
					break;

			// Edge case 0: at the end of the path.
			if (startIndex == positions.Length - 1)
			{ return positions[startIndex]; }

			switch (interpolationTypes[startIndex])
			{
				case PositionInterpolationType.Linear: // Linear interpolation.
					int nextIndex = Mathf.Min(startIndex + 1, times.Length - 1);

					float overTime = t - times[startIndex];
					float intervalTime = times[nextIndex] - times[startIndex];
					if (intervalTime <= 0) return positions[nextIndex];

					float normTime = overTime / intervalTime;
					return Vector3.Lerp(positions[startIndex], positions[nextIndex], normTime);
				case PositionInterpolationType.CubicSpline: // Cubic spline interpolation using Hermite polynomials.
					Vector3 slope1, slope2;
					// Edge case 1: entire curve is just two points.
					if (positions.Length == 2)
					{
						slope1 = SplineUtils.EstimateSlope(positions[0], positions[1], times[1] - times[0]);
						slope2 = slope1;
					}
					// Edge case 2: first section of the curve.
					else if (startIndex == 0)
					{
						slope1 = SplineUtils.EstimateSlope(positions[0], positions[1], times[1] - times[0]);
						slope2 = SplineUtils.EstimateSlope(positions[0], positions[1], positions[2], times[1] - times[0], times[2] - times[1]);
					}
					else if (startIndex == positions.Length - 2)// Edge case 3: last section of the curve.
					{
						slope1 = SplineUtils.EstimateSlope(positions[startIndex - 1], positions[startIndex], positions[startIndex + 1], times[startIndex] - times[startIndex - 1], times[startIndex + 1] - times[startIndex]);
						slope2 = SplineUtils.EstimateSlope(positions[startIndex], positions[startIndex + 1], times[startIndex + 1] - times[startIndex]);
					}
					else // General case: in the middle of the curve.
					{
						slope1 = SplineUtils.EstimateSlope(positions[startIndex - 1], positions[startIndex], positions[startIndex + 1], times[startIndex] - times[startIndex - 1], times[startIndex + 1] - times[startIndex]);
						slope2 = SplineUtils.EstimateSlope(positions[startIndex], positions[startIndex + 1], positions[startIndex + 2], times[startIndex + 1] - times[startIndex], times[startIndex + 2] - times[startIndex + 1]);
					}
					return SplineUtils.EvaluateSpline(positions[startIndex], slope1, positions[startIndex + 1], slope2, t, times[startIndex], times[startIndex + 1]);
				default:
					Debug.LogError($"[CameraTools.Vector3Animation]: Invalid interpolation type {interpolationTypes[startIndex]}");
					return Vector3.zero;
			}
		}
	}
}

