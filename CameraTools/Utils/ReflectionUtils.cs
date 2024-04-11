using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace CameraTools.Utils
{
	/// <summary>
	/// Using delegates to speed up reflection for frequently accessed properties and fields.
	/// This can give up to 1000x faster (but typically around 50-200x faster) access to these properties and fields.
	/// https://stackoverflow.com/questions/10820453/reflection-performance-create-delegate-properties-c for properties.
	/// https://stackoverflow.com/questions/16073091/is-there-a-way-to-create-a-delegate-to-get-and-set-values-for-a-fieldinfo for fields.
	/// </summary>
	public static class ReflectionUtils
	{
		public static Func<object, T> BuildGetAccessor<T>(MethodInfo method)
		{
			var obj = Expression.Parameter(typeof(object), "o");

			Expression<Func<object, T>> expr =
				Expression.Lambda<Func<object, T>>(
					Expression.Convert(
						Expression.Call(
							method.IsStatic ? null : Expression.Convert(obj, method.DeclaringType),
							method),
						typeof(T)),
					obj);

			return expr.Compile();
		}

		public static Action<object, T> BuildSetAccessor<T>(MethodInfo method)
		{
			var obj = Expression.Parameter(typeof(object), "o");
			var value = Expression.Parameter(typeof(T));

			Expression<Action<object, T>> expr =
				Expression.Lambda<Action<object, T>>(
					Expression.Call(
						method.IsStatic ? null : Expression.Convert(obj, method.DeclaringType),
						method,
						Expression.Convert(value, method.GetParameters()[0].ParameterType)),
					obj,
					value);

			return expr.Compile();
		}

		public static Func<S, T> CreateGetter<S, T>(FieldInfo field)
		{
			string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
			DynamicMethod getterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] { typeof(S) }, true);
			ILGenerator gen = getterMethod.GetILGenerator();
			if (field.IsStatic)
			{
				gen.Emit(OpCodes.Ldsfld, field);
			}
			else
			{
				gen.Emit(OpCodes.Ldarg_0);
				gen.Emit(OpCodes.Ldfld, field);
			}
			gen.Emit(OpCodes.Ret);
			return (Func<S, T>)getterMethod.CreateDelegate(typeof(Func<S, T>));
		}

		public static Action<S, T> CreateSetter<S, T>(FieldInfo field)
		{
			string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
			DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(S), typeof(T) }, true);
			ILGenerator gen = setterMethod.GetILGenerator();
			if (field.IsStatic)
			{
				gen.Emit(OpCodes.Ldarg_1);
				gen.Emit(OpCodes.Stsfld, field);
			}
			else
			{
				gen.Emit(OpCodes.Ldarg_0);
				gen.Emit(OpCodes.Ldarg_1);
				gen.Emit(OpCodes.Stfld, field);
			}
			gen.Emit(OpCodes.Ret);
			return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
		}

	}
}