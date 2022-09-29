using ProtoBuf.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ProtoBuf.Meta
{
	public class MetaType : ISerializerProxy
	{
		internal sealed class Comparer : IComparer, IComparer<MetaType>
		{
			public static readonly Comparer Default = new Comparer();

			public int Compare(object x, object y)
			{
				return Compare(x as MetaType, y as MetaType);
			}

			public int Compare(MetaType x, MetaType y)
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
				return string.Compare(x.GetSchemaTypeName(), y.GetSchemaTypeName(), StringComparison.Ordinal);
			}
		}

		[Flags]
		internal enum AttributeFamily
		{
			None = 0x0,
			ProtoBuf = 0x1,
			DataContractSerialier = 0x2,
			XmlSerializer = 0x4,
			AutoTuple = 0x8
		}

		private MetaType baseType;

		private BasicList subTypes;

		internal static readonly Type ienumerable = typeof(IEnumerable);

		private CallbackSet callbacks;

		private string name;

		private MethodInfo factory;

		private readonly RuntimeTypeModel model;

		private readonly Type type;

		private IProtoTypeSerializer serializer;

		private Type constructType;

		private Type surrogate;

		private readonly BasicList fields = new BasicList();

		private const byte OPTIONS_Pending = 1;

		private const byte OPTIONS_EnumPassThru = 2;

		private const byte OPTIONS_Frozen = 4;

		private const byte OPTIONS_PrivateOnApi = 8;

		private const byte OPTIONS_SkipConstructor = 16;

		private const byte OPTIONS_AsReferenceDefault = 32;

		private const byte OPTIONS_AutoTuple = 64;

		private const byte OPTIONS_IgnoreListHandling = 128;

		private volatile byte flags;

		IProtoSerializer ISerializerProxy.Serializer => Serializer;

		public MetaType BaseType => baseType;

		internal TypeModel Model => model;

		public bool IncludeSerializerMethod
		{
			get
			{
				return !HasFlag(8);
			}
			set
			{
				SetFlag(8, !value, throwIfFrozen: true);
			}
		}

		public bool AsReferenceDefault
		{
			get
			{
				return HasFlag(32);
			}
			set
			{
				SetFlag(32, value, throwIfFrozen: true);
			}
		}

		public bool HasCallbacks
		{
			get
			{
				if (callbacks != null)
				{
					return callbacks.NonTrivial;
				}
				return false;
			}
		}

		public bool HasSubtypes
		{
			get
			{
				if (subTypes != null)
				{
					return subTypes.Count != 0;
				}
				return false;
			}
		}

		public CallbackSet Callbacks
		{
			get
			{
				if (callbacks == null)
				{
					callbacks = new CallbackSet(this);
				}
				return callbacks;
			}
		}

		private bool IsValueType => type.IsValueType;

		public string Name
		{
			get
			{
				return name;
			}
			set
			{
				ThrowIfFrozen();
				name = value;
			}
		}

		public Type Type => type;

		internal IProtoTypeSerializer Serializer
		{
			get
			{
				if (serializer == null)
				{
					int opaqueToken = 0;
					try
					{
						model.TakeLock(ref opaqueToken);
						if (serializer == null)
						{
							SetFlag(4, value: true, throwIfFrozen: false);
							serializer = BuildSerializer();
						}
					}
					finally
					{
						model.ReleaseLock(opaqueToken);
					}
				}
				return serializer;
			}
		}

		internal bool IsList => (IgnoreListHandling ? null : TypeModel.GetListItemType(model, type)) != null;

		public bool UseConstructor
		{
			get
			{
				return !HasFlag(16);
			}
			set
			{
				SetFlag(16, !value, throwIfFrozen: true);
			}
		}

		public Type ConstructType
		{
			get
			{
				return constructType;
			}
			set
			{
				ThrowIfFrozen();
				constructType = value;
			}
		}

		public ValueMember this[int fieldNumber]
		{
			get
			{
				BasicList.NodeEnumerator enumerator = fields.GetEnumerator();
				while (enumerator.MoveNext())
				{
					ValueMember valueMember = (ValueMember)enumerator.Current;
					if (valueMember.FieldNumber == fieldNumber)
					{
						return valueMember;
					}
				}
				return null;
			}
		}

		public ValueMember this[MemberInfo member]
		{
			get
			{
				if (member == null)
				{
					return null;
				}
				BasicList.NodeEnumerator enumerator = fields.GetEnumerator();
				while (enumerator.MoveNext())
				{
					ValueMember valueMember = (ValueMember)enumerator.Current;
					if (valueMember.Member == member)
					{
						return valueMember;
					}
				}
				return null;
			}
		}

		public bool EnumPassthru
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

		public bool IgnoreListHandling
		{
			get
			{
				return HasFlag(128);
			}
			set
			{
				SetFlag(128, value, throwIfFrozen: true);
			}
		}

		internal bool Pending
		{
			get
			{
				return HasFlag(1);
			}
			set
			{
				SetFlag(1, value, throwIfFrozen: false);
			}
		}

		internal IEnumerable Fields => fields;

		internal bool IsAutoTuple => HasFlag(64);

		public override string ToString()
		{
			return type.ToString();
		}

		private bool IsValidSubType(Type subType)
		{
			return type.IsAssignableFrom(subType);
		}

		public MetaType AddSubType(int fieldNumber, Type derivedType)
		{
			return AddSubType(fieldNumber, derivedType, DataFormat.Default);
		}

		public MetaType AddSubType(int fieldNumber, Type derivedType, DataFormat dataFormat)
		{
			if (derivedType == null)
			{
				throw new ArgumentNullException("derivedType");
			}
			if (fieldNumber < 1)
			{
				throw new ArgumentOutOfRangeException("fieldNumber");
			}
			if ((!type.IsClass && !type.IsInterface) || type.IsSealed)
			{
				throw new InvalidOperationException("Sub-types can only be added to non-sealed classes");
			}
			if (!IsValidSubType(derivedType))
			{
				throw new ArgumentException(derivedType.Name + " is not a valid sub-type of " + type.Name, "derivedType");
			}
			MetaType metaType = model[derivedType];
			ThrowIfFrozen();
			metaType.ThrowIfFrozen();
			SubType value = new SubType(fieldNumber, metaType, dataFormat);
			ThrowIfFrozen();
			metaType.SetBaseType(this);
			if (subTypes == null)
			{
				subTypes = new BasicList();
			}
			subTypes.Add(value);
			return this;
		}

		private void SetBaseType(MetaType baseType)
		{
			if (baseType == null)
			{
				throw new ArgumentNullException("baseType");
			}
			if (this.baseType == baseType)
			{
				return;
			}
			if (this.baseType != null)
			{
				throw new InvalidOperationException("A type can only participate in one inheritance hierarchy");
			}
			for (MetaType metaType = baseType; metaType != null; metaType = metaType.baseType)
			{
				if (metaType == this)
				{
					throw new InvalidOperationException("Cyclic inheritance is not allowed");
				}
			}
			this.baseType = baseType;
		}

		public MetaType SetCallbacks(MethodInfo beforeSerialize, MethodInfo afterSerialize, MethodInfo beforeDeserialize, MethodInfo afterDeserialize)
		{
			CallbackSet callbackSet = Callbacks;
			callbackSet.BeforeSerialize = beforeSerialize;
			callbackSet.AfterSerialize = afterSerialize;
			callbackSet.BeforeDeserialize = beforeDeserialize;
			callbackSet.AfterDeserialize = afterDeserialize;
			return this;
		}

		public MetaType SetCallbacks(string beforeSerialize, string afterSerialize, string beforeDeserialize, string afterDeserialize)
		{
			if (IsValueType)
			{
				throw new InvalidOperationException();
			}
			CallbackSet callbackSet = Callbacks;
			callbackSet.BeforeSerialize = ResolveMethod(beforeSerialize, instance: true);
			callbackSet.AfterSerialize = ResolveMethod(afterSerialize, instance: true);
			callbackSet.BeforeDeserialize = ResolveMethod(beforeDeserialize, instance: true);
			callbackSet.AfterDeserialize = ResolveMethod(afterDeserialize, instance: true);
			return this;
		}

		internal string GetSchemaTypeName()
		{
			if (surrogate != null)
			{
				return model[surrogate].GetSchemaTypeName();
			}
			if (!Helpers.IsNullOrEmpty(name))
			{
				return name;
			}
			string text = this.type.Name;
			if (this.type.IsGenericType)
			{
				StringBuilder stringBuilder = new StringBuilder(text);
				int num = text.IndexOf('`');
				if (num >= 0)
				{
					stringBuilder.Length = num;
				}
				Type[] genericArguments = this.type.GetGenericArguments();
				foreach (Type obj in genericArguments)
				{
					stringBuilder.Append('_');
					Type type = obj;
					MetaType metaType;
					if (model.GetKey(ref type) >= 0 && (metaType = model[type]) != null && metaType.surrogate == null)
					{
						stringBuilder.Append(metaType.GetSchemaTypeName());
					}
					else
					{
						stringBuilder.Append(type.Name);
					}
				}
				return stringBuilder.ToString();
			}
			return text;
		}

		public MetaType SetFactory(MethodInfo factory)
		{
			model.VerifyFactory(factory, type);
			ThrowIfFrozen();
			this.factory = factory;
			return this;
		}

		public MetaType SetFactory(string factory)
		{
			return SetFactory(ResolveMethod(factory, instance: false));
		}

		private MethodInfo ResolveMethod(string name, bool instance)
		{
			if (Helpers.IsNullOrEmpty(name))
			{
				return null;
			}
			if (!instance)
			{
				return Helpers.GetStaticMethod(type, name);
			}
			return Helpers.GetInstanceMethod(type, name);
		}

		internal static Exception InbuiltType(Type type)
		{
			return new ArgumentException("Data of this type has inbuilt behaviour, and cannot be added to a model in this way: " + type.FullName);
		}

		internal MetaType(RuntimeTypeModel model, Type type, MethodInfo factory)
		{
			this.factory = factory;
			if (model == null)
			{
				throw new ArgumentNullException("model");
			}
			if (type == null)
			{
				throw new ArgumentNullException("type");
			}
			if (model.TryGetBasicTypeSerializer(type) != null)
			{
				throw InbuiltType(type);
			}
			this.type = type;
			this.model = model;
			if (Helpers.IsEnum(type))
			{
				EnumPassthru = type.IsDefined(model.MapType(typeof(FlagsAttribute)), inherit: false);
			}
		}

		protected internal void ThrowIfFrozen()
		{
			if ((flags & 4) != 0)
			{
				throw new InvalidOperationException("The type cannot be changed once a serializer has been generated for " + type.FullName);
			}
		}

		private IProtoTypeSerializer BuildSerializer()
		{
			if (Helpers.IsEnum(type))
			{
				return new TagDecorator(1, WireType.Variant, strict: false, new EnumSerializer(type, GetEnumMap()));
			}
			Type itemType = IgnoreListHandling ? null : TypeModel.GetListItemType(model, type);
			if (itemType != null)
			{
				if (surrogate != null)
				{
					throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot use a surrogate");
				}
				if (subTypes != null && subTypes.Count != 0)
				{
					throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be subclassed");
				}
				Type defaultType = null;
				ResolveListTypes(model, type, ref itemType, ref defaultType);
				ValueMember valueMember = new ValueMember(model, 1, type, itemType, defaultType, DataFormat.Default);
				return new TypeSerializer(model, type, new int[1]
				{
					1
				}, new IProtoSerializer[1]
				{
					valueMember.Serializer
				}, null, isRootType: true, useConstructor: true, null, constructType, factory);
			}
			if (surrogate != null)
			{
				MetaType metaType = model[surrogate];
				MetaType metaType2;
				while ((metaType2 = metaType.baseType) != null)
				{
					metaType = metaType2;
				}
				return new SurrogateSerializer(model, type, surrogate, metaType.Serializer);
			}
			if (IsAutoTuple)
			{
				MemberInfo[] mappedMembers;
				ConstructorInfo constructorInfo = ResolveTupleConstructor(type, out mappedMembers);
				if (constructorInfo == null)
				{
					throw new InvalidOperationException();
				}
				return new TupleSerializer(model, constructorInfo, mappedMembers);
			}
			fields.Trim();
			int count = fields.Count;
			int num = (subTypes != null) ? subTypes.Count : 0;
			int[] array = new int[count + num];
			IProtoSerializer[] array2 = new IProtoSerializer[count + num];
			int num2 = 0;
			if (num != 0)
			{
				BasicList.NodeEnumerator enumerator = subTypes.GetEnumerator();
				while (enumerator.MoveNext())
				{
					SubType subType = (SubType)enumerator.Current;
					if (!subType.DerivedType.IgnoreListHandling && model.MapType(ienumerable).IsAssignableFrom(subType.DerivedType.Type))
					{
						throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be used as a subclass");
					}
					array[num2] = subType.FieldNumber;
					array2[num2++] = subType.Serializer;
				}
			}
			if (count != 0)
			{
				BasicList.NodeEnumerator enumerator = fields.GetEnumerator();
				while (enumerator.MoveNext())
				{
					ValueMember valueMember2 = (ValueMember)enumerator.Current;
					array[num2] = valueMember2.FieldNumber;
					array2[num2++] = valueMember2.Serializer;
				}
			}
			BasicList basicList = null;
			for (MetaType metaType3 = BaseType; metaType3 != null; metaType3 = metaType3.BaseType)
			{
				MethodInfo methodInfo = metaType3.HasCallbacks ? metaType3.Callbacks.BeforeDeserialize : null;
				if (methodInfo != null)
				{
					if (basicList == null)
					{
						basicList = new BasicList();
					}
					basicList.Add(methodInfo);
				}
			}
			MethodInfo[] array3 = null;
			if (basicList != null)
			{
				array3 = new MethodInfo[basicList.Count];
				basicList.CopyTo(array3, 0);
				Array.Reverse(array3);
			}
			return new TypeSerializer(model, type, array, array2, array3, baseType == null, UseConstructor, callbacks, constructType, factory);
		}

		private static Type GetBaseType(MetaType type)
		{
			return type.type.BaseType;
		}

		internal static bool GetAsReferenceDefault(RuntimeTypeModel model, Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException("type");
			}
			if (Helpers.IsEnum(type))
			{
				return false;
			}
			AttributeMap[] array = AttributeMap.Create(model, type, inherit: false);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].AttributeType.FullName == "ProtoBuf.ProtoContractAttribute" && array[i].TryGet("AsReferenceDefault", out object value))
				{
					return (bool)value;
				}
			}
			return false;
		}

		internal void ApplyDefaultBehaviour()
		{
			Type type = GetBaseType(this);
			if (type != null && model.FindWithoutAdd(type) == null && GetContractFamily(model, type, null) != 0)
			{
				model.FindOrAddAuto(type, demand: true, addWithContractOnly: false, addEvenIfAutoDisabled: false);
			}
			AttributeMap[] array = AttributeMap.Create(model, this.type, inherit: false);
			AttributeFamily attributeFamily = GetContractFamily(model, this.type, array);
			if (attributeFamily == AttributeFamily.AutoTuple)
			{
				SetFlag(64, value: true, throwIfFrozen: true);
			}
			bool flag = !EnumPassthru && Helpers.IsEnum(this.type);
			if (attributeFamily == AttributeFamily.None && !flag)
			{
				return;
			}
			BasicList basicList = null;
			BasicList basicList2 = null;
			int dataMemberOffset = 0;
			int num = 1;
			bool flag2 = model.InferTagFromNameDefault;
			ImplicitFields implicitFields = ImplicitFields.None;
			string text = null;
			foreach (AttributeMap attributeMap in array)
			{
				string fullName = attributeMap.AttributeType.FullName;
				object value;
				if (!flag && fullName == "ProtoBuf.ProtoIncludeAttribute")
				{
					int fieldNumber = 0;
					if (attributeMap.TryGet("tag", out value))
					{
						fieldNumber = (int)value;
					}
					DataFormat dataFormat = DataFormat.Default;
					if (attributeMap.TryGet("DataFormat", out value))
					{
						dataFormat = (DataFormat)(int)value;
					}
					Type type2 = null;
					try
					{
						if (attributeMap.TryGet("knownTypeName", out value))
						{
							type2 = model.GetType((string)value, this.type.Assembly);
						}
						else if (attributeMap.TryGet("knownType", out value))
						{
							type2 = (Type)value;
						}
					}
					catch (Exception innerException)
					{
						throw new InvalidOperationException("Unable to resolve sub-type of: " + this.type.FullName, innerException);
					}
					if (type2 == null)
					{
						throw new InvalidOperationException("Unable to resolve sub-type of: " + this.type.FullName);
					}
					if (IsValidSubType(type2))
					{
						AddSubType(fieldNumber, type2, dataFormat);
					}
				}
				if (fullName == "ProtoBuf.ProtoPartialIgnoreAttribute" && attributeMap.TryGet("MemberName", out value) && value != null)
				{
					if (basicList == null)
					{
						basicList = new BasicList();
					}
					basicList.Add((string)value);
				}
				if (!flag && fullName == "ProtoBuf.ProtoPartialMemberAttribute")
				{
					if (basicList2 == null)
					{
						basicList2 = new BasicList();
					}
					basicList2.Add(attributeMap);
				}
				if (fullName == "ProtoBuf.ProtoContractAttribute")
				{
					if (attributeMap.TryGet("Name", out value))
					{
						text = (string)value;
					}
					if (Helpers.IsEnum(this.type))
					{
						if (attributeMap.TryGet("EnumPassthruHasValue", publicOnly: false, out value) && (bool)value && attributeMap.TryGet("EnumPassthru", out value))
						{
							EnumPassthru = (bool)value;
							if (EnumPassthru)
							{
								flag = false;
							}
						}
					}
					else
					{
						if (attributeMap.TryGet("DataMemberOffset", out value))
						{
							dataMemberOffset = (int)value;
						}
						if (attributeMap.TryGet("InferTagFromNameHasValue", publicOnly: false, out value) && (bool)value && attributeMap.TryGet("InferTagFromName", out value))
						{
							flag2 = (bool)value;
						}
						if (attributeMap.TryGet("ImplicitFields", out value) && value != null)
						{
							implicitFields = (ImplicitFields)(int)value;
						}
						if (attributeMap.TryGet("SkipConstructor", out value))
						{
							UseConstructor = !(bool)value;
						}
						if (attributeMap.TryGet("IgnoreListHandling", out value))
						{
							IgnoreListHandling = (bool)value;
						}
						if (attributeMap.TryGet("AsReferenceDefault", out value))
						{
							AsReferenceDefault = (bool)value;
						}
						if (attributeMap.TryGet("ImplicitFirstTag", out value) && (int)value > 0)
						{
							num = (int)value;
						}
					}
				}
				if (fullName == "System.Runtime.Serialization.DataContractAttribute" && text == null && attributeMap.TryGet("Name", out value))
				{
					text = (string)value;
				}
				if (fullName == "System.Xml.Serialization.XmlTypeAttribute" && text == null && attributeMap.TryGet("TypeName", out value))
				{
					text = (string)value;
				}
			}
			if (!Helpers.IsNullOrEmpty(text))
			{
				Name = text;
			}
			if (implicitFields != 0)
			{
				attributeFamily &= AttributeFamily.ProtoBuf;
			}
			MethodInfo[] array2 = null;
			BasicList basicList3 = new BasicList();
			MemberInfo[] members = this.type.GetMembers(flag ? (BindingFlags.Static | BindingFlags.Public) : (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			foreach (MemberInfo memberInfo in members)
			{
				if (memberInfo.DeclaringType != this.type || memberInfo.IsDefined(model.MapType(typeof(ProtoIgnoreAttribute)), inherit: true) || (basicList != null && basicList.Contains(memberInfo.Name)))
				{
					continue;
				}
				bool forced = false;
				PropertyInfo propertyInfo;
				FieldInfo fieldInfo;
				MethodInfo methodInfo;
				if ((propertyInfo = (memberInfo as PropertyInfo)) != null)
				{
					if (!flag)
					{
						Type effectiveType = propertyInfo.PropertyType;
						bool isPublic = Helpers.GetGetMethod(propertyInfo, nonPublic: false, allowInternal: false) != null;
						bool isField = false;
						ApplyDefaultBehaviour_AddMembers(model, attributeFamily, flag, basicList2, dataMemberOffset, flag2, implicitFields, basicList3, memberInfo, ref forced, isPublic, isField, ref effectiveType);
					}
				}
				else if ((fieldInfo = (memberInfo as FieldInfo)) != null)
				{
					Type effectiveType = fieldInfo.FieldType;
					bool isPublic = fieldInfo.IsPublic;
					bool isField = true;
					if (!flag || fieldInfo.IsStatic)
					{
						ApplyDefaultBehaviour_AddMembers(model, attributeFamily, flag, basicList2, dataMemberOffset, flag2, implicitFields, basicList3, memberInfo, ref forced, isPublic, isField, ref effectiveType);
					}
				}
				else if ((methodInfo = (memberInfo as MethodInfo)) != null && !flag)
				{
					AttributeMap[] array3 = AttributeMap.Create(model, methodInfo, inherit: false);
					if (array3 != null && array3.Length != 0)
					{
						CheckForCallback(methodInfo, array3, "ProtoBuf.ProtoBeforeSerializationAttribute", ref array2, 0);
						CheckForCallback(methodInfo, array3, "ProtoBuf.ProtoAfterSerializationAttribute", ref array2, 1);
						CheckForCallback(methodInfo, array3, "ProtoBuf.ProtoBeforeDeserializationAttribute", ref array2, 2);
						CheckForCallback(methodInfo, array3, "ProtoBuf.ProtoAfterDeserializationAttribute", ref array2, 3);
						CheckForCallback(methodInfo, array3, "System.Runtime.Serialization.OnSerializingAttribute", ref array2, 4);
						CheckForCallback(methodInfo, array3, "System.Runtime.Serialization.OnSerializedAttribute", ref array2, 5);
						CheckForCallback(methodInfo, array3, "System.Runtime.Serialization.OnDeserializingAttribute", ref array2, 6);
						CheckForCallback(methodInfo, array3, "System.Runtime.Serialization.OnDeserializedAttribute", ref array2, 7);
					}
				}
			}
			ProtoMemberAttribute[] array4 = new ProtoMemberAttribute[basicList3.Count];
			basicList3.CopyTo(array4, 0);
			ProtoMemberAttribute[] array5;
			if (flag2 || implicitFields != 0)
			{
				Array.Sort(array4);
				int num2 = num;
				array5 = array4;
				foreach (ProtoMemberAttribute protoMemberAttribute in array5)
				{
					if (!protoMemberAttribute.TagIsPinned)
					{
						protoMemberAttribute.Rebase(num2++);
					}
				}
			}
			array5 = array4;
			foreach (ProtoMemberAttribute normalizedAttribute in array5)
			{
				ValueMember valueMember = ApplyDefaultBehaviour(flag, normalizedAttribute);
				if (valueMember != null)
				{
					Add(valueMember);
				}
			}
			if (array2 != null)
			{
				SetCallbacks(Coalesce(array2, 0, 4), Coalesce(array2, 1, 5), Coalesce(array2, 2, 6), Coalesce(array2, 3, 7));
			}
		}

		private static void ApplyDefaultBehaviour_AddMembers(TypeModel model, AttributeFamily family, bool isEnum, BasicList partialMembers, int dataMemberOffset, bool inferTagByName, ImplicitFields implicitMode, BasicList members, MemberInfo member, ref bool forced, bool isPublic, bool isField, ref Type effectiveType)
		{
			switch (implicitMode)
			{
			case ImplicitFields.AllFields:
				if (isField)
				{
					forced = true;
				}
				break;
			case ImplicitFields.AllPublic:
				if (isPublic)
				{
					forced = true;
				}
				break;
			}
			if (effectiveType.IsSubclassOf(model.MapType(typeof(Delegate))))
			{
				effectiveType = null;
			}
			if (effectiveType != null)
			{
				ProtoMemberAttribute protoMemberAttribute = NormalizeProtoMember(model, member, family, forced, isEnum, partialMembers, dataMemberOffset, inferTagByName);
				if (protoMemberAttribute != null)
				{
					members.Add(protoMemberAttribute);
				}
			}
		}

		private static MethodInfo Coalesce(MethodInfo[] arr, int x, int y)
		{
			MethodInfo methodInfo = arr[x];
			if (methodInfo == null)
			{
				methodInfo = arr[y];
			}
			return methodInfo;
		}

		internal static AttributeFamily GetContractFamily(RuntimeTypeModel model, Type type, AttributeMap[] attributes)
		{
			AttributeFamily attributeFamily = AttributeFamily.None;
			if (attributes == null)
			{
				attributes = AttributeMap.Create(model, type, inherit: false);
			}
			for (int i = 0; i < attributes.Length; i++)
			{
				switch (attributes[i].AttributeType.FullName)
				{
				case "ProtoBuf.ProtoContractAttribute":
				{
					bool value = false;
					GetFieldBoolean(ref value, attributes[i], "UseProtoMembersOnly");
					if (value)
					{
						return AttributeFamily.ProtoBuf;
					}
					attributeFamily |= AttributeFamily.ProtoBuf;
					break;
				}
				case "System.Xml.Serialization.XmlTypeAttribute":
					if (!model.AutoAddProtoContractTypesOnly)
					{
						attributeFamily |= AttributeFamily.XmlSerializer;
					}
					break;
				case "System.Runtime.Serialization.DataContractAttribute":
					if (!model.AutoAddProtoContractTypesOnly)
					{
						attributeFamily |= AttributeFamily.DataContractSerialier;
					}
					break;
				}
			}
			if (attributeFamily == AttributeFamily.None && ResolveTupleConstructor(type, out MemberInfo[] _) != null)
			{
				attributeFamily |= AttributeFamily.AutoTuple;
			}
			return attributeFamily;
		}

		internal static ConstructorInfo ResolveTupleConstructor(Type type, out MemberInfo[] mappedMembers)
		{
			mappedMembers = null;
			if (type == null)
			{
				throw new ArgumentNullException("type");
			}
			if (type.IsAbstract)
			{
				return null;
			}
			ConstructorInfo[] constructors = Helpers.GetConstructors(type, nonPublic: false);
			if (constructors.Length == 0 || (constructors.Length == 1 && constructors[0].GetParameters().Length == 0))
			{
				return null;
			}
			MemberInfo[] instanceFieldsAndProperties = Helpers.GetInstanceFieldsAndProperties(type, publicOnly: true);
			BasicList basicList = new BasicList();
			for (int i = 0; i < instanceFieldsAndProperties.Length; i++)
			{
				PropertyInfo propertyInfo = instanceFieldsAndProperties[i] as PropertyInfo;
				if (propertyInfo != null)
				{
					if (!propertyInfo.CanRead)
					{
						return null;
					}
					if (propertyInfo.CanWrite && Helpers.GetSetMethod(propertyInfo, nonPublic: false, allowInternal: false) != null)
					{
						return null;
					}
					basicList.Add(propertyInfo);
					continue;
				}
				FieldInfo fieldInfo = instanceFieldsAndProperties[i] as FieldInfo;
				if (fieldInfo != null)
				{
					if (!fieldInfo.IsInitOnly)
					{
						return null;
					}
					basicList.Add(fieldInfo);
				}
			}
			if (basicList.Count == 0)
			{
				return null;
			}
			MemberInfo[] array = new MemberInfo[basicList.Count];
			basicList.CopyTo(array, 0);
			int[] array2 = new int[array.Length];
			int num = 0;
			ConstructorInfo result = null;
			mappedMembers = new MemberInfo[array2.Length];
			for (int j = 0; j < constructors.Length; j++)
			{
				ParameterInfo[] parameters = constructors[j].GetParameters();
				if (parameters.Length != array.Length)
				{
					continue;
				}
				for (int k = 0; k < array2.Length; k++)
				{
					array2[k] = -1;
				}
				for (int l = 0; l < parameters.Length; l++)
				{
					string b = parameters[l].Name.ToLower();
					for (int m = 0; m < array.Length; m++)
					{
						if (!(array[m].Name.ToLower() != b) && Helpers.GetMemberType(array[m]) == parameters[l].ParameterType)
						{
							array2[l] = m;
						}
					}
				}
				bool flag = false;
				for (int n = 0; n < array2.Length; n++)
				{
					if (array2[n] < 0)
					{
						flag = true;
						break;
					}
					mappedMembers[n] = array[array2[n]];
				}
				if (!flag)
				{
					num++;
					result = constructors[j];
				}
			}
			if (num != 1)
			{
				return null;
			}
			return result;
		}

		private static void CheckForCallback(MethodInfo method, AttributeMap[] attributes, string callbackTypeName, ref MethodInfo[] callbacks, int index)
		{
			int num = 0;
			while (true)
			{
				if (num >= attributes.Length)
				{
					return;
				}
				if (attributes[num].AttributeType.FullName == callbackTypeName)
				{
					if (callbacks == null)
					{
						callbacks = new MethodInfo[8];
					}
					else if (callbacks[index] != null)
					{
						break;
					}
					callbacks[index] = method;
				}
				num++;
			}
			Type reflectedType = method.ReflectedType;
			throw new ProtoException("Duplicate " + callbackTypeName + " callbacks on " + reflectedType.FullName);
		}

		private static bool HasFamily(AttributeFamily value, AttributeFamily required)
		{
			return (value & required) == required;
		}

		private static ProtoMemberAttribute NormalizeProtoMember(TypeModel model, MemberInfo member, AttributeFamily family, bool forced, bool isEnum, BasicList partialMembers, int dataMemberOffset, bool inferByTagName)
		{
			if (member == null || (family == AttributeFamily.None && !isEnum))
			{
				return null;
			}
			int value = int.MinValue;
			int num = (!inferByTagName) ? 1 : (-1);
			string text = null;
			bool value2 = false;
			bool ignore = false;
			bool flag = false;
			bool value3 = false;
			bool value4 = false;
			bool value5 = false;
			bool value6 = false;
			bool tagIsPinned = false;
			bool value7 = false;
			DataFormat value8 = DataFormat.Default;
			if (isEnum)
			{
				forced = true;
			}
			AttributeMap[] attribs = AttributeMap.Create(model, member, inherit: true);
			if (isEnum)
			{
				AttributeMap attribute = GetAttribute(attribs, "ProtoBuf.ProtoIgnoreAttribute");
				if (attribute != null)
				{
					ignore = true;
				}
				else
				{
					attribute = GetAttribute(attribs, "ProtoBuf.ProtoEnumAttribute");
					value = Convert.ToInt32(((FieldInfo)member).GetRawConstantValue());
					if (attribute != null)
					{
						GetFieldName(ref text, attribute, "Name");
						if ((bool)Helpers.GetInstanceMethod(attribute.AttributeType, "HasValue").Invoke(attribute.Target, null) && attribute.TryGet("Value", out object value9))
						{
							value = (int)value9;
						}
					}
				}
				flag = true;
			}
			if (!ignore && !flag)
			{
				AttributeMap attribute = GetAttribute(attribs, "ProtoBuf.ProtoMemberAttribute");
				GetIgnore(ref ignore, attribute, attribs, "ProtoBuf.ProtoIgnoreAttribute");
				if (!ignore && attribute != null)
				{
					GetFieldNumber(ref value, attribute, "Tag");
					GetFieldName(ref text, attribute, "Name");
					GetFieldBoolean(ref value3, attribute, "IsRequired");
					GetFieldBoolean(ref value2, attribute, "IsPacked");
					GetFieldBoolean(ref value7, attribute, "OverwriteList");
					GetDataFormat(ref value8, attribute, "DataFormat");
					GetFieldBoolean(ref value5, attribute, "AsReferenceHasValue", publicOnly: false);
					if (value5)
					{
						value5 = GetFieldBoolean(ref value4, attribute, "AsReference", publicOnly: true);
					}
					GetFieldBoolean(ref value6, attribute, "DynamicType");
					flag = (tagIsPinned = (value > 0));
				}
				if (!flag && partialMembers != null)
				{
					BasicList.NodeEnumerator enumerator = partialMembers.GetEnumerator();
					while (enumerator.MoveNext())
					{
						AttributeMap attributeMap = (AttributeMap)enumerator.Current;
						if (attributeMap.TryGet("MemberName", out object value10) && (string)value10 == member.Name)
						{
							GetFieldNumber(ref value, attributeMap, "Tag");
							GetFieldName(ref text, attributeMap, "Name");
							GetFieldBoolean(ref value3, attributeMap, "IsRequired");
							GetFieldBoolean(ref value2, attributeMap, "IsPacked");
							GetFieldBoolean(ref value7, attribute, "OverwriteList");
							GetDataFormat(ref value8, attributeMap, "DataFormat");
							GetFieldBoolean(ref value5, attribute, "AsReferenceHasValue", publicOnly: false);
							if (value5)
							{
								value5 = GetFieldBoolean(ref value4, attributeMap, "AsReference", publicOnly: true);
							}
							GetFieldBoolean(ref value6, attributeMap, "DynamicType");
							if (flag = (tagIsPinned = (value > 0)))
							{
								break;
							}
						}
					}
				}
			}
			if (!ignore && !flag && HasFamily(family, AttributeFamily.DataContractSerialier))
			{
				AttributeMap attribute = GetAttribute(attribs, "System.Runtime.Serialization.DataMemberAttribute");
				if (attribute != null)
				{
					GetFieldNumber(ref value, attribute, "Order");
					GetFieldName(ref text, attribute, "Name");
					GetFieldBoolean(ref value3, attribute, "IsRequired");
					flag = (value >= num);
					if (flag)
					{
						value += dataMemberOffset;
					}
				}
			}
			if (!ignore && !flag && HasFamily(family, AttributeFamily.XmlSerializer))
			{
				AttributeMap attribute = GetAttribute(attribs, "System.Xml.Serialization.XmlElementAttribute");
				if (attribute == null)
				{
					attribute = GetAttribute(attribs, "System.Xml.Serialization.XmlArrayAttribute");
				}
				GetIgnore(ref ignore, attribute, attribs, "System.Xml.Serialization.XmlIgnoreAttribute");
				if (attribute != null && !ignore)
				{
					GetFieldNumber(ref value, attribute, "Order");
					GetFieldName(ref text, attribute, "ElementName");
					flag = (value >= num);
				}
			}
			if (!ignore && !flag && GetAttribute(attribs, "System.NonSerializedAttribute") != null)
			{
				ignore = true;
			}
			if (ignore || (value < num && !forced))
			{
				return null;
			}
			return new ProtoMemberAttribute(value, forced || inferByTagName)
			{
				AsReference = value4,
				AsReferenceHasValue = value5,
				DataFormat = value8,
				DynamicType = value6,
				IsPacked = value2,
				OverwriteList = value7,
				IsRequired = value3,
				Name = (Helpers.IsNullOrEmpty(text) ? member.Name : text),
				Member = member,
				TagIsPinned = tagIsPinned
			};
		}

		private ValueMember ApplyDefaultBehaviour(bool isEnum, ProtoMemberAttribute normalizedAttribute)
		{
			MemberInfo member;
			if (normalizedAttribute == null || (member = normalizedAttribute.Member) == null)
			{
				return null;
			}
			Type memberType = Helpers.GetMemberType(member);
			Type itemType = null;
			Type defaultType = null;
			ResolveListTypes(model, memberType, ref itemType, ref defaultType);
			if (itemType != null && model.FindOrAddAuto(memberType, demand: false, addWithContractOnly: true, addEvenIfAutoDisabled: false) >= 0 && model[memberType].IgnoreListHandling)
			{
				itemType = null;
				defaultType = null;
			}
			AttributeMap[] attribs = AttributeMap.Create(model, member, inherit: true);
			object defaultValue = null;
			if (model.UseImplicitZeroDefaults)
			{
				switch (Helpers.GetTypeCode(memberType))
				{
				case ProtoTypeCode.Boolean:
					defaultValue = false;
					break;
				case ProtoTypeCode.Decimal:
					defaultValue = 0m;
					break;
				case ProtoTypeCode.Single:
					defaultValue = 0f;
					break;
				case ProtoTypeCode.Double:
					defaultValue = 0.0;
					break;
				case ProtoTypeCode.Byte:
					defaultValue = (byte)0;
					break;
				case ProtoTypeCode.Char:
					defaultValue = '\0';
					break;
				case ProtoTypeCode.Int16:
					defaultValue = (short)0;
					break;
				case ProtoTypeCode.Int32:
					defaultValue = 0;
					break;
				case ProtoTypeCode.Int64:
					defaultValue = 0L;
					break;
				case ProtoTypeCode.SByte:
					defaultValue = (sbyte)0;
					break;
				case ProtoTypeCode.UInt16:
					defaultValue = (ushort)0;
					break;
				case ProtoTypeCode.UInt32:
					defaultValue = 0u;
					break;
				case ProtoTypeCode.UInt64:
					defaultValue = 0uL;
					break;
				case ProtoTypeCode.TimeSpan:
					defaultValue = TimeSpan.Zero;
					break;
				case ProtoTypeCode.Guid:
					defaultValue = Guid.Empty;
					break;
				}
			}
			AttributeMap attribute;
			if ((attribute = GetAttribute(attribs, "System.ComponentModel.DefaultValueAttribute")) != null && attribute.TryGet("Value", out object value))
			{
				defaultValue = value;
			}
			ValueMember valueMember = (isEnum || normalizedAttribute.Tag > 0) ? new ValueMember(model, type, normalizedAttribute.Tag, member, memberType, itemType, defaultType, normalizedAttribute.DataFormat, defaultValue) : null;
			if (valueMember != null)
			{
				Type declaringType = type;
				PropertyInfo propertyInfo = Helpers.GetProperty(declaringType, member.Name + "Specified", nonPublic: true);
				MethodInfo getMethod = Helpers.GetGetMethod(propertyInfo, nonPublic: true, allowInternal: true);
				if (getMethod == null || getMethod.IsStatic)
				{
					propertyInfo = null;
				}
				if (propertyInfo != null)
				{
					valueMember.SetSpecified(getMethod, Helpers.GetSetMethod(propertyInfo, nonPublic: true, allowInternal: true));
				}
				else
				{
					MethodInfo instanceMethod = Helpers.GetInstanceMethod(declaringType, "ShouldSerialize" + member.Name, Helpers.EmptyTypes);
					if (instanceMethod != null && instanceMethod.ReturnType == model.MapType(typeof(bool)))
					{
						valueMember.SetSpecified(instanceMethod, null);
					}
				}
				if (!Helpers.IsNullOrEmpty(normalizedAttribute.Name))
				{
					valueMember.SetName(normalizedAttribute.Name);
				}
				valueMember.IsPacked = normalizedAttribute.IsPacked;
				valueMember.IsRequired = normalizedAttribute.IsRequired;
				valueMember.OverwriteList = normalizedAttribute.OverwriteList;
				if (normalizedAttribute.AsReferenceHasValue)
				{
					valueMember.AsReference = normalizedAttribute.AsReference;
				}
				valueMember.DynamicType = normalizedAttribute.DynamicType;
			}
			return valueMember;
		}

		private static void GetDataFormat(ref DataFormat value, AttributeMap attrib, string memberName)
		{
			if (attrib != null && value == DataFormat.Default && attrib.TryGet(memberName, out object value2) && value2 != null)
			{
				value = (DataFormat)value2;
			}
		}

		private static void GetIgnore(ref bool ignore, AttributeMap attrib, AttributeMap[] attribs, string fullName)
		{
			if (!ignore && attrib != null)
			{
				ignore = (GetAttribute(attribs, fullName) != null);
			}
		}

		private static void GetFieldBoolean(ref bool value, AttributeMap attrib, string memberName)
		{
			GetFieldBoolean(ref value, attrib, memberName, publicOnly: true);
		}

		private static bool GetFieldBoolean(ref bool value, AttributeMap attrib, string memberName, bool publicOnly)
		{
			if (attrib == null)
			{
				return false;
			}
			if (value)
			{
				return true;
			}
			if (attrib.TryGet(memberName, publicOnly, out object value2) && value2 != null)
			{
				value = (bool)value2;
				return true;
			}
			return false;
		}

		private static void GetFieldNumber(ref int value, AttributeMap attrib, string memberName)
		{
			if (attrib != null && value <= 0 && attrib.TryGet(memberName, out object value2) && value2 != null)
			{
				value = (int)value2;
			}
		}

		private static void GetFieldName(ref string name, AttributeMap attrib, string memberName)
		{
			if (attrib != null && Helpers.IsNullOrEmpty(name) && attrib.TryGet(memberName, out object value) && value != null)
			{
				name = (string)value;
			}
		}

		private static AttributeMap GetAttribute(AttributeMap[] attribs, string fullName)
		{
			foreach (AttributeMap attributeMap in attribs)
			{
				if (attributeMap != null && attributeMap.AttributeType.FullName == fullName)
				{
					return attributeMap;
				}
			}
			return null;
		}

		public MetaType Add(int fieldNumber, string memberName)
		{
			AddField(fieldNumber, memberName, null, null, null);
			return this;
		}

		public ValueMember AddField(int fieldNumber, string memberName)
		{
			return AddField(fieldNumber, memberName, null, null, null);
		}

		public MetaType Add(string memberName)
		{
			Add(GetNextFieldNumber(), memberName);
			return this;
		}

		public void SetSurrogate(Type surrogateType)
		{
			if (surrogateType == type)
			{
				surrogateType = null;
			}
			if (surrogateType != null && surrogateType != null && Helpers.IsAssignableFrom(model.MapType(typeof(IEnumerable)), surrogateType))
			{
				throw new ArgumentException("Repeated data (a list, collection, etc) has inbuilt behaviour and cannot be used as a surrogate");
			}
			ThrowIfFrozen();
			surrogate = surrogateType;
		}

		internal MetaType GetSurrogateOrSelf()
		{
			if (surrogate != null)
			{
				return model[surrogate];
			}
			return this;
		}

		internal MetaType GetSurrogateOrBaseOrSelf(bool deep)
		{
			if (surrogate != null)
			{
				return model[surrogate];
			}
			MetaType metaType = baseType;
			if (metaType != null)
			{
				if (deep)
				{
					MetaType result;
					do
					{
						result = metaType;
						metaType = metaType.baseType;
					}
					while (metaType != null);
					return result;
				}
				return metaType;
			}
			return this;
		}

		private int GetNextFieldNumber()
		{
			int num = 0;
			BasicList.NodeEnumerator enumerator = fields.GetEnumerator();
			while (enumerator.MoveNext())
			{
				ValueMember valueMember = (ValueMember)enumerator.Current;
				if (valueMember.FieldNumber > num)
				{
					num = valueMember.FieldNumber;
				}
			}
			if (subTypes != null)
			{
				enumerator = subTypes.GetEnumerator();
				while (enumerator.MoveNext())
				{
					SubType subType = (SubType)enumerator.Current;
					if (subType.FieldNumber > num)
					{
						num = subType.FieldNumber;
					}
				}
			}
			return num + 1;
		}

		public MetaType Add(params string[] memberNames)
		{
			if (memberNames == null)
			{
				throw new ArgumentNullException("memberNames");
			}
			int nextFieldNumber = GetNextFieldNumber();
			for (int i = 0; i < memberNames.Length; i++)
			{
				Add(nextFieldNumber++, memberNames[i]);
			}
			return this;
		}

		public MetaType Add(int fieldNumber, string memberName, object defaultValue)
		{
			AddField(fieldNumber, memberName, null, null, defaultValue);
			return this;
		}

		public MetaType Add(int fieldNumber, string memberName, Type itemType, Type defaultType)
		{
			AddField(fieldNumber, memberName, itemType, defaultType, null);
			return this;
		}

		public ValueMember AddField(int fieldNumber, string memberName, Type itemType, Type defaultType)
		{
			return AddField(fieldNumber, memberName, itemType, defaultType, null);
		}

		private ValueMember AddField(int fieldNumber, string memberName, Type itemType, Type defaultType, object defaultValue)
		{
			MemberInfo memberInfo = null;
			MemberInfo[] member = type.GetMember(memberName, Helpers.IsEnum(type) ? (BindingFlags.Static | BindingFlags.Public) : (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			if (member != null && member.Length == 1)
			{
				memberInfo = member[0];
			}
			if (memberInfo == null)
			{
				throw new ArgumentException("Unable to determine member: " + memberName, "memberName");
			}
			Type memberType;
			switch (memberInfo.MemberType)
			{
			case MemberTypes.Field:
				memberType = ((FieldInfo)memberInfo).FieldType;
				break;
			case MemberTypes.Property:
				memberType = ((PropertyInfo)memberInfo).PropertyType;
				break;
			default:
				throw new NotSupportedException(memberInfo.MemberType.ToString());
			}
			ResolveListTypes(model, memberType, ref itemType, ref defaultType);
			ValueMember valueMember = new ValueMember(model, type, fieldNumber, memberInfo, memberType, itemType, defaultType, DataFormat.Default, defaultValue);
			Add(valueMember);
			return valueMember;
		}

		internal static void ResolveListTypes(TypeModel model, Type type, ref Type itemType, ref Type defaultType)
		{
			if (type == null)
			{
				return;
			}
			if (type.IsArray)
			{
				if (type.GetArrayRank() != 1)
				{
					throw new NotSupportedException("Multi-dimension arrays are supported");
				}
				itemType = type.GetElementType();
				if (itemType == model.MapType(typeof(byte)))
				{
					defaultType = (itemType = null);
				}
				else
				{
					defaultType = type;
				}
			}
			if (itemType == null)
			{
				itemType = TypeModel.GetListItemType(model, type);
			}
			if (itemType != null)
			{
				Type itemType2 = null;
				Type defaultType2 = null;
				ResolveListTypes(model, itemType, ref itemType2, ref defaultType2);
				if (itemType2 != null)
				{
					throw TypeModel.CreateNestedListsNotSupported();
				}
			}
			if (itemType == null || defaultType != null)
			{
				return;
			}
			if (type.IsClass && !type.IsAbstract && Helpers.GetConstructor(type, Helpers.EmptyTypes, nonPublic: true) != null)
			{
				defaultType = type;
			}
			if (defaultType == null && type.IsInterface)
			{
				Type[] genericArguments;
				if (type.IsGenericType && type.GetGenericTypeDefinition() == model.MapType(typeof(IDictionary<, >)) && itemType == model.MapType(typeof(KeyValuePair<, >)).MakeGenericType(genericArguments = type.GetGenericArguments()))
				{
					defaultType = model.MapType(typeof(Dictionary<, >)).MakeGenericType(genericArguments);
				}
				else
				{
					defaultType = model.MapType(typeof(List<>)).MakeGenericType(itemType);
				}
			}
			if (defaultType != null && !Helpers.IsAssignableFrom(type, defaultType))
			{
				defaultType = null;
			}
		}

		private void Add(ValueMember member)
		{
			int opaqueToken = 0;
			try
			{
				model.TakeLock(ref opaqueToken);
				ThrowIfFrozen();
				fields.Add(member);
			}
			finally
			{
				model.ReleaseLock(opaqueToken);
			}
		}

		public ValueMember[] GetFields()
		{
			ValueMember[] array = new ValueMember[fields.Count];
			fields.CopyTo(array, 0);
			Array.Sort(array, ValueMember.Comparer.Default);
			return array;
		}

		public SubType[] GetSubtypes()
		{
			if (subTypes == null || subTypes.Count == 0)
			{
				return new SubType[0];
			}
			SubType[] array = new SubType[subTypes.Count];
			subTypes.CopyTo(array, 0);
			Array.Sort(array, SubType.Comparer.Default);
			return array;
		}

		internal bool IsDefined(int fieldNumber)
		{
			BasicList.NodeEnumerator enumerator = fields.GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (((ValueMember)enumerator.Current).FieldNumber == fieldNumber)
				{
					return true;
				}
			}
			return false;
		}

		internal int GetKey(bool demand, bool getBaseKey)
		{
			return model.GetKey(type, demand, getBaseKey);
		}

		internal EnumSerializer.EnumPair[] GetEnumMap()
		{
			if (HasFlag(2))
			{
				return null;
			}
			EnumSerializer.EnumPair[] array = new EnumSerializer.EnumPair[fields.Count];
			for (int i = 0; i < array.Length; i++)
			{
				ValueMember valueMember = (ValueMember)fields[i];
				int fieldNumber = valueMember.FieldNumber;
				object rawEnumValue = valueMember.GetRawEnumValue();
				array[i] = new EnumSerializer.EnumPair(fieldNumber, rawEnumValue, valueMember.MemberType);
			}
			return array;
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

		internal static MetaType GetRootType(MetaType source)
		{
			while (source.serializer != null)
			{
				MetaType metaType = source.baseType;
				if (metaType == null)
				{
					return source;
				}
				source = metaType;
			}
			RuntimeTypeModel runtimeTypeModel = source.model;
			int opaqueToken = 0;
			try
			{
				runtimeTypeModel.TakeLock(ref opaqueToken);
				MetaType metaType2;
				while ((metaType2 = source.baseType) != null)
				{
					source = metaType2;
				}
				return source;
			}
			finally
			{
				runtimeTypeModel.ReleaseLock(opaqueToken);
			}
		}

		internal bool IsPrepared()
		{
			return false;
		}

		internal static StringBuilder NewLine(StringBuilder builder, int indent)
		{
			return Helpers.AppendLine(builder).Append(' ', indent * 3);
		}

		internal void WriteSchema(StringBuilder builder, int indent, ref bool requiresBclImport)
		{
			if (surrogate != null)
			{
				return;
			}
			ValueMember[] array = new ValueMember[fields.Count];
			fields.CopyTo(array, 0);
			Array.Sort(array, ValueMember.Comparer.Default);
			if (IsList)
			{
				string schemaTypeName = model.GetSchemaTypeName(TypeModel.GetListItemType(model, type), DataFormat.Default, asReference: false, dynamicType: false, ref requiresBclImport);
				NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
				NewLine(builder, indent + 1).Append("repeated ").Append(schemaTypeName).Append(" items = 1;");
				NewLine(builder, indent).Append('}');
				return;
			}
			if (IsAutoTuple)
			{
				if (ResolveTupleConstructor(type, out MemberInfo[] mappedMembers) == null)
				{
					return;
				}
				NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
				for (int i = 0; i < mappedMembers.Length; i++)
				{
					Type effectiveType;
					if (mappedMembers[i] is PropertyInfo)
					{
						effectiveType = ((PropertyInfo)mappedMembers[i]).PropertyType;
					}
					else
					{
						if (!(mappedMembers[i] is FieldInfo))
						{
							throw new NotSupportedException("Unknown member type: " + mappedMembers[i].GetType().Name);
						}
						effectiveType = ((FieldInfo)mappedMembers[i]).FieldType;
					}
					NewLine(builder, indent + 1).Append("optional ").Append(model.GetSchemaTypeName(effectiveType, DataFormat.Default, asReference: false, dynamicType: false, ref requiresBclImport).Replace('.', '_')).Append(' ')
						.Append(mappedMembers[i].Name)
						.Append(" = ")
						.Append(i + 1)
						.Append(';');
				}
				NewLine(builder, indent).Append('}');
				return;
			}
			ValueMember[] array3;
			if (Helpers.IsEnum(type))
			{
				NewLine(builder, indent).Append("enum ").Append(GetSchemaTypeName()).Append(" {");
				if (array.Length == 0 && EnumPassthru)
				{
					if (type.IsDefined(model.MapType(typeof(FlagsAttribute)), inherit: false))
					{
						NewLine(builder, indent + 1).Append("// this is a composite/flags enumeration");
					}
					else
					{
						NewLine(builder, indent + 1).Append("// this enumeration will be passed as a raw value");
					}
					FieldInfo[] array2 = type.GetFields();
					foreach (FieldInfo fieldInfo in array2)
					{
						if (fieldInfo.IsStatic && fieldInfo.IsLiteral)
						{
							object rawConstantValue = fieldInfo.GetRawConstantValue();
							NewLine(builder, indent + 1).Append(fieldInfo.Name).Append(" = ").Append(rawConstantValue)
								.Append(";");
						}
					}
				}
				else
				{
					array3 = array;
					foreach (ValueMember valueMember in array3)
					{
						NewLine(builder, indent + 1).Append(valueMember.Name).Append(" = ").Append(valueMember.FieldNumber)
							.Append(';');
					}
				}
				NewLine(builder, indent).Append('}');
				return;
			}
			NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
			array3 = array;
			foreach (ValueMember valueMember2 in array3)
			{
				string value = (valueMember2.ItemType != null) ? "repeated" : (valueMember2.IsRequired ? "required" : "optional");
				NewLine(builder, indent + 1).Append(value).Append(' ');
				if (valueMember2.DataFormat == DataFormat.Group)
				{
					builder.Append("group ");
				}
				string schemaTypeName2 = valueMember2.GetSchemaTypeName(applyNetObjectProxy: true, ref requiresBclImport);
				builder.Append(schemaTypeName2).Append(" ").Append(valueMember2.Name)
					.Append(" = ")
					.Append(valueMember2.FieldNumber);
				if (valueMember2.DefaultValue != null)
				{
					if (valueMember2.DefaultValue is string)
					{
						builder.Append(" [default = \"").Append(valueMember2.DefaultValue).Append("\"]");
					}
					else if (valueMember2.DefaultValue is bool)
					{
						builder.Append(((bool)valueMember2.DefaultValue) ? " [default = true]" : " [default = false]");
					}
					else
					{
						builder.Append(" [default = ").Append(valueMember2.DefaultValue).Append(']');
					}
				}
				if (valueMember2.ItemType != null && valueMember2.IsPacked)
				{
					builder.Append(" [packed=true]");
				}
				builder.Append(';');
				if (schemaTypeName2 == "bcl.NetObjectProxy" && valueMember2.AsReference && !valueMember2.DynamicType)
				{
					builder.Append(" // reference-tracked ").Append(valueMember2.GetSchemaTypeName(applyNetObjectProxy: false, ref requiresBclImport));
				}
			}
			if (subTypes != null && subTypes.Count != 0)
			{
				NewLine(builder, indent + 1).Append("// the following represent sub-types; at most 1 should have a value");
				SubType[] array4 = new SubType[subTypes.Count];
				subTypes.CopyTo(array4, 0);
				Array.Sort(array4, SubType.Comparer.Default);
				SubType[] array5 = array4;
				foreach (SubType subType in array5)
				{
					string schemaTypeName3 = subType.DerivedType.GetSchemaTypeName();
					NewLine(builder, indent + 1).Append("optional ").Append(schemaTypeName3).Append(" ")
						.Append(schemaTypeName3)
						.Append(" = ")
						.Append(subType.FieldNumber)
						.Append(';');
				}
			}
			NewLine(builder, indent).Append('}');
		}
	}
}
