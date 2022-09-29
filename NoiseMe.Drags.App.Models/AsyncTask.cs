using System;
using System.Threading;

namespace NoiseMe.Drags.App.Models
{
	public class AsyncTask
	{
		public static TaskAction<R> StartNew<R>(TaskAction<R> function)
		{
			R retv = default(R);
			bool completed = false;
			object sync = new object();
			function.BeginInvoke(delegate(IAsyncResult iAsyncResult)
			{
				lock (sync)
				{
					completed = true;
					retv = function.EndInvoke(iAsyncResult);
					Monitor.Pulse(sync);
				}
			}, null);
			return delegate
			{
				lock (sync)
				{
					if (!completed)
					{
						Monitor.Wait(sync);
					}
					return retv;
				}
			};
		}

		public static AsyncAction StartNew(AsyncAction function)
		{
			bool completed = false;
			object sync = new object();
			function.BeginInvoke(delegate(IAsyncResult iAsyncResult)
			{
				lock (sync)
				{
					completed = true;
					function.EndInvoke(iAsyncResult);
					Monitor.Pulse(sync);
				}
			}, null);
			return delegate
			{
				lock (sync)
				{
					if (!completed)
					{
						Monitor.Wait(sync);
					}
				}
			};
		}
	}
}
