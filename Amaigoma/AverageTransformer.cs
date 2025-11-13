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

         //Point startingOffset = new Point(SlidingWindowHalfSize - fullWindowHalfSize, SlidingWindowHalfSize - fullWindowHalfSize);
         int abc = Math.Max(0, ((fullWindowHalfSize - SlidingWindowHalfSize) / slidingWindowSize) /*- 1*/);
         Point startingOffset = new Point(fullWindowHalfSize - (abc * slidingWindowSize), fullWindowHalfSize - (abc * slidingWindowSize));

         //for (int y = 0; y <= (fullWindowsSize - SlidingWindowSize); y += SlidingWindowSize)
         for (int y = startingOffset.Y - SlidingWindowHalfSize; y <= (fullWindowsSize - SlidingWindowSize); y += SlidingWindowSize)
         {
            //for (int x = 0; x <= (fullWindowsSize - SlidingWindowSize); x += SlidingWindowSize)
            for (int x = startingOffset.X - SlidingWindowHalfSize; x <= (fullWindowsSize - SlidingWindowSize); x += SlidingWindowSize)
            {
               Point positionOffset = new Point(x + SlidingWindowHalfSize, y + SlidingWindowHalfSize);

               //positionOffset.Offset(startingOffset.X, startingOffset.Y);
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
