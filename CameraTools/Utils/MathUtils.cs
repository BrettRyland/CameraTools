using UnityEngine;
using System.Globalization;

namespace CameraTools.Utils
{
	public static class MathUtils
	{
		// This is a fun workaround for M1-chip Macs (Apple Silicon). Specific issue the workaround is for is here: 
		// https://issuetracker.unity3d.com/issues/m1-incorrect-calculation-of-values-using-multiplication-with-mathf-dot-sqrt-when-an-unused-variable-is-declared
		// Borrowed from BDArmoryPlus.
		public static float Sqrt(float value) => (OSUtils.AppleSilicon) ? SqrtARM(value) : (float)System.Math.Sqrt((double)value);
		public static double Sqrt(double value) => (OSUtils.AppleSilicon) ? SqrtARM(value) : System.Math.Sqrt(value);

		private static float SqrtARM(float value)
		{
			float sqrt = (float)System.Math.Sqrt((double)value);
			float sqrt1 = 1f * sqrt;
			return sqrt1;
		}
		private static double SqrtARM(double value)
		{
			double sqrt = System.Math.Sqrt(value);
			double sqrt1 = 1d * sqrt;
			return sqrt1;
		}

		public static float RoundToUnit(float value, float unit = 1f)
		{
			var rounded = Mathf.Round(value / unit) * unit;
			return (unit % 1 != 0) ? rounded : Mathf.Round(rounded); // Fix near-integer loss of precision.
		}
	}

	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	internal class OSUtils : MonoBehaviour
	{
		static public bool AppleSilicon = false;
		void Awake()
		{
			// Check for Apple Processor
			AppleSilicon = CultureInfo.InvariantCulture.CompareInfo.IndexOf(SystemInfo.processorType, "Apple", CompareOptions.IgnoreCase) >= 0;
		}
	}
}