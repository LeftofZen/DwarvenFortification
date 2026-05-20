using System;
using System.Collections.Generic;
using System.Linq;

namespace DFCore
{
	public static class IEnumerableExtensions
	{
		// taken from https://stackoverflow.com/questions/2019417/how-to-access-random-item-in-list
		public static T TakeRandom<T>(this IEnumerable<T> source) => source.TakeRandom(1).SingleOrDefault();

		public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> source, int count) => source.Shuffle().Take(count);

		public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source) => source.OrderBy(x => Guid.NewGuid());
	}
}
