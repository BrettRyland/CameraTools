namespace CameraTools
{
	public static class PartExtensions
	{
		/// <summary>
		/// KSP version dependent query of whether the part is a kerbal on EVA.
		/// </summary>
		/// <param name="part">Part to check.</param>
		/// <returns>true if the part is a kerbal on EVA.</returns>
		public static bool IsKerbalEVA(this Part part)
		{
			if (part == null) return false;
			if ((Versioning.version_major == 1 && Versioning.version_minor > 10) || Versioning.version_major > 1) // Introduced in 1.11
			{
				return part.IsKerbalEVA_1_11();
			}
			else
			{
				return part.IsKerbalEVA_pre_1_11();
			}
		}

		private static bool IsKerbalEVA_1_11(this Part part) // KSP has issues on older versions if this call is in the parent function.
		{
			return part.isKerbalEVA();
		}

		private static bool IsKerbalEVA_pre_1_11(this Part part)
		{
			return part.FindModuleImplementing<KerbalEVA>() != null;
		}
	}
}