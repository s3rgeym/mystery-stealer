using ProtoBuf.Serializers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ProtoBuf.Meta
{
	public sealed class SubType
	{
		internal sealed class Comparer : IComparer, IComparer<SubType>
		{
			public static readonly Comparer Default = new Comparer();

			public int Compare(object x, object y)
			{
				return Compare(x as SubType, y as SubType);
			}

			public int Compare(SubType x, SubType y)
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

		private readonly MetaType derivedType;

		private readonly DataFormat dataFormat;

		private IProtoSerializer serializer;

		public int FieldNumber => fieldNumber;

		public MetaType DerivedType => derivedType;

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

		public SubType(int fieldNumber, MetaType derivedType, DataFormat format)
		{
			if (derivedType == null)
			{
				throw new ArgumentNullException("derivedType");
			}
			if (fieldNumber <= 0)
			{
				throw new ArgumentOutOfRangeException("fieldNumber");
			}
			this.fieldNumber = fieldNumber;
			this.derivedType = derivedType;
			dataFormat = format;
		}

		private IProtoSerializer BuildSerializer()
		{
			WireType wireType = WireType.String;
			if (dataFormat == DataFormat.Group)
			{
				wireType = WireType.StartGroup;
			}
			IProtoSerializer tail = new SubItemSerializer(derivedType.Type, derivedType.GetKey(demand: false, getBaseKey: false), derivedType, recursionCheck: false);
			return new TagDecorator(fieldNumber, wireType, strict: false, tail);
		}
	}
}
