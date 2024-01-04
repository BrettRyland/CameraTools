using KSP.Localization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CameraTools.Utils
{
	public static class StringUtils
	{
		static readonly Dictionary<string, string> localizedStrings = []; // Cache localized strings so that they don't need to be repeatedly localized.
		static readonly Dictionary<string, bool> hasTip = []; // Cache whether a tooltip exists for the string.

		static string GetLocalization(string template)
		{
			if (!localizedStrings.TryGetValue(template, out string result))
			{
				result = Localizer.Format(template);
				localizedStrings[template] = result;
			}
			return result;
		}

		/// <summary>
		/// Get the localisation of #LOC_CameraTools_template as a GUIContent with a tooltip if available.
		/// If the localised string ends with ":", then a space is appended.
		/// If param is not null, it is appended to the localised string.
		/// </summary>
		/// <param name="template"></param>
		/// <param name="param"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static GUIContent Localize(string template, string param = null)
		{
			template = $"#LOC_CameraTools_{template}";
			if (!hasTip.ContainsKey(template))
			{
				var tip = $"{template}.tip";
				var localisedTip = GetLocalization(tip);
				hasTip[template] = tip != localisedTip;
			}
			var content = GetLocalization(template);
			if (content.EndsWith(":")) content += " "; // Add a space after ":"
			if (param != null) content += param; // Add the parameter
			if (hasTip[template]) return new GUIContent(content, GetLocalization($"{template}.tip"));
			else return new GUIContent(content);
		}

		/// <summary>
		/// Get the localisation of #LOC_CameraTools_template as a string.
		/// </summary>
		/// <param name="template"></param>
		public static string LocalizeStr(string template)
		{
			template = $"#LOC_CameraTools_{template}";
			return GetLocalization(template);
		}
	}
}