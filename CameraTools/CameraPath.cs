﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CameraTools
{
	public class CameraPath
	{
		public string pathName;
		public int keyframeCount { get { return points.Count; } }
		public List<Vector3> points;
		public List<PositionInterpolationType> positionInterpolationTypes;
		public List<Quaternion> rotations;
		public List<RotationInterpolationType> rotationInterpolationTypes;
		public List<float> times;
		public List<float> zooms;

		public float timeScale = 1;

		Vector3Animation pointCurve;
		RotationAnimation rotationCurve;
		AnimationCurve zoomCurve;

		public CameraPath()
		{
			pathName = "New Path";
			points = new List<Vector3>();
			rotations = new List<Quaternion>();
			times = new List<float>();
			zooms = new List<float>();
			positionInterpolationTypes = new List<PositionInterpolationType>();
			rotationInterpolationTypes = new List<RotationInterpolationType>();
		}

		public static CameraPath Load(ConfigNode node)
		{
			CameraPath newPath = new CameraPath();

			if (node.HasValue("pathName")) { newPath.pathName = node.GetValue("pathName"); }
			if (node.HasValue("points")) { newPath.points = ParseVectorList(node.GetValue("points")); }
			if (node.HasValue("positionInterpolationTypes")) { newPath.positionInterpolationTypes = ParseEnumTypeList<PositionInterpolationType>(node.GetValue("positionInterpolationTypes")); }
			if (node.HasValue("rotations")) { newPath.rotations = ParseQuaternionList(node.GetValue("rotations")); }
			if (node.HasValue("rotationInterpolationTypes")) { newPath.rotationInterpolationTypes = ParseEnumTypeList<RotationInterpolationType>(node.GetValue("rotationInterpolationTypes")); }
			if (node.HasValue("times")) { newPath.times = ParseFloatList(node.GetValue("times")); }
			if (node.HasValue("zooms")) { newPath.zooms = ParseFloatList(node.GetValue("zooms")); }
			if (node.HasValue("timeScale")) { newPath.timeScale = float.Parse(node.GetValue("timeScale")); } else { newPath.timeScale = 1; }

			// Ensure there's a consistent number of entries in the path.
			while (newPath.positionInterpolationTypes.Count < newPath.points.Count) { newPath.positionInterpolationTypes.Add(PositionInterpolationType.CubicSpline); }
			while (newPath.rotations.Count < newPath.points.Count) { newPath.rotations.Add(Quaternion.identity); }
			while (newPath.rotationInterpolationTypes.Count < newPath.points.Count) { newPath.rotationInterpolationTypes.Add(RotationInterpolationType.Slerp); }
			while (newPath.times.Count < newPath.points.Count) { newPath.times.Add(newPath.times.Count); }
			while (newPath.zooms.Count < newPath.points.Count) { newPath.zooms.Add(1); }
			newPath.Refresh();

			return newPath;
		}

		public void Save(ConfigNode node)
		{
			Debug.Log("[CameraTools]: Saving path: " + pathName);
			ConfigNode pathNode = node.AddNode("CAMERAPATH");
			pathNode.AddValue("pathName", pathName);
			pathNode.AddValue("points", WriteVectorList(points));
			pathNode.AddValue("positionInterpolationTypes", WriteEnumTypeList(positionInterpolationTypes));
			pathNode.AddValue("rotations", WriteQuaternionList(rotations));
			pathNode.AddValue("rotationInterpolationTypes", WriteEnumTypeList(rotationInterpolationTypes));
			pathNode.AddValue("times", WriteFloatList(times));
			pathNode.AddValue("zooms", WriteFloatList(zooms));
			pathNode.AddValue("timeScale", timeScale);
		}

		public static string WriteVectorList(List<Vector3> list)
		{
			return string.Join(";", list.Select(val => ConfigNode.WriteVector(val)));
		}

		public static string WriteQuaternionList(List<Quaternion> list)
		{
			return string.Join(";", list.Select(val => ConfigNode.WriteQuaternion(val)));
		}

		public static string WriteFloatList(List<float> list)
		{
			return string.Join(";", list);
		}

		public static string WriteEnumTypeList<T>(List<T> list) where T : System.Enum
		{
			return string.Join(";", list);
		}

		public static List<Vector3> ParseVectorList(string arrayString)
		{
			string[] vectorStrings = arrayString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			List<Vector3> vList = new List<Vector3>();
			for (int i = 0; i < vectorStrings.Length; i++)
			{
				try
				{
					vList.Add(ConfigNode.ParseVector3(vectorStrings[i]));
				}
				catch (Exception e)
				{
					Debug.LogError("[CameraTools]: Failed to parse vector: --" + vectorStrings[i] + "--, reason: " + e.Message);
				}
			}

			return vList;
		}

		public static List<Quaternion> ParseQuaternionList(string arrayString)
		{
			string[] qStrings = arrayString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			List<Quaternion> qList = new List<Quaternion>();
			for (int i = 0; i < qStrings.Length; i++)
			{
				qList.Add(ConfigNode.ParseQuaternion(qStrings[i]));
			}

			return qList;
		}

		public static List<float> ParseFloatList(string arrayString)
		{
			string[] fStrings = arrayString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			List<float> fList = new List<float>();
			for (int i = 0; i < fStrings.Length; i++)
			{
				fList.Add(float.Parse(fStrings[i]));
			}

			return fList;
		}

		public static List<E> ParseEnumTypeList<E>(string arrayString) where E : struct, System.Enum
		{
			var iList = new List<E>();
			if (string.IsNullOrEmpty(arrayString)) return iList;
			string[] iStrings = arrayString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < iStrings.Length; i++)
			{
				E enumType;
				if (Enum.TryParse<E>(iStrings[i], out enumType))
				{ iList.Add(enumType); }
				else
				{ iList.Add(default(E)); }
			}

			return iList;
		}

		public void AddTransform(Transform cameraTransform, float zoom, float time, PositionInterpolationType positionInterpolationType, RotationInterpolationType rotationInterpolationType)
		{
			points.Add(cameraTransform.localPosition);
			rotations.Add(cameraTransform.localRotation);
			zooms.Add(zoom);
			times.Add(time);
			positionInterpolationTypes.Add(positionInterpolationType);
			rotationInterpolationTypes.Add(rotationInterpolationType);
			Sort();
			UpdateCurves();
		}

		public void SetTransform(int index, Transform cameraTransform, float zoom, float time, PositionInterpolationType positionInterpolationType, RotationInterpolationType rotationInterpolationType)
		{
			points[index] = cameraTransform.localPosition;
			rotations[index] = cameraTransform.localRotation;
			zooms[index] = zoom;
			times[index] = time;
			positionInterpolationTypes[index] = positionInterpolationType;
			rotationInterpolationTypes[index] = rotationInterpolationType;
			Sort();
			UpdateCurves();
		}

		public void Refresh()
		{
			Sort();
			UpdateCurves();
		}

		public void RemoveKeyframe(int index)
		{
			points.RemoveAt(index);
			rotations.RemoveAt(index);
			zooms.RemoveAt(index);
			times.RemoveAt(index);
			positionInterpolationTypes.RemoveAt(index);
			rotationInterpolationTypes.RemoveAt(index);
			UpdateCurves();
		}

		public void Sort()
		{
			List<CameraKeyframe> keyframes = new List<CameraKeyframe>();
			for (int i = 0; i < points.Count; i++)
			{
				keyframes.Add(new CameraKeyframe(points[i], rotations[i], zooms[i], times[i], positionInterpolationTypes[i], rotationInterpolationTypes[i]));
			}
			keyframes.Sort(new CameraKeyframeComparer());

			for (int i = 0; i < keyframes.Count; i++)
			{
				points[i] = keyframes[i].position;
				rotations[i] = keyframes[i].rotation;
				zooms[i] = keyframes[i].zoom;
				times[i] = keyframes[i].time;
			}
		}

		public CameraKeyframe GetKeyframe(int index)
		{
			int i = index;
			return new CameraKeyframe(points[i], rotations[i], zooms[i], times[i], positionInterpolationTypes[i], rotationInterpolationTypes[i]);
		}

		public void UpdateCurves()
		{
			pointCurve = new Vector3Animation(points.ToArray(), times.ToArray(), positionInterpolationTypes.ToArray());
			rotationCurve = new RotationAnimation(rotations.ToArray(), times.ToArray(), rotationInterpolationTypes.ToArray());
			zoomCurve = new AnimationCurve();
			for (int i = 0; i < zooms.Count; i++)
			{
				zoomCurve.AddKey(new Keyframe(times[i], zooms[i]));
			}
		}

		public CameraTransformation Evaulate(float time)
		{
			CameraTransformation tf = new CameraTransformation();
			tf.position = pointCurve.Evaluate(time);
			tf.rotation = rotationCurve.Evaluate(time);
			tf.zoom = zoomCurve.Evaluate(time);

			return tf;
		}
	}
}

