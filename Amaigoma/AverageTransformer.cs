using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;

namespace Amaigoma
{
   using DataTransformer = Func<IEnumerable<double>, double>;

   // TODO Use Skia to add more advanced features ?
   // TODO Rename class to something else than "Transformer"
   public sealed record AverageTransformer : IEnumerable<DataTransformer> // ncrunch: no coverage
   {
      public int FeatureWindowSize
      {
         get;
         private set;
      }

      private int SlidingWindowSize
      {
         get;
      }

      private double SlidingWindowSizeSquaredInverted
      {
         get;
      }

      private ImmutableList<int> IntegralIndices
      {
         get;
      }

      public AverageTransformer(int slidingWindowSize, int fullWindowsSize)
      {
         fullWindowsSize.ShouldBeGreaterThanOrEqualTo(slidingWindowSize);

         FeatureWindowSize = fullWindowsSize;
         SlidingWindowSize = slidingWindowSize;
         SlidingWindowSizeSquaredInverted = 1.0 / (slidingWindowSize * slidingWindowSize);
         IntegralIndices = ImmutableList<int>.Empty;

         int width = FeatureWindowSize + 1;

         for (int y = 0; y <= (FeatureWindowSize - SlidingWindowSize); y += SlidingWindowSize)
         {
            int topOffsetY = (width * y);
            int bottomOffsetY = width * (y + SlidingWindowSize);

            for (int x = 0; x <= (FeatureWindowSize - SlidingWindowSize); x += SlidingWindowSize)
            {
               int rightX = x + SlidingWindowSize;

               IntegralIndices = IntegralIndices.Add(x + topOffsetY);
               IntegralIndices = IntegralIndices.Add(rightX + topOffsetY);
               IntegralIndices = IntegralIndices.Add(x + bottomOffsetY);
               IntegralIndices = IntegralIndices.Add(rightX + bottomOffsetY);
            }
         }
      }

      // TODO Use Benchmark.net to try to improve the benchmark of this method
      public IEnumerator<DataTransformer> GetEnumerator()
      {
         int i = 0;

         while (i < IntegralIndices.Count)
         {
            int j = i;

            yield return (list) =>
            {
               // TODO This ToArray() should be removed for optimization
               double[] integral = list.ToArray();

               double sum = integral[IntegralIndices[j]];
               sum -= integral[IntegralIndices[j + 1]];
               sum -= integral[IntegralIndices[j + 2]];
               sum += integral[IntegralIndices[j + 3]];

               return sum * SlidingWindowSizeSquaredInverted;
            };

            i += 4;
         }
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
         return this.GetEnumerator();
      }
   }
}
