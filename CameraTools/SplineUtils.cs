using UnityEngine;

namespace CameraTools
{
	public static class SplineUtils
	{
		// TODO Add checks for when time-deltas are really small that might give NaNs/Infs and handle them gracefully
		#region Vector3
		/// <summary>
		/// Evaluate the function p(x) = h00(t)*p0 + h10(t)*(x1-x0)*m0 + h01(t)*p1 + h11(t)*(x1-x0)*m1, where
		/// 		h00, h10, h01, h11 are the Hermite polynomial basis functions,
		/// 		p0, p1, m0, m1 are the points and their slopes,
		/// 		t is the normalised distance [0,1] between the points (x0 and x1) to perform the interpolation.
		/// 		https://en.wikipedia.org/wiki/Cubic_Hermite_spline#Interpolation_on_an_arbitrary_interval
		/// <param name="point1">The start point</param>
		/// <param name="slope1">The slope at the start point</param>
		/// <param name="point2">The end point</param>
		/// <param name="slope2">The slope at the end point</param>
		/// <param name="t">The point in the interval (between tStart and tStop) to evaluate the function</param>
		/// <param name="tStart">The start of the interval</param>
		/// <param name="tStop">The end of the interval</param>
		/// <returns></returns>
		public static Vector3 EvaluateSpline(Vector3 point1, Vector3 slope1, Vector3 point2, Vector3 slope2, float t, float tStart, float tStop)
		{
			var dt = tStop - tStart;
			t = Mathf.Clamp01((t - tStart) / dt); // Rescale the t paramter and enforce that it is in the correct range.
			var t2 = t * t;
			var t3 = t2 * t;
			var h00 = 2 * t3 - 3 * t2 + 1;
			var h10 = t3 - 2 * t2 + t;
			var h01 = -2 * t3 + 3 * t2;
			var h11 = t3 - t2;
			return h00 * point1 + h10 * dt * slope1 + h01 * point2 + h11 * dt * slope2;
		}

		/// <summary>
		/// Slope estimation using 3-point finite difference.
		/// https://en.wikipedia.org/wiki/Cubic_Hermite_spline#Finite_difference
		/// </summary>
		/// <param name="point0"></param>
		/// <param name="point1"></param>
		/// <param name="point2"></param>
		/// <param name="dt01"></param>
		/// <param name="dt12"></param>
		/// <returns></returns>
		public static Vector3 EstimateSlope(Vector3 point0, Vector3 point1, Vector3 point2, float dt01, float dt12)
		{
			if (dt01 == 0 || dt12 == 0) return Vector3.zero;
			return 0.5f * ((point2 - point1) / dt12 + (point1 - point0) / dt01);
		}

		/// <summary>
		/// Slope estimation using 2-point finite difference.
		/// </summary>
		/// <param name="point0"></param>
		/// <param name="point1"></param>
		/// <param name="dt"></param>
		/// <returns></returns>
		public static Vector3 EstimateSlope(Vector3 point0, Vector3 point1, float dt)
		{
			if (dt == 0) return Vector3.zero;
			return (point1 - point0) / dt;
		}
		#endregion

		#region Quaternions
		// Note: same as for Vector3, but component-wise on quaternions.

		/// <summary>
		/// Evaluate the function p(x) = h00(t)*p0 + h10(t)*(x1-x0)*m0 + h01(t)*p1 + h11(t)*(x1-x0)*m1, where
		/// 		h00, h10, h01, h11 are the Hermite polynomial basis functions,
		/// 		p0, p1, m0, m1 are the points and their slopes,
		/// 		t is the normalised distance [0,1] between the points (x0 and x1) to perform the interpolation.
		/// 		https://en.wikipedia.org/wiki/Cubic_Hermite_spline#Interpolation_on_an_arbitrary_interval
		/// <param name="point1">The start point</param>
		/// <param name="slope1">The slope at the start point</param>
		/// <param name="point2">The end point</param>
		/// <param name="slope2">The slope at the end point</param>
		/// <param name="t">The point in the interval (between tStart and tStop) to evaluate the function</param>
		/// <param name="tStart">The start of the interval</param>
		/// <param name="tStop">The end of the interval</param>
		/// <returns></returns>
		public static Quaternion EvaluateSpline(Quaternion point1, Quaternion slope1, Quaternion point2, Quaternion slope2, float t, float tStart, float tStop)
		{
			var dt = tStop - tStart;
			t = Mathf.Clamp01((t - tStart) / dt); // Rescale the t paramter and enforce that it is in the correct range.
			var t2 = t * t;
			var t3 = t2 * t;
			var h00 = 2 * t3 - 3 * t2 + 1;
			var h10 = t3 - 2 * t2 + t;
			var h01 = -2 * t3 + 3 * t2;
			var h11 = t3 - t2;
			return new Quaternion(
				h00 * point1.x + h10 * dt * slope1.x + h01 * point2.x + h11 * dt * slope2.x,
				h00 * point1.y + h10 * dt * slope1.y + h01 * point2.y + h11 * dt * slope2.y,
				h00 * point1.z + h10 * dt * slope1.z + h01 * point2.z + h11 * dt * slope2.z,
				h00 * point1.w + h10 * dt * slope1.w + h01 * point2.w + h11 * dt * slope2.w
			);
		}

		/// <summary>
		/// Slope estimation using 3-point finite difference.
		/// https://en.wikipedia.org/wiki/Cubic_Hermite_spline#Finite_difference
		/// </summary>
		/// <param name="point0"></param>
		/// <param name="point1"></param>
		/// <param name="point2"></param>
		/// <param name="dt01"></param>
		/// <param name="dt12"></param>
		/// <returns></returns>
		public static Quaternion EstimateSlope(Quaternion point0, Quaternion point1, Quaternion point2, float dt01, float dt12)
		{
			if (dt01 == 0 || dt12 == 0) return Quaternion.identity;
			return new Quaternion(
				0.5f * ((point2.x - point1.x) / dt12 + (point1.x - point0.x) / dt01),
				0.5f * ((point2.y - point1.y) / dt12 + (point1.y - point0.y) / dt01),
				0.5f * ((point2.z - point1.z) / dt12 + (point1.z - point0.z) / dt01),
				0.5f * ((point2.w - point1.w) / dt12 + (point1.w - point0.w) / dt01)
			);
		}

		/// <summary>
		/// Slope estimation using 2-point finite difference.
		/// </summary>
		/// <param name="point0"></param>
		/// <param name="point1"></param>
		/// <param name="dt"></param>
		/// <returns></returns>
		public static Quaternion EstimateSlope(Quaternion point0, Quaternion point1, float dt)
		{
			if (dt == 0) return Quaternion.identity;
			return new Quaternion(
				(point1.x - point0.x) / dt,
				(point1.y - point0.y) / dt,
				(point1.z - point0.z) / dt,
				(point1.w - point0.w) / dt
			);
		}
		#endregion
	}
}