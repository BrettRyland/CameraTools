using UnityEngine;
using System.Linq;

namespace CameraTools.Utils
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class Tooltips : MonoBehaviour
	{
		public static Tooltips Instance;

		/// <summary>
		/// Set the tooltip for the next frame.
		/// </summary>
		/// <param name="tooltip">The tooltip to set.</param>
		/// <param name="position">The screen position of the mouse.<br>E.g., "Event.current.mousePosition" if not within a GUI.Window, "Event.current.mousePosition + windowRect.position" otherwise.</br></param>
		public static void SetTooltip(string tooltip, Vector2 position = default) => Instance.SetTooltipImpl(tooltip, position);

		string tooltip = ""; // The current tooltip being shown.
		string _tooltip = ""; // Staging for the next frame's tooltip. This avoids Repaint errors due to changing the tooltip.
		Rect tooltipRect = new();

		void Awake()
		{
			// Refresh any lingering instances.
			if (Instance) Destroy(Instance);
			Instance = this;
		}

		void LateUpdate()
		{
			tooltip = _tooltip;
			_tooltip = "";
		}

		void OnGUI()
		{
			if (CamTools.ShowTooltips && !string.IsNullOrEmpty(tooltip))
			{
				var windowID = GUIUtility.GetControlID(FocusType.Passive);
				tooltipRect = GUILayout.Window(windowID, tooltipRect, ShowTooltip, "", GUIStyle.none);
				GUI.BringWindowToFront(windowID);
			}
		}

		void ShowTooltip(int windowID)
		{
			GUILayout.BeginVertical();
			GUILayout.Label(tooltip, GUI.skin.box);
			GUILayout.EndVertical();
		}

		void SetTooltipImpl(string tip, Vector2 position = default)
		{
			if (string.IsNullOrEmpty(tip)) return; // Ignore empty tooltips. (We clear the tip in LateUpdate.)
			if (!string.IsNullOrEmpty(_tooltip)) return; // Someone has already stored a tooltip.
			_tooltip = tip;
			new GUIContent(_tooltip);
			tooltipRect.position = position;
			var lines = _tooltip.Split('\n');
			tooltipRect.width = lines.Max(l => GUI.skin.box.CalcSize(new GUIContent(l)).x);
			tooltipRect.height = 20 * lines.Length; // Text height is 20px.
			tooltipRect.x = Mathf.Clamp(tooltipRect.x - tooltipRect.width - 20, 5, Screen.width - tooltipRect.width - 5);
			tooltipRect.y = Mathf.Clamp(tooltipRect.y - tooltipRect.height - 10, 5, Screen.height - tooltipRect.height - 5);
		}
	}
}