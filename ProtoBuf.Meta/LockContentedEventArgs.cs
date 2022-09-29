using System;

namespace ProtoBuf.Meta
{
	public sealed class LockContentedEventArgs : EventArgs
	{
		private readonly string ownerStackTrace;

		public string OwnerStackTrace => ownerStackTrace;

		internal LockContentedEventArgs(string ownerStackTrace)
		{
			this.ownerStackTrace = ownerStackTrace;
		}
	}
}
