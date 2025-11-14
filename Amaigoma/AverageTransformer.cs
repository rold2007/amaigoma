using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
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

      public ImmutableList<Point> PositionOffsets
      {
         get;
      }

      private double SlidingWindowSizeSquaredInverted
      {
         get;
      }

      // TODO Add support for overlapping sliding windows
      // TODO Add support a different fullWindowsSize for each slding window size
      public AverageTransformer(int slidingWindowSize, int fullWindowsSize)
      {
         fullWindowsSize.ShouldBeGreaterThanOrEqualTo(slidingWindowSize);

         int fullWindowHalfSize = fullWindowsSize / 2;

         SlidingWindowHalfSize = slidingWindowSize / 2;
         SlidingWindowSizePlusOne = slidingWindowSize + 1;
         SlidingWindowSize = slidingWindowSize;
         SlidingWindowSizeSquaredInverted = 1.0 / (slidingWindowSize * slidingWindowSize);
         PositionOffsets = [];

         int offsetAdjustment = Math.Max(0, ((fullWindowHalfSize - SlidingWindowHalfSize) / slidingWindowSize));
         Point startingOffset = new(fullWindowHalfSize - (offsetAdjustment * slidingWindowSize), fullWindowHalfSize - (offsetAdjustment * slidingWindowSize));

         for (int y = startingOffset.Y - SlidingWindowHalfSize; y <= (fullWindowsSize - SlidingWindowSize); y += SlidingWindowSize)
         {
            for (int x = startingOffset.X - SlidingWindowHalfSize; x <= (fullWindowsSize - SlidingWindowSize); x += SlidingWindowSize)
            {
               Point positionOffset = new(x + SlidingWindowHalfSize, y + SlidingWindowHalfSize);

               positionOffset.Offset(-fullWindowHalfSize, -fullWindowHalfSize);

               PositionOffsets = PositionOffsets.Add(positionOffset);
            }
         }

         FeatureCount = PositionOffsets.Count;
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
