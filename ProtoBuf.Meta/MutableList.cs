namespace ProtoBuf.Meta
{
	internal sealed class MutableList : BasicList
	{
		public new object this[int index]
		{
			get
			{
				return head[index];
			}
			set
			{
				head[index] = value;
			}
		}

		public void RemoveLast()
		{
			head.RemoveLastWithMutate();
		}

		public void Clear()
		{
			head.Clear();
		}
	}
}
