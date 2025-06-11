using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Shouldly;

namespace Amaigoma
{
   // TODO Use Skia to add more advanced features ?
   public sealed record AverageTransformer
   {
      public int FeatureCount
      {
         get;
         private set;
      }

      public int SlidingWindowSize
      {
         get;
      }

      public int SlidingWindowHalfSize
      {
         get;
      }

      public int SlidingWindowSizePlusOne
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

         SlidingWindowHalfSize = slidingWindowSize / 2;
         SlidingWindowSizePlusOne = slidingWindowSize + 1;
         SlidingWindowSize = slidingWindowSize;
         SlidingWindowSizeSquaredInverted = 1.0 / (slidingWindowSize * slidingWindowSize);
         IntegralIndices = [];

         int width = fullWindowsSize + 1;

         for (int y = 0; y <= (fullWindowsSize - SlidingWindowSize); y += SlidingWindowSize)
         {
            int topOffsetY = (width * y);
            int bottomOffsetY = width * (y + SlidingWindowSize);

            for (int x = 0; x <= (fullWindowsSize - SlidingWindowSize); x += SlidingWindowSize)
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

      public IEnumerable<int> DataTransformersIndices(int featureIndex)
      {
         featureIndex *= 4;

         return [IntegralIndices[featureIndex], IntegralIndices[featureIndex + 1], IntegralIndices[featureIndex + 2], IntegralIndices[featureIndex + 3]];
      }

      public int DataTransformers(IList<uint> integral)
      {
         uint sum = integral[0];
         sum -= integral[1];
         sum -= integral[2];
         sum += integral[3];

         return Convert.ToInt32(sum * SlidingWindowSizeSquaredInverted);
      }
   }
}
