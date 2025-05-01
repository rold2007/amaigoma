using Shouldly;
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
        private ImmutableDictionary<int, SampleData> Samples;
        private Buffer2D<ulong> IntegralImage;
        private int FeatureWindowSize;
        private int FeatureWindowHalfSize;
        private int FeatureWindowSizePlusOne;
        private ImmutableList<AverageTransformer> AverageTransformers = ImmutableList<AverageTransformer>.Empty;
        private ImmutableList<Range> DataTransformersRanges = ImmutableList<Range>.Empty;
        private ImmutableList<double> ConvertedSample = ImmutableList<double>.Empty;
        static private RangeComparer rangeComparer = new RangeComparer();
        private ImmutableList<ReadOnlyMemory<ulong>> RowSpans = ImmutableList<ReadOnlyMemory<ulong>>.Empty;

        public AverageWindowFeature(ImmutableDictionary<int, SampleData> positions, Buffer2D<ulong> integralImage, int featureWindowSize)
        {
            Samples = positions;
            IntegralImage = integralImage;
            FeatureWindowSize = featureWindowSize;
            FeatureWindowHalfSize = featureWindowSize / 2;
            FeatureWindowSizePlusOne = FeatureWindowSize + 1;
            ConvertedSample = ConvertedSample.AddRange(Enumerable.Repeat<double>(0, 4));

            // Empty line of zeros for the integral
            RowSpans = RowSpans.Add(new ReadOnlyMemory<ulong>(Enumerable.Repeat<ulong>(0, integralImage.Width + 1).ToArray()));

            for (int y = 0; y < integralImage.Height; y++)
            {
                // UNDONE Send a list of ReadOnlyMemory in parameter instead of Buffer2D<ulong> integralImage
                // Add one zero at the beginning for the integral
                ReadOnlySpan<ulong> integralData = [0, .. IntegralImage.DangerousGetRowSpan(y)];
                RowSpans = RowSpans.Add(new ReadOnlyMemory<ulong>(integralData.ToArray()));
            }
        }

        public double ConvertAll(int id, int featureIndex)
        {
            Point position = Samples[id].Position;
            int dataTransformerIndex = DataTransformersRanges.BinarySearch(Range.StartAt(featureIndex), rangeComparer);
            int slidingWindowSize = AverageTransformers[dataTransformerIndex].SlidingWindowSize;
            int slidingWindowHalfSize = AverageTransformers[dataTransformerIndex].SlidingWindowHalfSize;
            int slidingWindowSizePlusOne = AverageTransformers[dataTransformerIndex].SlidingWindowSizePlusOne;

            {
                int indexY = position.Y - slidingWindowHalfSize;

                ReadOnlySpan<ulong> rowSpan = RowSpans[indexY].Span;
                // +1 length to support first column of integral image
                ReadOnlySpan<ulong> slice = rowSpan.Slice(position.X - slidingWindowHalfSize, slidingWindowSizePlusOne);

                ConvertedSample = ConvertedSample.SetItem(0, slice[0]);

                ConvertedSample = ConvertedSample.SetItem(1, slice[slidingWindowSize]);
                ReadOnlySpan<ulong> rowSpan2 = RowSpans[position.Y + slidingWindowHalfSize + 1].Span;
                // +1 length to support first column of integral image
                ReadOnlySpan<ulong> slice2 = rowSpan2.Slice(position.X - slidingWindowHalfSize, slidingWindowSizePlusOne);

                ConvertedSample = ConvertedSample.SetItem(2, slice2[0]);
                ConvertedSample = ConvertedSample.SetItem(3, slice2[slidingWindowSize]);
            }

            return AverageTransformers[dataTransformerIndex].DataTransformers(ConvertedSample);
        }

        // TODO Change this method to make the class immutable
        public void AddAverageTransformer(IEnumerable<int> slidingWindowSizes)
        {
            int startRange = 0;
            int endRange;

            foreach (int slidingWindowSize in slidingWindowSizes)
            {
                AverageTransformer averageTransformer = new(slidingWindowSize, FeatureWindowSize);

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