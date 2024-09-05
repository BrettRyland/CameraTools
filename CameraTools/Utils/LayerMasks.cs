namespace CameraTools.Utils
{
	// LayerMasks for raycasts. Use as (int)(Parts|EVA|Scenery).
	public enum LayerMasks
	{
		Parts = 1 << 0,
		Scenery = 1 << 15,
		Kerbals = 1 << 16, // Internal kerbals
		EVA = 1 << 17,
		Unknown19 = 1 << 19, // Why are some raycasts using this layer?
		RootPart = 1 << 21,
		Unknown23 = 1 << 23, // Why are some raycasts using this layer?
		Wheels = 1 << 26
	}; // Scenery includes terrain and buildings.
}