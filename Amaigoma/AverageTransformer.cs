using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;

namespace Amaigoma
{
   // TODO Use Skia to add more advanced features ?
   // TODO Rename class to something else than "Transformer"
   public sealed record AverageTransformer
   {
      public int FeatureWindowSize
      {
         get;
         private set;
      }

      public int FeatureCount
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

         FeatureCount = IntegralIndices.Count / 4;
      }

      public IEnumerable<double> DataTransformersIndices(int featureIndex)
      {
         featureIndex *= 4;

         return [IntegralIndices[featureIndex], IntegralIndices[featureIndex + 1], IntegralIndices[featureIndex + 2], IntegralIndices[featureIndex + 3]];
      }

      public double DataTransformers(IEnumerable<double> sampledData)
      {
         // TODO This ToArray() should be removed for optimization
         double[] integral = sampledData.ToArray();

         double sum = integral[0];
         sum -= integral[1];
         sum -= integral[2];
         sum += integral[3];

         return sum * SlidingWindowSizeSquaredInverted;
      }
   }
}
