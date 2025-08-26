// TODO Move all classes depending on SixLabors to a different Utility project so that Amaigoma doesn't depend on it
using Shouldly;
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
      public int IntegralImageIndex;
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
      private ImmutableList<AverageTransformer> AverageTransformers = [];
      private ImmutableList<Range> DataTransformersRanges = [];
      private static readonly RangeComparer rangeComparer = new();
      private readonly ImmutableList<ImmutableList<ReadOnlyMemory<ulong>>> RowSpansPerIntegralImage;

      public AverageWindowFeature(ImmutableDictionary<int, SampleData> positions, ImmutableList<Buffer2D<ulong>> integralImages)
      {
         Samples = positions;

         RowSpansPerIntegralImage = ImmutableList<ImmutableList<ReadOnlyMemory<ulong>>>.Empty;

         foreach (Buffer2D<ulong> integralImage in integralImages)
         {
            ImmutableList<ReadOnlyMemory<ulong>> rowSpans = ImmutableList<ReadOnlyMemory<ulong>>.Empty;

            // Empty line of zeros for the integral
            rowSpans = rowSpans.Add(new ReadOnlyMemory<ulong>([.. Enumerable.Repeat<ulong>(0, integralImage.Width + 1)]));

            for (int y = 0; y < integralImage.Height; y++)
            {
               // TODO Send a list of ReadOnlyMemory in parameter instead of Buffer2D<ulong> integralImage
               // Add one zero at the beginning for the integral
               ReadOnlySpan<ulong> integralData = [0, .. integralImage.DangerousGetRowSpan(y)];
               rowSpans = rowSpans.Add(new ReadOnlyMemory<ulong>(integralData.ToArray()));
            }

            RowSpansPerIntegralImage = RowSpansPerIntegralImage.Add(rowSpans);
         }
      }

      public int ConvertAll(int id, int featureIndex)
      {
         int integralImageIndex = Samples[id].IntegralImageIndex;
         Point position = Samples[id].Position;
         int dataTransformerIndex = DataTransformersRanges.BinarySearch(Range.StartAt(featureIndex), rangeComparer);
         int intraTransformerIndex = featureIndex - DataTransformersRanges[dataTransformerIndex].Start.Value;
         int slidingWindowSize = AverageTransformers[dataTransformerIndex].SlidingWindowSize;
         int slidingWindowHalfSize = AverageTransformers[dataTransformerIndex].SlidingWindowHalfSize;
         int slidingWindowSizePlusOne = AverageTransformers[dataTransformerIndex].SlidingWindowSizePlusOne;
         ImmutableList<ReadOnlyMemory<ulong>> rowSpans = RowSpansPerIntegralImage[integralImageIndex];
         System.Drawing.Point positionOffset = AverageTransformers[dataTransformerIndex].PositionOffsets[intraTransformerIndex];

         position.Offset(positionOffset.X, positionOffset.Y);

         ReadOnlySpan<ulong> topRowSpan = rowSpans[position.Y - slidingWindowHalfSize].Span;
         ReadOnlySpan<ulong> bottomRowSpan = rowSpans[position.Y + slidingWindowHalfSize + 1].Span;
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
            (slidingWindowSize % 2).ShouldBe(1);

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