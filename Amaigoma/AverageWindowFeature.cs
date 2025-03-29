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
        private int FeatureWindowSizePlusOne;
        private ImmutableList<AverageTransformer> AverageTransformers = ImmutableList<AverageTransformer>.Empty;
        private ImmutableList<Range> DataTransformersRanges = ImmutableList<Range>.Empty;
        private ImmutableList<double> ConvertedSample = ImmutableList<double>.Empty;
        private ImmutableList<int> IndexYPositions = ImmutableList<int>.Empty;
        static private RangeComparer rangeComparer = new RangeComparer();
        private ImmutableList<ReadOnlyMemory<ulong>> RowSpans = ImmutableList<ReadOnlyMemory<ulong>>.Empty;

        public AverageWindowFeature(ImmutableDictionary<int, SampleData> positions, Buffer2D<ulong> integralImage, int featureWindowSize)
        {
            Samples = positions;
            IntegralImage = integralImage;
            FeatureWindowSize = featureWindowSize;
            FeatureWindowSizePlusOne = FeatureWindowSize + 1;
            ConvertedSample = ConvertedSample.AddRange(Enumerable.Repeat<double>(0, 4));
            IndexYPositions = IndexYPositions.AddRange(Enumerable.Repeat<int>(0, integralImage.Width * integralImage.Height));

            for (int y = 0; y < integralImage.Height; y++)
            {
                // UNDONE Send a list of ReadOnlyMenory in parameter instead of Buffer2D<ulong> integralImage
                RowSpans = RowSpans.Add(new ReadOnlyMemory<ulong>(IntegralImage.DangerousGetRowSpan(y).ToArray()));
            }
        }

        public double ConvertAll(int id, int featureIndex)
        {
            Point position = Samples[id].Position;
            List<double> newSample = new((FeatureWindowSize + 1) * (FeatureWindowSize + 1));
            int dataTransformerIndex = DataTransformersRanges.BinarySearch(Range.StartAt(featureIndex), rangeComparer);
            IEnumerable<int> indices = AverageTransformers[dataTransformerIndex].DataTransformersIndices(featureIndex - DataTransformersRanges[dataTransformerIndex].Start.Value);

            // UNDONE Try to apply this solution to see if it is faster, although it will probably allocate more: https://github.com/SixLabors/ImageSharp/discussions/1666#discussioncomment-876494
            // +1 length to support first row of integral image

            // UNDONE This logic can be further optimized, no need to get all 4 spans when dealing with AverageTransformers
            foreach (int i in indices)
            {
                int indexY = i / (FeatureWindowSize + 1);
                int y2 = indexY;

                IndexYPositions[i].ShouldBe(indexY);

                {
                    int yPosition = position.Y + y2;

                    yPosition.ShouldBeGreaterThanOrEqualTo(0);

                    Span<ulong> rowSpan = IntegralImage.DangerousGetRowSpan(yPosition);
                    // +1 length to support first column of integral image
                    Span<ulong> slice = rowSpan.Slice(position.X, FeatureWindowSize + 1);

                    int indexX = i - (indexY * (FeatureWindowSize + 1));
                    newSample.Add(slice[indexX]);
                }
            }

            List<int> indices2 = AverageTransformers[dataTransformerIndex].DataTransformersIndices(featureIndex - DataTransformersRanges[dataTransformerIndex].Start.Value).ToList();

            indices2.Count.ShouldBe(4);

            /*
            {
               // UNDONE Get rid of this Convert.ToInt32() and the double value if possible
               int indexY = Convert.ToInt32(Math.Truncate(indices2[0] * InvertedFeatureWindowSizePlusOne));
               int y2 = indexY;

               {
                  int yPosition = position.Y + y2;

                  yPosition.ShouldBeGreaterThanOrEqualTo(0);

                  Span<ulong> rowSpan = IntegralImage.DangerousGetRowSpan(yPosition);
                  // +1 length to support first column of integral image
                  Span<ulong> slice = rowSpan.Slice(position.X, FeatureWindowSizePlusOne);

                  int indexX = indices2[0] - (indexY * FeatureWindowSizePlusOne);
                  ConvertedSample = ConvertedSample.SetItem(0, slice[indexX]);

                  indexX = indices2[1] - (indexY * FeatureWindowSizePlusOne);
                  ConvertedSample = ConvertedSample.SetItem(1, slice[indexX]);
               }

               indexY = Convert.ToInt32(Math.Truncate(indices2[2] * InvertedFeatureWindowSizePlusOne));
               y2 = indexY;

               {
                  int yPosition = position.Y + y2;

                  yPosition.ShouldBeGreaterThanOrEqualTo(0);

                  Span<ulong> rowSpan = IntegralImage.DangerousGetRowSpan(yPosition);
                  // +1 length to support first column of integral image
                  Span<ulong> slice = rowSpan.Slice(position.X, FeatureWindowSizePlusOne);

                  int indexX = indices2[2] - (indexY * FeatureWindowSizePlusOne);
                  ConvertedSample = ConvertedSample.SetItem(2, slice[indexX]);

                  indexX = indices2[3] - (indexY * FeatureWindowSizePlusOne);
                  ConvertedSample = ConvertedSample.SetItem(3, slice[indexX]);
               }
            }
            //*/

            // {
            //    int indexY = IndexYPositions[indices2[0]];

            //    Span<ulong> rowSpan = IntegralImage.DangerousGetRowSpan(position.Y + indexY);
            //    // +1 length to support first column of integral image
            //    Span<ulong> slice = rowSpan.Slice(position.X, FeatureWindowSizePlusOne);

            //    int indexX = indices2[0] - (indexY * FeatureWindowSizePlusOne);
            //    ConvertedSample = ConvertedSample.SetItem(0, slice[indexX]);

            //    int indexX3 = indices2[1] - (indexY * FeatureWindowSizePlusOne);
            //    ConvertedSample = ConvertedSample.SetItem(1, slice[indexX3]);

            //    indexY = IndexYPositions[indices2[2]];

            //    Span<ulong> rowSpan2 = IntegralImage.DangerousGetRowSpan(position.Y + indexY);
            //    // +1 length to support first column of integral image
            //    Span<ulong> slice2 = rowSpan2.Slice(position.X, FeatureWindowSizePlusOne);

            //    ConvertedSample = ConvertedSample.SetItem(2, slice2[indexX]);
            //    ConvertedSample = ConvertedSample.SetItem(3, slice2[indexX3]);
            // }

            {
                int indexY = IndexYPositions[indices2[0]];

                ReadOnlySpan<ulong> rowSpan = RowSpans[position.Y + indexY].Span;
                // +1 length to support first column of integral image
                ReadOnlySpan<ulong> slice = rowSpan.Slice(position.X, FeatureWindowSizePlusOne);

                int indexX = indices2[0] - (indexY * FeatureWindowSizePlusOne);
                ConvertedSample = ConvertedSample.SetItem(0, slice[indexX]);

                int indexX3 = indices2[1] - (indexY * FeatureWindowSizePlusOne);
                ConvertedSample = ConvertedSample.SetItem(1, slice[indexX3]);

                indexY = IndexYPositions[indices2[2]];

                ReadOnlySpan<ulong> rowSpan2 = RowSpans[position.Y + indexY].Span;
                // +1 length to support first column of integral image
                ReadOnlySpan<ulong> slice2 = rowSpan2.Slice(position.X, FeatureWindowSizePlusOne);

                ConvertedSample = ConvertedSample.SetItem(2, slice2[indexX]);
                ConvertedSample = ConvertedSample.SetItem(3, slice2[indexX3]);
            }

            ConvertedSample.ShouldBe(newSample);

            return AverageTransformers[dataTransformerIndex].DataTransformers(newSample);
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

                for (int i = 0; i < averageTransformer.FeatureCount; i++)
                {
                    foreach (int index in averageTransformer.DataTransformersIndices(i))
                    {
                        IndexYPositions = IndexYPositions.SetItem(index, index / FeatureWindowSizePlusOne);
                    }
                }
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