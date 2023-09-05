using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record SabotenCache
   {
      private ImmutableList<double> TransformedData { get; } = ImmutableList<double>.Empty;
      private ImmutableList<bool> FetchedData { get; } = ImmutableList<bool>.Empty;

      public SabotenCache(IEnumerable<double> data)
      {
         Data = data;
      }

      private SabotenCache(IEnumerable<double> data, ImmutableList<double> transformedData, ImmutableList<bool> fetchedData)
      {
         Data = data;
         TransformedData = transformedData;
         FetchedData = fetchedData;
      }

      public SabotenCache LoadCache(Range range, IEnumerable<double> newTransformedData)
      {
         ImmutableList<double> transformedData;
         ImmutableList<bool> fetchedData;

         if (range.Start.Value == TransformedData.Count)
         {
            transformedData = TransformedData.AddRange(newTransformedData);
            fetchedData = FetchedData.AddRange(Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }
         else if (range.Start.Value > TransformedData.Count)
         {
            // Fill the gap
            transformedData = TransformedData.AddRange(Enumerable.Repeat(0.0, range.Start.Value - TransformedData.Count));
            fetchedData = FetchedData.AddRange(Enumerable.Repeat(false, range.Start.Value - TransformedData.Count));

            // Fill the new data
            transformedData = transformedData.AddRange(newTransformedData);
            fetchedData = fetchedData.AddRange(Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }
         else
         {
            range.Start.Value.ShouldBeLessThan(TransformedData.Count);

            transformedData = TransformedData.RemoveRange(range.Start.Value, range.End.Value - range.Start.Value);
            fetchedData = FetchedData.RemoveRange(range.Start.Value, range.End.Value - range.Start.Value);

            transformedData = transformedData.InsertRange(range.Start.Value, newTransformedData);
            fetchedData = fetchedData.InsertRange(range.Start.Value, Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }

         return new SabotenCache(Data, transformedData, fetchedData);
      }

      public bool CacheHit(int index)
      {
         if (index < FetchedData.Count())
         {
            return FetchedData[index];
         }
         else
         {
            return false;
         }
      }

      public double this[int index]
      {
         get
         {
#if DEBUG
            CacheHit(index).ShouldBeTrue("Need to call Prefetch() first.");
#endif

            return TransformedData[index];
         }
      }

      public IEnumerable<double> Data { get; }
   }
}
