using System.Text;
using System.Collections.Concurrent;

namespace Mermaider.Rendering;

internal static class SharedStringBuilderPool
{
	internal static readonly LocalStringBuilderPool Instance = new();

	internal sealed class LocalStringBuilderPool
	{
		private const int InitialCapacity = 4096;
		private const int MaximumRetainedCapacity = 64 * 1024;
		private readonly ConcurrentBag<StringBuilder> builders = new();

		internal StringBuilder Get() => builders.TryTake(out StringBuilder? builder)
			? builder
			: new StringBuilder(InitialCapacity);

		internal void Return(StringBuilder builder)
		{
			if (builder.Capacity > MaximumRetainedCapacity)
				return;
			builder.Clear();
			builders.Add(builder);
		}
	}
}
