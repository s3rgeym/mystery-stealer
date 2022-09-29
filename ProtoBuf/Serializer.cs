using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;

namespace ProtoBuf
{
	public static class Serializer
	{
		public static class NonGeneric
		{
			public static object DeepClone(object instance)
			{
				if (instance != null)
				{
					return RuntimeTypeModel.Default.DeepClone(instance);
				}
				return null;
			}

			public static void Serialize(Stream dest, object instance)
			{
				if (instance != null)
				{
					RuntimeTypeModel.Default.Serialize(dest, instance);
				}
			}

			public static object Deserialize(Type type, Stream source)
			{
				return RuntimeTypeModel.Default.Deserialize(source, null, type);
			}

			public static object Merge(Stream source, object instance)
			{
				if (instance == null)
				{
					throw new ArgumentNullException("instance");
				}
				return RuntimeTypeModel.Default.Deserialize(source, instance, instance.GetType(), null);
			}

			public static void SerializeWithLengthPrefix(Stream destination, object instance, PrefixStyle style, int fieldNumber)
			{
				if (instance == null)
				{
					throw new ArgumentNullException("instance");
				}
				RuntimeTypeModel @default = RuntimeTypeModel.Default;
				@default.SerializeWithLengthPrefix(destination, instance, @default.MapType(instance.GetType()), style, fieldNumber);
			}

			public static bool TryDeserializeWithLengthPrefix(Stream source, PrefixStyle style, TypeResolver resolver, out object value)
			{
				value = RuntimeTypeModel.Default.DeserializeWithLengthPrefix(source, null, null, style, 0, resolver);
				return value != null;
			}

			public static bool CanSerialize(Type type)
			{
				return RuntimeTypeModel.Default.IsDefined(type);
			}
		}

		public static class GlobalOptions
		{
			[Obsolete("Please use RuntimeTypeModel.Default.InferTagFromNameDefault instead (or on a per-model basis)", false)]
			public static bool InferTagFromName
			{
				get
				{
					return RuntimeTypeModel.Default.InferTagFromNameDefault;
				}
				set
				{
					RuntimeTypeModel.Default.InferTagFromNameDefault = value;
				}
			}
		}

		public delegate Type TypeResolver(int fieldNumber);

		private const string ProtoBinaryField = "proto";

		public const int ListItemTag = 1;

		public static string GetProto<T>()
		{
			return RuntimeTypeModel.Default.GetSchema(RuntimeTypeModel.Default.MapType(typeof(T)));
		}

		public static T DeepClone<T>(T instance)
		{
			if (instance != null)
			{
				return (T)RuntimeTypeModel.Default.DeepClone(instance);
			}
			return instance;
		}

		public static T Merge<T>(Stream source, T instance)
		{
			return (T)RuntimeTypeModel.Default.Deserialize(source, instance, typeof(T));
		}

		public static T Deserialize<T>(Stream source)
		{
			return (T)RuntimeTypeModel.Default.Deserialize(source, null, typeof(T));
		}

		public static void Serialize<T>(Stream destination, T instance)
		{
			if (instance != null)
			{
				RuntimeTypeModel.Default.Serialize(destination, instance);
			}
		}

		public static TTo ChangeType<TFrom, TTo>(TFrom instance)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				Serialize(memoryStream, instance);
				memoryStream.Position = 0L;
				return Deserialize<TTo>(memoryStream);
			}
		}

		public static void PrepareSerializer<T>()
		{
		}

		public static IEnumerable<T> DeserializeItems<T>(Stream source, PrefixStyle style, int fieldNumber)
		{
			return RuntimeTypeModel.Default.DeserializeItems<T>(source, style, fieldNumber);
		}

		public static T DeserializeWithLengthPrefix<T>(Stream source, PrefixStyle style)
		{
			return DeserializeWithLengthPrefix<T>(source, style, 0);
		}

		public static T DeserializeWithLengthPrefix<T>(Stream source, PrefixStyle style, int fieldNumber)
		{
			RuntimeTypeModel @default = RuntimeTypeModel.Default;
			return (T)@default.DeserializeWithLengthPrefix(source, null, @default.MapType(typeof(T)), style, fieldNumber);
		}

		public static T MergeWithLengthPrefix<T>(Stream source, T instance, PrefixStyle style)
		{
			RuntimeTypeModel @default = RuntimeTypeModel.Default;
			return (T)@default.DeserializeWithLengthPrefix(source, instance, @default.MapType(typeof(T)), style, 0);
		}

		public static void SerializeWithLengthPrefix<T>(Stream destination, T instance, PrefixStyle style)
		{
			SerializeWithLengthPrefix(destination, instance, style, 0);
		}

		public static void SerializeWithLengthPrefix<T>(Stream destination, T instance, PrefixStyle style, int fieldNumber)
		{
			RuntimeTypeModel @default = RuntimeTypeModel.Default;
			@default.SerializeWithLengthPrefix(destination, instance, @default.MapType(typeof(T)), style, fieldNumber);
		}

		public static bool TryReadLengthPrefix(Stream source, PrefixStyle style, out int length)
		{
			length = ProtoReader.ReadLengthPrefix(source, expectHeader: false, style, out int _, out int bytesRead);
			return bytesRead > 0;
		}

		public static bool TryReadLengthPrefix(byte[] buffer, int index, int count, PrefixStyle style, out int length)
		{
			using (Stream source = new MemoryStream(buffer, index, count))
			{
				return TryReadLengthPrefix(source, style, out length);
			}
		}

		public static void FlushPool()
		{
			BufferPool.Flush();
		}
	}
}
