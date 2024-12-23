using Amaigoma;
using Shouldly;
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
         int randomSeed = new Random().Next();
         Random RandomSource = new(randomSeed);
         List<double> integral = new List<double>();
         int byteIndex;
         List<double> computedValues = new List<double>();

         for (int windowSize = 1; windowSize < (FeatureFullWindowSize + 1); windowSize++)
         {
            double windowSizeSquaredInverted = 1.0 / (windowSize * windowSize);

            AverageTransformer averageTransformer = new AverageTransformer(windowSize, FeatureFullWindowSize);

            byte[] bytes = new byte[FeatureFullWindowSize * FeatureFullWindowSize];

            RandomSource.NextBytes(bytes);

            integral.Clear();

            integral.AddRange(Enumerable.Repeat<double>(0.0, FeatureFullWindowSize + 1));

            byteIndex = 0;

            for (int y = 0; y < FeatureFullWindowSize; y++)
            {
               integral.Add(0);

               for (int x = 0; x < FeatureFullWindowSize; x++)
               {
                  integral.Add(bytes[byteIndex] +
                     integral[integral.Count() - (FeatureFullWindowSize + 1)] +
                     integral.Last() -
                     integral[integral.Count() - (FeatureFullWindowSize + 2)]);
                  byteIndex++;
               }
            }

            List<double> convertedValues = averageTransformer.ConvertAll(integral).ToList<double>();

            computedValues.Clear();

            for (int offsetY = 0; (offsetY + windowSize) <= FeatureFullWindowSize; offsetY += windowSize)
            {
               for (int offsetX = 0; (offsetX + windowSize) <= FeatureFullWindowSize; offsetX += windowSize)
               {
                  double computedValue = 0.0;

                  for (int y = 0; y < windowSize; y++)
                  {
                     for (int x = 0; x < windowSize; x++)
                     {
                        computedValue += bytes[offsetX + x + (offsetY + y) * FeatureFullWindowSize];
                     }
                  }

                  computedValues.Add(computedValue * windowSizeSquaredInverted);
               }
            }

            convertedValues.Count.ShouldBe(computedValues.Count);
            convertedValues.ShouldBe(computedValues);
         }
      }
   }
}
