using Amaigoma;
using Shouldly;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public record AverageTransformerTests // ncrunch: no coverage
   {
      [Fact]
      public void ConvertAll()
      {
         const int FeatureFullWindowSize = 17;
         const int FeatureFullWindowSizePlusOne = FeatureFullWindowSize + 1;
         const int FeatureHalfWindowSize = FeatureFullWindowSize / 2;
         int randomSeed = new Random().Next();
         Random RandomSource = new(randomSeed);
         List<uint> integral = [];
         int byteIndex;
         List<int> computedValues = [];

         for (int windowSize = 1; windowSize < (FeatureFullWindowSize + 1); windowSize += 2)
         {
            AverageTransformer averageTransformer = new(windowSize, FeatureFullWindowSize);

            byte[] bytes = new byte[FeatureFullWindowSize * FeatureFullWindowSize];

            RandomSource.NextBytes(bytes);

            integral.Clear();

            integral.AddRange(Enumerable.Repeat<uint>(0, FeatureFullWindowSize + 1));

            byteIndex = 0;

            for (int y = 0; y < FeatureFullWindowSize; y++)
            {
               integral.Add(0);

               for (int x = 0; x < FeatureFullWindowSize; x++)
               {
                  integral.Add(bytes[byteIndex] +
                     integral[integral.Count - (FeatureFullWindowSize + 1)] +
                     integral.Last() -
                     integral[integral.Count - (FeatureFullWindowSize + 2)]);
                  byteIndex++;
               }
            }

            List<int> convertedValues = [];

            for (int featureIndex = 0; featureIndex < averageTransformer.FeatureCount; featureIndex++)
            {
               List<uint> newSample = [];
               System.Drawing.Point point = averageTransformer.PositionOffsets[featureIndex];
               int leftX = FeatureHalfWindowSize + point.X - windowSize / 2;
               int rightX = FeatureHalfWindowSize + point.X + 1 + windowSize / 2;
               int topY = FeatureHalfWindowSize + point.Y - windowSize / 2;
               int bottomY = FeatureHalfWindowSize + point.Y + 1 + windowSize / 2;

               newSample.Add(integral[leftX + topY * FeatureFullWindowSizePlusOne]);
               newSample.Add(integral[rightX + topY * FeatureFullWindowSizePlusOne]);
               newSample.Add(integral[leftX + bottomY * FeatureFullWindowSizePlusOne]);
               newSample.Add(integral[rightX + bottomY * FeatureFullWindowSizePlusOne]);

               convertedValues.Add(averageTransformer.DataTransformers(newSample));
            }

            computedValues.Clear();

            int windowCount = (FeatureFullWindowSize - windowSize) / windowSize;
            int halfWindowCount = windowCount / 2;
            int averageTransformerHalfSize = windowSize / 2;
            int windowOffset = halfWindowCount * windowSize;

            // TODO This logic was copy pasted and slightly modified from AverageTransformer.cs. It could be simplified and/or shared.
            for (int windowOffsetY = -windowOffset; windowOffsetY <= windowOffset; windowOffsetY += windowSize)
            {
               for (int windowOffsetX = -windowOffset; windowOffsetX <= windowOffset; windowOffsetX += windowSize)
               {
                  Point pixelPosition = new(FeatureHalfWindowSize + windowOffsetX, FeatureHalfWindowSize + windowOffsetY);
                  int manuallyConvertedValue = 0;

                  for (int y = pixelPosition.Y - averageTransformerHalfSize; y <= pixelPosition.Y + averageTransformerHalfSize; y++)
                  {
                     for (int x = pixelPosition.X - averageTransformerHalfSize; x <= pixelPosition.X + averageTransformerHalfSize; x++)
                     {
                        manuallyConvertedValue += bytes[x + y * FeatureFullWindowSize];
                     }
                  }

                  manuallyConvertedValue = Convert.ToInt32((double)manuallyConvertedValue / (windowSize * windowSize));

                  computedValues.Add(Convert.ToInt32(manuallyConvertedValue));
               }
            }

            convertedValues.Count.ShouldBe(computedValues.Count);
            convertedValues.ShouldBe(computedValues);
         }
      }
   }
}
