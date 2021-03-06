﻿namespace Amaigoma
{
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   public sealed class SabotenCache
   {
      private readonly ImmutableList<double> transformedData = ImmutableList<double>.Empty;
      private readonly ImmutableList<bool> fetchedData = ImmutableList<bool>.Empty;

      public SabotenCache(IList<double> data)
      {
         this.Data = data;
      }

      private SabotenCache(IList<double> data, ImmutableList<double> transformedData, ImmutableList<bool> fetchedData)
      {
         this.Data = data;
         this.transformedData = transformedData;

         this.fetchedData = fetchedData;
      }

      public SabotenCache LoadCache(Range range, IEnumerable<double> newTransformedData)
      {
         ImmutableList<double> transformedData;
         ImmutableList<bool> fetchedData;

         if (range.Start.Value == this.transformedData.Count)
         {
            transformedData = this.transformedData.AddRange(newTransformedData);
            fetchedData = this.fetchedData.AddRange(Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }
         else if (range.Start.Value > this.transformedData.Count)
         {
            // Fill the gap
            transformedData = this.transformedData.AddRange(Enumerable.Repeat(0.0, range.Start.Value - this.transformedData.Count));
            fetchedData = this.fetchedData.AddRange(Enumerable.Repeat(false, range.Start.Value - this.transformedData.Count));

            // Fill the new data
            transformedData = transformedData.AddRange(newTransformedData);
            fetchedData = fetchedData.AddRange(Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }
         else
         {
            range.Start.Value.ShouldBeLessThan(this.transformedData.Count);

            transformedData = this.transformedData.RemoveRange(range.Start.Value, range.End.Value - range.Start.Value);
            fetchedData = this.fetchedData.RemoveRange(range.Start.Value, range.End.Value - range.Start.Value);

            transformedData = transformedData.InsertRange(range.Start.Value, newTransformedData);
            fetchedData = fetchedData.InsertRange(range.Start.Value, Enumerable.Repeat(true, range.End.Value - range.Start.Value));
         }

         return new SabotenCache(Data, transformedData, fetchedData);
      }

      public bool CacheHit(int index)
      {
         if (index < fetchedData.Count())
         {
            return fetchedData[index];
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
            fetchedData[index].ShouldBeTrue();

            return transformedData[index];
         }
      }

      public IList<double> Data { get; }
   }
}
