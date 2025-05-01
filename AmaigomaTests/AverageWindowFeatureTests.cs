using Amaigoma;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using Xunit;

namespace AmaigomaTests
{
   public record AverageWindowFeatureTests // ncrunch: no coverage
   {
      [Fact]
      public void Constructor()
      {
         // TODO Make AverageWindowFeature immutable with only a parameter-less constructor as public
         //  AverageWindowFeature averageWindowFeature = new();
      }

      [Fact]
      // TODO Refactor this test to simplify the code
      public void ConvertAllTest()
      {
         const int FeatureFullWindowSize = 17;
         const int FeatureHalfWindowSize = FeatureFullWindowSize / 2;
         int randomSeed = new Random().Next();
         Random RandomSource = new(randomSeed);
         System.Drawing.Size imageSize = new(51, 51);
         ImmutableDictionary<int, SampleData> positions = ImmutableDictionary<int, SampleData>.Empty;

         // Fill image with random data
         byte[] bytes = new byte[imageSize.Width * imageSize.Height];

         RandomSource.NextBytes(bytes);
         Image<L8> image = Image.LoadPixelData<L8>(bytes, imageSize.Width, imageSize.Height);

         Buffer2D<ulong> integralImage = image.CalculateIntegralImage();

         for (int y = FeatureHalfWindowSize; y < imageSize.Height - FeatureHalfWindowSize; y++)
         {
            for (int x = FeatureHalfWindowSize; x < imageSize.Width - FeatureHalfWindowSize; x++)
            {
               positions = positions.Add(positions.Count, new SampleData { Position = new SixLabors.ImageSharp.Point(x, y), Label = 0 });
            }
         }

         AverageWindowFeature averageWindowFeature = new(positions, integralImage, FeatureFullWindowSize);

         ImmutableList<int> averageTransformerSizes = [FeatureFullWindowSize, 7, 5, 3, 1];
         ImmutableList<int> featureIndexAverageTransformerSizes = ImmutableList<int>.Empty;

         foreach (int averageTransformerSize in averageTransformerSizes)
         {
            for (int y = 0; y <= FeatureFullWindowSize - averageTransformerSize; y += averageTransformerSize)
            {
               for (int x = 0; x <= FeatureFullWindowSize - averageTransformerSize; x += averageTransformerSize)
               {
                  featureIndexAverageTransformerSizes = featureIndexAverageTransformerSizes.Add(averageTransformerSize);
               }
            }
         }

         averageWindowFeature.AddAverageTransformer(averageTransformerSizes);

         foreach (KeyValuePair<int, SampleData> position in positions)
         {
            for (int featureIndex = 0; featureIndex < averageWindowFeature.FeaturesCount(); featureIndex++)
            {
               // Manual compute for validation
               int averageTransformerSize = featureIndexAverageTransformerSizes[featureIndex];
               int averageTransformerHalfSize = averageTransformerSize / 2;
               Point pixelPosition = position.Value.Position;
               double manuallyConvertedValue = 0;

               for (int y = pixelPosition.Y - averageTransformerHalfSize; y <= pixelPosition.Y + averageTransformerHalfSize; y++)
               {
                  for (int x = pixelPosition.X - averageTransformerHalfSize; x <= pixelPosition.X + averageTransformerHalfSize; x++)
                  {
                     manuallyConvertedValue += bytes[x + y * imageSize.Width];
                  }
               }

               manuallyConvertedValue /= (averageTransformerSize * averageTransformerSize);

               // UNDONE Add Benchmark.Net to validate faster method and optimize even more if needed
               double convertedValueNew = averageWindowFeature.ConvertAll(position.Key, featureIndex);

               convertedValueNew.ShouldBe(manuallyConvertedValue, 0.0000001);
            }
         }
      }

      [Fact]
      public void ConvertAllSmallImageErrorTest()
      {
         const int FeatureFullWindowSize = 17;
         System.Drawing.Size imageSize = new(16, 16);
         ImmutableDictionary<int, SampleData> positions = ImmutableDictionary<int, SampleData>.Empty;
         Image<L8> image = new(imageSize.Width, imageSize.Height);
         Buffer2D<ulong> integralImage = image.CalculateIntegralImage();

         positions = positions.Add(0, new SampleData { Position = new SixLabors.ImageSharp.Point(0, 0), Label = 0 });

         AverageWindowFeature averageWindowFeature = new(positions, integralImage, FeatureFullWindowSize);

         ImmutableList<int> averageTransformerSizes = [FeatureFullWindowSize];

         averageWindowFeature.AddAverageTransformer(averageTransformerSizes);

         Should.Throw<ArgumentOutOfRangeException>(() => averageWindowFeature.ConvertAll(0, 0));
      }
   }
}
