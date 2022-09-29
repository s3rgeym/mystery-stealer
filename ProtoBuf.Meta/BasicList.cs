using System;
using System.Collections;

namespace ProtoBuf.Meta
{
	internal class BasicList : IEnumerable
	{
		public struct NodeEnumerator : IEnumerator
		{
			private int position;

			private readonly Node node;

			public object Current => node[position];

			internal NodeEnumerator(Node node)
			{
				position = -1;
				this.node = node;
			}

			void IEnumerator.Reset()
			{
				position = -1;
			}

			public bool MoveNext()
			{
				int length = node.Length;
				if (position <= length)
				{
					return ++position < length;
				}
				return false;
			}
		}

		internal sealed class Node
		{
			private readonly object[] data;

			private int length;

			public object this[int index]
			{
				get
				{
					if (index >= 0 && index < length)
					{
						return data[index];
					}
					throw new ArgumentOutOfRangeException("index");
				}
				set
				{
					if (index >= 0 && index < length)
					{
						data[index] = value;
						return;
					}
					throw new ArgumentOutOfRangeException("index");
				}
			}

			public int Length => length;

			internal Node(object[] data, int length)
			{
				this.data = data;
				this.length = length;
			}

			public void RemoveLastWithMutate()
			{
				if (length == 0)
				{
					throw new InvalidOperationException();
				}
				length--;
			}

			public Node Append(object value)
			{
				int num = length + 1;
				object[] array;
				if (data == null)
				{
					array = new object[10];
				}
				else if (length == data.Length)
				{
					array = new object[data.Length * 2];
					Array.Copy(data, array, length);
				}
				else
				{
					array = data;
				}
				array[length] = value;
				return new Node(array, num);
			}

			public Node Trim()
			{
				if (length == 0 || length == data.Length)
				{
					return this;
				}
				object[] destinationArray = new object[length];
				Array.Copy(data, destinationArray, length);
				return new Node(destinationArray, length);
			}

			internal int IndexOfString(string value)
			{
				for (int i = 0; i < length; i++)
				{
					if (value == (string)data[i])
					{
						return i;
					}
				}
				return -1;
			}

			internal int IndexOfReference(object instance)
			{
				for (int i = 0; i < length; i++)
				{
					if (instance == data[i])
					{
						return i;
					}
				}
				return -1;
			}

			internal int IndexOf(MatchPredicate predicate, object ctx)
			{
				for (int i = 0; i < length; i++)
				{
					if (predicate(data[i], ctx))
					{
						return i;
					}
				}
				return -1;
			}

			internal void CopyTo(Array array, int offset)
			{
				if (length > 0)
				{
					Array.Copy(data, 0, array, offset, length);
				}
			}

			internal void Clear()
			{
				if (data != null)
				{
					Array.Clear(data, 0, data.Length);
				}
				length = 0;
			}
		}

		internal delegate bool MatchPredicate(object value, object ctx);

		internal sealed class Group
		{
			public readonly int First;

			public readonly BasicList Items;

			public Group(int first)
			{
				First = first;
				Items = new BasicList();
			}
		}

		private static readonly Node nil = new Node(null, 0);

		protected Node head = nil;

		public object this[int index] => head[index];

		public int Count => head.Length;

		public void CopyTo(Array array, int offset)
		{
			head.CopyTo(array, offset);
		}

		public int Add(object value)
		{
			return (head = head.Append(value)).Length - 1;
		}

		public void Trim()
		{
			head = head.Trim();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new NodeEnumerator(head);
		}

		public NodeEnumerator GetEnumerator()
		{
			return new NodeEnumerator(head);
		}

		internal int IndexOf(MatchPredicate predicate, object ctx)
		{
			return head.IndexOf(predicate, ctx);
		}

		internal int IndexOfString(string value)
		{
			return head.IndexOfString(value);
		}

		internal int IndexOfReference(object instance)
		{
			return head.IndexOfReference(instance);
		}

		internal bool Contains(object value)
		{
			NodeEnumerator enumerator = GetEnumerator();
			while (enumerator.MoveNext())
			{
				if (object.Equals(enumerator.Current, value))
				{
					return true;
				}
			}
			return false;
		}

		internal static BasicList GetContiguousGroups(int[] keys, object[] values)
		{
			if (keys == null)
			{
				throw new ArgumentNullException("keys");
			}
			if (values == null)
			{
				throw new ArgumentNullException("values");
			}
			if (values.Length < keys.Length)
			{
				throw new ArgumentException("Not all keys are covered by values", "values");
			}
			BasicList basicList = new BasicList();
			Group group = null;
			for (int i = 0; i < keys.Length; i++)
			{
				if (i == 0 || keys[i] != keys[i - 1])
				{
					group = null;
				}
				if (group == null)
				{
					group = new Group(keys[i]);
					basicList.Add(group);
				}
				group.Items.Add(values[i]);
			}
			return basicList;
		}
	}
}
