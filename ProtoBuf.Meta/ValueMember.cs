using ProtoBuf.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace ProtoBuf.Meta
{
	public class ValueMember
	{
		internal sealed class Comparer : IComparer, IComparer<ValueMember>
		{
			public static readonly Comparer Default = new Comparer();

			public int Compare(object x, object y)
			{
				return Compare(x as ValueMember, y as ValueMember);
			}

			public int Compare(ValueMember x, ValueMember y)
			{
				if (x == y)
				{
					return 0;
				}
				if (x == null)
				{
					return -1;
				}
				if (y == null)
				{
					return 1;
				}
				return x.FieldNumber.CompareTo(y.FieldNumber);
			}
		}

		private readonly int fieldNumber;

		private readonly MemberInfo member;

		private readonly Type parentType;

		private readonly Type itemType;

		private readonly Type defaultType;

		private readonly Type memberType;

		private object defaultValue;

		private readonly RuntimeTypeModel model;

		private IProtoSerializer serializer;

		private DataFormat dataFormat;

		private bool asReference;

		private bool dynamicType;

		private MethodInfo getSpecified;

		private MethodInfo setSpecified;

		private string name;

		private const byte OPTIONS_IsStrict = 1;

		private const byte OPTIONS_IsPacked = 2;

		private const byte OPTIONS_IsRequired = 4;

		private const byte OPTIONS_OverwriteList = 8;

		private const byte OPTIONS_SupportNull = 16;

		private byte flags;

		public int FieldNumber => fieldNumber;

		public MemberInfo Member => member;

		public Type ItemType => itemType;

		public Type MemberType => memberType;

		public Type DefaultType => defaultType;

		public Type ParentType => parentType;

		public object DefaultValue
		{
			get
			{
				return defaultValue;
			}
			set
			{
				ThrowIfFrozen();
				defaultValue = value;
			}
		}

		internal IProtoSerializer Serializer
		{
			get
			{
				if (serializer == null)
				{
					serializer = BuildSerializer();
				}
				return serializer;
			}
		}

		public DataFormat DataFormat
		{
			get
			{
				return dataFormat;
			}
			set
			{
				ThrowIfFrozen();
				dataFormat = value;
			}
		}

		public bool IsStrict
		{
			get
			{
				return HasFlag(1);
			}
			set
			{
				SetFlag(1, value, throwIfFrozen: true);
			}
		}

		public bool IsPacked
		{
			get
			{
				return HasFlag(2);
			}
			set
			{
				SetFlag(2, value, throwIfFrozen: true);
			}
		}

		public bool OverwriteList
		{
			get
			{
				return HasFlag(8);
			}
			set
			{
				SetFlag(8, value, throwIfFrozen: true);
			}
		}

		public bool IsRequired
		{
			get
			{
				return HasFlag(4);
			}
			set
			{
				SetFlag(4, value, throwIfFrozen: true);
			}
		}

		public bool AsReference
		{
			get
			{
				return asReference;
			}
			set
			{
				ThrowIfFrozen();
				asReference = value;
			}
		}

		public bool DynamicType
		{
			get
			{
				return dynamicType;
			}
			set
			{
				ThrowIfFrozen();
				dynamicType = value;
			}
		}

		public string Name
		{
			get
			{
				if (!Helpers.IsNullOrEmpty(name))
				{
					return name;
				}
				return member.Name;
			}
		}

		public bool SupportNull
		{
			get
			{
				return HasFlag(16);
			}
			set
			{
				SetFlag(16, value, throwIfFrozen: true);
			}
		}

		public ValueMember(RuntimeTypeModel model, Type parentType, int fieldNumber, MemberInfo member, Type memberType, Type itemType, Type defaultType, DataFormat dataFormat, object defaultValue)
			: this(model, fieldNumber, memberType, itemType, defaultType, dataFormat)
		{
			if (member == null)
			{
				throw new ArgumentNullException("member");
			}
			if (parentType == null)
			{
				throw new ArgumentNullException("parentType");
			}
			if (fieldNumber < 1 && !Helpers.IsEnum(parentType))
			{
				throw new ArgumentOutOfRangeException("fieldNumber");
			}
			this.member = member;
			this.parentType = parentType;
			if (fieldNumber < 1 && !Helpers.IsEnum(parentType))
			{
				throw new ArgumentOutOfRangeException("fieldNumber");
			}
			if (defaultValue != null && model.MapType(defaultValue.GetType()) != memberType)
			{
				defaultValue = ParseDefaultValue(memberType, defaultValue);
			}
			this.defaultValue = defaultValue;
			MetaType metaType = model.FindWithoutAdd(memberType);
			if (metaType != null)
			{
				asReference = metaType.AsReferenceDefault;
			}
			else
			{
				asReference = MetaType.GetAsReferenceDefault(model, memberType);
			}
		}

		internal ValueMember(RuntimeTypeModel model, int fieldNumber, Type memberType, Type itemType, Type defaultType, DataFormat dataFormat)
		{
			if (memberType == null)
			{
				throw new ArgumentNullException("memberType");
			}
			if (model == null)
			{
				throw new ArgumentNullException("model");
			}
			this.fieldNumber = fieldNumber;
			this.memberType = memberType;
			this.itemType = itemType;
			this.defaultType = defaultType;
			this.model = model;
			this.dataFormat = dataFormat;
		}

		internal object GetRawEnumValue()
		{
			return ((FieldInfo)member).GetRawConstantValue();
		}

		private static object ParseDefaultValue(Type type, object value)
		{
			Type underlyingType = Helpers.GetUnderlyingType(type);
			if (underlyingType != null)
			{
				type = underlyingType;
			}
			if (value is string)
			{
				string text = (string)value;
				if (Helpers.IsEnum(type))
				{
					return Helpers.ParseEnum(type, text);
				}
				switch (Helpers.GetTypeCode(type))
				{
				case ProtoTypeCode.Boolean:
					return bool.Parse(text);
				case ProtoTypeCode.Byte:
					return byte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Char:
					if (text.Length == 1)
					{
						return text[0];
					}
					throw new FormatException("Single character expected: \"" + text + "\"");
				case ProtoTypeCode.DateTime:
					return DateTime.Parse(text, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Decimal:
					return decimal.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Double:
					return double.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Int16:
					return short.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Int32:
					return int.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Int64:
					return long.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.SByte:
					return sbyte.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
				case ProtoTypeCode.Single:
					return float.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.String:
					return text;
				case ProtoTypeCode.UInt16:
					return ushort.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.UInt32:
					return uint.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.UInt64:
					return ulong.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture);
				case ProtoTypeCode.TimeSpan:
					return TimeSpan.Parse(text);
				case ProtoTypeCode.Uri:
					return text;
				case ProtoTypeCode.Guid:
					return new Guid(text);
				}
			}
			if (Helpers.IsEnum(type))
			{
				return Enum.ToObject(type, value);
			}
			return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
		}

		public void SetSpecified(MethodInfo getSpecified, MethodInfo setSpecified)
		{
			if (getSpecified != null && (getSpecified.ReturnType != model.MapType(typeof(bool)) || getSpecified.IsStatic || getSpecified.GetParameters().Length != 0))
			{
				throw new ArgumentException("Invalid pattern for checking member-specified", "getSpecified");
			}
			ParameterInfo[] parameters;
			if (setSpecified != null && (setSpecified.ReturnType != model.MapType(typeof(void)) || setSpecified.IsStatic || (parameters = setSpecified.GetParameters()).Length != 1 || parameters[0].ParameterType != model.MapType(typeof(bool))))
			{
				throw new ArgumentException("Invalid pattern for setting member-specified", "setSpecified");
			}
			ThrowIfFrozen();
			this.getSpecified = getSpecified;
			this.setSpecified = setSpecified;
		}

		private void ThrowIfFrozen()
		{
			if (serializer != null)
			{
				throw new InvalidOperationException("The type cannot be changed once a serializer has been generated");
			}
		}

		private IProtoSerializer BuildSerializer()
		{
			int opaqueToken = 0;
			try
			{
				model.TakeLock(ref opaqueToken);
				Type type = (itemType == null) ? memberType : itemType;
				IProtoSerializer protoSerializer = TryGetCoreSerializer(model, dataFormat, type, out WireType defaultWireType, asReference, dynamicType, OverwriteList, allowComplexTypes: true);
				if (protoSerializer == null)
				{
					throw new InvalidOperationException("No serializer defined for type: " + type.FullName);
				}
				if (itemType != null && SupportNull)
				{
					if (IsPacked)
					{
						throw new NotSupportedException("Packed encodings cannot support null values");
					}
					protoSerializer = new TagDecorator(1, defaultWireType, IsStrict, protoSerializer);
					protoSerializer = new NullDecorator(model, protoSerializer);
					protoSerializer = new TagDecorator(fieldNumber, WireType.StartGroup, strict: false, protoSerializer);
				}
				else
				{
					protoSerializer = new TagDecorator(fieldNumber, defaultWireType, IsStrict, protoSerializer);
				}
				if (itemType != null)
				{
					if (!SupportNull)
					{
						if (Helpers.GetUnderlyingType(itemType) == null)
						{
							_ = itemType;
						}
					}
					else
					{
						_ = itemType;
					}
					protoSerializer = ((!memberType.IsArray) ? ((ProtoDecoratorBase)ListDecorator.Create(model, memberType, defaultType, protoSerializer, fieldNumber, IsPacked, defaultWireType, member != null && PropertyDecorator.CanWrite(model, member), OverwriteList, SupportNull)) : ((ProtoDecoratorBase)new ArrayDecorator(model, protoSerializer, fieldNumber, IsPacked, defaultWireType, memberType, OverwriteList, SupportNull)));
				}
				else if (defaultValue != null && !IsRequired && getSpecified == null)
				{
					protoSerializer = new DefaultValueDecorator(model, defaultValue, protoSerializer);
				}
				if (memberType == model.MapType(typeof(Uri)))
				{
					protoSerializer = new UriDecorator(model, protoSerializer);
				}
				if (member != null)
				{
					if (member is PropertyInfo)
					{
						protoSerializer = new PropertyDecorator(model, parentType, (PropertyInfo)member, protoSerializer);
					}
					else
					{
						if (!(member is FieldInfo))
						{
							throw new InvalidOperationException();
						}
						protoSerializer = new FieldDecorator(parentType, (FieldInfo)member, protoSerializer);
					}
					if (getSpecified != null || setSpecified != null)
					{
						protoSerializer = new MemberSpecifiedDecorator(getSpecified, setSpecified, protoSerializer);
					}
				}
				return protoSerializer;
			}
			finally
			{
				model.ReleaseLock(opaqueToken);
			}
		}

		private static WireType GetIntWireType(DataFormat format, int width)
		{
			switch (format)
			{
			case DataFormat.ZigZag:
				return WireType.SignedVariant;
			case DataFormat.FixedSize:
				if (width != 32)
				{
					return WireType.Fixed64;
				}
				return WireType.Fixed32;
			case DataFormat.Default:
			case DataFormat.TwosComplement:
				return WireType.Variant;
			default:
				throw new InvalidOperationException();
			}
		}

		private static WireType GetDateTimeWireType(DataFormat format)
		{
			switch (format)
			{
			case DataFormat.Group:
				return WireType.StartGroup;
			case DataFormat.FixedSize:
				return WireType.Fixed64;
			case DataFormat.Default:
				return WireType.String;
			default:
				throw new InvalidOperationException();
			}
		}

		internal static IProtoSerializer TryGetCoreSerializer(RuntimeTypeModel model, DataFormat dataFormat, Type type, out WireType defaultWireType, bool asReference, bool dynamicType, bool overwriteList, bool allowComplexTypes)
		{
			Type underlyingType = Helpers.GetUnderlyingType(type);
			if (underlyingType != null)
			{
				type = underlyingType;
			}
			if (Helpers.IsEnum(type))
			{
				if (allowComplexTypes && model != null)
				{
					defaultWireType = WireType.Variant;
					return new EnumSerializer(type, model.GetEnumMap(type));
				}
				defaultWireType = WireType.None;
				return null;
			}
			switch (Helpers.GetTypeCode(type))
			{
			case ProtoTypeCode.Int32:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new Int32Serializer(model);
			case ProtoTypeCode.UInt32:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new UInt32Serializer(model);
			case ProtoTypeCode.Int64:
				defaultWireType = GetIntWireType(dataFormat, 64);
				return new Int64Serializer(model);
			case ProtoTypeCode.UInt64:
				defaultWireType = GetIntWireType(dataFormat, 64);
				return new UInt64Serializer(model);
			case ProtoTypeCode.String:
				defaultWireType = WireType.String;
				if (asReference)
				{
					return new NetObjectSerializer(model, model.MapType(typeof(string)), 0, BclHelpers.NetObjectOptions.AsReference);
				}
				return new StringSerializer(model);
			case ProtoTypeCode.Single:
				defaultWireType = WireType.Fixed32;
				return new SingleSerializer(model);
			case ProtoTypeCode.Double:
				defaultWireType = WireType.Fixed64;
				return new DoubleSerializer(model);
			case ProtoTypeCode.Boolean:
				defaultWireType = WireType.Variant;
				return new BooleanSerializer(model);
			case ProtoTypeCode.DateTime:
				defaultWireType = GetDateTimeWireType(dataFormat);
				return new DateTimeSerializer(model);
			case ProtoTypeCode.Decimal:
				defaultWireType = WireType.String;
				return new DecimalSerializer(model);
			case ProtoTypeCode.Byte:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new ByteSerializer(model);
			case ProtoTypeCode.SByte:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new SByteSerializer(model);
			case ProtoTypeCode.Char:
				defaultWireType = WireType.Variant;
				return new CharSerializer(model);
			case ProtoTypeCode.Int16:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new Int16Serializer(model);
			case ProtoTypeCode.UInt16:
				defaultWireType = GetIntWireType(dataFormat, 32);
				return new UInt16Serializer(model);
			case ProtoTypeCode.TimeSpan:
				defaultWireType = GetDateTimeWireType(dataFormat);
				return new TimeSpanSerializer(model);
			case ProtoTypeCode.Guid:
				defaultWireType = WireType.String;
				return new GuidSerializer(model);
			case ProtoTypeCode.Uri:
				defaultWireType = WireType.String;
				return new StringSerializer(model);
			case ProtoTypeCode.ByteArray:
				defaultWireType = WireType.String;
				return new BlobSerializer(model, overwriteList);
			case ProtoTypeCode.Type:
				defaultWireType = WireType.String;
				return new SystemTypeSerializer(model);
			default:
			{
				IProtoSerializer protoSerializer = model.AllowParseableTypes ? ParseableSerializer.TryCreate(type, model) : null;
				if (protoSerializer != null)
				{
					defaultWireType = WireType.String;
					return protoSerializer;
				}
				if (allowComplexTypes && model != null)
				{
					int key = model.GetKey(type, demand: false, getBaseKey: true);
					if (asReference || dynamicType)
					{
						defaultWireType = ((dataFormat == DataFormat.Group) ? WireType.StartGroup : WireType.String);
						BclHelpers.NetObjectOptions netObjectOptions = BclHelpers.NetObjectOptions.None;
						if (asReference)
						{
							netObjectOptions |= BclHelpers.NetObjectOptions.AsReference;
						}
						if (dynamicType)
						{
							netObjectOptions |= BclHelpers.NetObjectOptions.DynamicType;
						}
						if (key >= 0)
						{
							if (asReference && Helpers.IsValueType(type))
							{
								string str = "AsReference cannot be used with value-types";
								str = ((!(type.Name == "KeyValuePair`2")) ? (str + ": " + type.FullName) : (str + "; please see http://stackoverflow.com/q/14436606/"));
								throw new InvalidOperationException(str);
							}
							MetaType metaType = model[type];
							if (asReference && metaType.IsAutoTuple)
							{
								netObjectOptions |= BclHelpers.NetObjectOptions.LateSet;
							}
							if (metaType.UseConstructor)
							{
								netObjectOptions |= BclHelpers.NetObjectOptions.UseConstructor;
							}
						}
						return new NetObjectSerializer(model, type, key, netObjectOptions);
					}
					if (key >= 0)
					{
						defaultWireType = ((dataFormat == DataFormat.Group) ? WireType.StartGroup : WireType.String);
						return new SubItemSerializer(type, key, model[type], recursionCheck: true);
					}
				}
				defaultWireType = WireType.None;
				return null;
			}
			}
		}

		internal void SetName(string name)
		{
			ThrowIfFrozen();
			this.name = name;
		}

		private bool HasFlag(byte flag)
		{
			return (flags & flag) == flag;
		}

		private void SetFlag(byte flag, bool value, bool throwIfFrozen)
		{
			if (throwIfFrozen && HasFlag(flag) != value)
			{
				ThrowIfFrozen();
			}
			if (value)
			{
				flags |= flag;
			}
			else
			{
				flags = (byte)(flags & ~flag);
			}
		}

		internal string GetSchemaTypeName(bool applyNetObjectProxy, ref bool requiresBclImport)
		{
			Type type = ItemType;
			if (type == null)
			{
				type = MemberType;
			}
			return model.GetSchemaTypeName(type, DataFormat, applyNetObjectProxy && asReference, applyNetObjectProxy && dynamicType, ref requiresBclImport);
		}
	}
}
