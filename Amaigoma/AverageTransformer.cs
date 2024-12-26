using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;

namespace Amaigoma
{
   // TODO Use Skia to add more advanced features ?
   public sealed record AverageTransformer // ncrunch: no coverage
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
      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         double[] integral = list.ToArray();
         double sum;

         ImmutableList<double> features = ImmutableList<double>.Empty;
         int i = 0;

         while (i < IntegralIndices.Count)
         {
            sum = integral[IntegralIndices[i++]];
            sum -= integral[IntegralIndices[i++]];
            sum -= integral[IntegralIndices[i++]];
            sum += integral[IntegralIndices[i++]];

            features = features.Add(sum * SlidingWindowSizeSquaredInverted);
         }

         return features;
      }
   }
}
