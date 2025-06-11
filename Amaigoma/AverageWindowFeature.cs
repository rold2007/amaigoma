// TODO Move all classes depending on SixLabors to a different Utility project so that Amaigoma doesn't depend on it
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public struct SampleData
   {
      public Point Position;
      public int Label;
   }

   internal class RangeComparer : IComparer<Range>
   {
      public int Compare(Range x, Range y)
      {
         if (x.Start.Value < y.Start.Value)
         {
            return -1;
         }
         else if (x.Start.Value >= y.End.Value)
         {
            return 1;
         }

         return 0;
      }
   }

   public record AverageWindowFeature // ncrunch: no coverage
   {
      private readonly ImmutableDictionary<int, SampleData> Samples;
      private readonly Buffer2D<ulong> IntegralImage;
      private ImmutableList<AverageTransformer> AverageTransformers = [];
      private ImmutableList<Range> DataTransformersRanges = [];
      private static readonly RangeComparer rangeComparer = new();
      private readonly ImmutableList<ReadOnlyMemory<ulong>> RowSpans = [];

      public AverageWindowFeature(ImmutableDictionary<int, SampleData> positions, Buffer2D<ulong> integralImage)
      {
         Samples = positions;
         IntegralImage = integralImage;

         // Empty line of zeros for the integral
         RowSpans = RowSpans.Add(new ReadOnlyMemory<ulong>([.. Enumerable.Repeat<ulong>(0, integralImage.Width + 1)]));

         for (int y = 0; y < integralImage.Height; y++)
         {
            // TODO Send a list of ReadOnlyMemory in parameter instead of Buffer2D<ulong> integralImage
            // Add one zero at the beginning for the integral
            ReadOnlySpan<ulong> integralData = [0, .. IntegralImage.DangerousGetRowSpan(y)];
            RowSpans = RowSpans.Add(new ReadOnlyMemory<ulong>(integralData.ToArray()));
         }
      }

      public int ConvertAll(int id, int featureIndex)
      {
         Point position = Samples[id].Position;
         int dataTransformerIndex = DataTransformersRanges.BinarySearch(Range.StartAt(featureIndex), rangeComparer);
         int slidingWindowSize = AverageTransformers[dataTransformerIndex].SlidingWindowSize;
         int slidingWindowHalfSize = AverageTransformers[dataTransformerIndex].SlidingWindowHalfSize;
         int slidingWindowSizePlusOne = AverageTransformers[dataTransformerIndex].SlidingWindowSizePlusOne;

         ReadOnlySpan<ulong> topRowSpan = RowSpans[position.Y - slidingWindowHalfSize].Span;
         ReadOnlySpan<ulong> bottomRowSpan = RowSpans[position.Y + slidingWindowHalfSize + 1].Span;
         ReadOnlySpan<ulong> topSlice = topRowSpan.Slice(position.X - slidingWindowHalfSize, slidingWindowSizePlusOne);
         ReadOnlySpan<ulong> bottomSlice = bottomRowSpan.Slice(position.X - slidingWindowHalfSize, slidingWindowSizePlusOne);

         return AverageTransformers[dataTransformerIndex].DataTransformers([Convert.ToUInt32(topSlice[0]), Convert.ToUInt32(topSlice[slidingWindowSize]), Convert.ToUInt32(bottomSlice[0]), Convert.ToUInt32(bottomSlice[slidingWindowSize])]);
      }

      // TODO Change this method to make the class immutable
      public void AddAverageTransformer(IEnumerable<int> slidingWindowSizes)
      {
         int startRange = 0;
         int endRange;
         int maxFeatureWindowSize = slidingWindowSizes.Max();

         foreach (int slidingWindowSize in slidingWindowSizes)
         {
            AverageTransformer averageTransformer = new(slidingWindowSize, maxFeatureWindowSize);

            endRange = startRange + averageTransformer.FeatureCount;

            AverageTransformers = AverageTransformers.Add(averageTransformer);
            DataTransformersRanges = DataTransformersRanges.Add(new Range(startRange, endRange));
            startRange = endRange;
         }
      }

      public int ExtractLabel(int id)
      {
         return Samples[id].Label;
      }

      public int FeaturesCount()
      {
         return DataTransformersRanges.Last().End.Value;
      }
   }
}