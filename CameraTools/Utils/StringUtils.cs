using KSP.Localization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CameraTools.Utils
{
	public static class StringUtils
	{
		static readonly Dictionary<string, string> localizedStrings = new(); // Cache localized strings so that they don't need to be repeatedly localized.

		public static string Localize(string template)
		{
			if (!localizedStrings.TryGetValue(template, out string result))
			{
				result = Localizer.Format(template);
				localizedStrings[template] = result;
			}
			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GUIContent LocTip(string template) => LocalizeWithToolTip(template); // Shortcut for convenience
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GUIContent LocalizeWithToolTip(string template)
		{
			return new GUIContent(Localize(template), Localize($"{template}.tip"));
		}
	}
}