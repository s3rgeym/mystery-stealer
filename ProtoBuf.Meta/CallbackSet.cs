using System;
using System.Reflection;

namespace ProtoBuf.Meta
{
	public class CallbackSet
	{
		private readonly MetaType metaType;

		private MethodInfo beforeSerialize;

		private MethodInfo afterSerialize;

		private MethodInfo beforeDeserialize;

		private MethodInfo afterDeserialize;

		internal MethodInfo this[TypeModel.CallbackType callbackType]
		{
			get
			{
				switch (callbackType)
				{
				case TypeModel.CallbackType.BeforeSerialize:
					return beforeSerialize;
				case TypeModel.CallbackType.AfterSerialize:
					return afterSerialize;
				case TypeModel.CallbackType.BeforeDeserialize:
					return beforeDeserialize;
				case TypeModel.CallbackType.AfterDeserialize:
					return afterDeserialize;
				default:
					throw new ArgumentException("Callback type not supported: " + callbackType, "callbackType");
				}
			}
		}

		public MethodInfo BeforeSerialize
		{
			get
			{
				return beforeSerialize;
			}
			set
			{
				beforeSerialize = SanityCheckCallback(metaType.Model, value);
			}
		}

		public MethodInfo BeforeDeserialize
		{
			get
			{
				return beforeDeserialize;
			}
			set
			{
				beforeDeserialize = SanityCheckCallback(metaType.Model, value);
			}
		}

		public MethodInfo AfterSerialize
		{
			get
			{
				return afterSerialize;
			}
			set
			{
				afterSerialize = SanityCheckCallback(metaType.Model, value);
			}
		}

		public MethodInfo AfterDeserialize
		{
			get
			{
				return afterDeserialize;
			}
			set
			{
				afterDeserialize = SanityCheckCallback(metaType.Model, value);
			}
		}

		public bool NonTrivial
		{
			get
			{
				if (beforeSerialize == null && beforeDeserialize == null && afterSerialize == null)
				{
					return afterDeserialize != null;
				}
				return true;
			}
		}

		internal CallbackSet(MetaType metaType)
		{
			if (metaType == null)
			{
				throw new ArgumentNullException("metaType");
			}
			this.metaType = metaType;
		}

		internal static bool CheckCallbackParameters(TypeModel model, MethodInfo method)
		{
			ParameterInfo[] parameters = method.GetParameters();
			for (int i = 0; i < parameters.Length; i++)
			{
				Type parameterType = parameters[i].ParameterType;
				if (parameterType != model.MapType(typeof(SerializationContext)) && parameterType != model.MapType(typeof(Type)))
				{
					return false;
				}
			}
			return true;
		}

		private MethodInfo SanityCheckCallback(TypeModel model, MethodInfo callback)
		{
			metaType.ThrowIfFrozen();
			if (callback == null)
			{
				return callback;
			}
			if (callback.IsStatic)
			{
				throw new ArgumentException("Callbacks cannot be static", "callback");
			}
			if (callback.ReturnType != model.MapType(typeof(void)) || !CheckCallbackParameters(model, callback))
			{
				throw CreateInvalidCallbackSignature(callback);
			}
			return callback;
		}

		internal static Exception CreateInvalidCallbackSignature(MethodInfo method)
		{
			return new NotSupportedException("Invalid callback signature in " + method.DeclaringType.FullName + "." + method.Name);
		}
	}
}
