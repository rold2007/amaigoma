using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record SabotenCache // ncrunch: no coverage
   {
      private ImmutableList<double> TransformedData { get; } = ImmutableList<double>.Empty;
      private ImmutableList<bool> FetchedData { get; } = ImmutableList<bool>.Empty;

      public SabotenCache()
      {
      }

      private SabotenCache(ImmutableList<double> transformedData, ImmutableList<bool> fetchedData)
      {
         TransformedData = transformedData;
         FetchedData = fetchedData;
      }

      public SabotenCache LoadCache(int dataIndex, double newTransformedData)
      {
         ImmutableList<double> transformedData;
         ImmutableList<bool> fetchedData;

         // Validate the data only once
         newTransformedData.ShouldBeGreaterThanOrEqualTo(0.0);
         newTransformedData.ShouldBeLessThanOrEqualTo(255.0);

         if (dataIndex == TransformedData.Count)
         {
            transformedData = TransformedData.Add(newTransformedData);
            fetchedData = FetchedData.AddRange(Enumerable.Repeat(true, 1));
         }
         else if (dataIndex > TransformedData.Count)
         {
            // Fill the gap
            transformedData = TransformedData.AddRange(Enumerable.Repeat(0.0, dataIndex - TransformedData.Count));
            fetchedData = FetchedData.AddRange(Enumerable.Repeat(false, dataIndex - TransformedData.Count));

            // Fill the new data
            transformedData = transformedData.Add(newTransformedData);
            fetchedData = fetchedData.AddRange(Enumerable.Repeat(true, 1));
         }
         else
         {
            dataIndex.ShouldBeLessThan(TransformedData.Count);

            transformedData = TransformedData.RemoveRange(dataIndex, 1);
            fetchedData = FetchedData.RemoveRange(dataIndex, 1);

            transformedData = transformedData.Insert(dataIndex, newTransformedData);
            fetchedData = fetchedData.Insert(dataIndex, true);
         }

         return new SabotenCache(transformedData, fetchedData);
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
   }
}
