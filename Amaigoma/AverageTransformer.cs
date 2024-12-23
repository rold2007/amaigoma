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

      public AverageTransformer(int slidingWindowSize, int fullWindowsSize)
      {
         fullWindowsSize.ShouldBeGreaterThanOrEqualTo(slidingWindowSize);

         FeatureWindowSize = fullWindowsSize;
         SlidingWindowSize = slidingWindowSize;
         SlidingWindowSizeSquaredInverted = 1.0 / (slidingWindowSize * slidingWindowSize);
      }

      // TODO Use Benchmark.net to try to improve the benchmark of this method
      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         int sizeX = FeatureWindowSize;
         int sizeY = FeatureWindowSize;
         int width = sizeX + 1;
         double[] integral = list.ToArray();
         double sum;

         for (int y = 0; y <= (sizeY - SlidingWindowSize); y += SlidingWindowSize)
         {
            int topOffsetY = (width * y);
            int bottomOffsetY = width * (y + SlidingWindowSize);

            for (int x = 0; x <= (sizeX - SlidingWindowSize); x += SlidingWindowSize)
            {
               int rightX = x + SlidingWindowSize;

               // UNDONE All these indices could be precomputed in the constructor. The loop would be a lot simpler.
               sum = integral[rightX + bottomOffsetY];
               sum -= integral[x + bottomOffsetY];
               sum -= integral[rightX + topOffsetY];
               sum += integral[x + topOffsetY];

               features = features.Add(sum * SlidingWindowSizeSquaredInverted);
            }
         }

         return features;
      }
   }
}
