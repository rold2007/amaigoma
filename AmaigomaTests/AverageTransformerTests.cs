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
         int randomSeed = new Random().Next();
         Random RandomSource = new(randomSeed);
         List<double> integral = new List<double>();
         int byteIndex;
         List<double> computedValues = new List<double>();

         for (int windowSize = 1; windowSize < (AverageTransformer.FeatureWindowSize + 1); windowSize++)
         {
            double windowSizeSquaredInverted = 1.0 / (windowSize * windowSize);

            AverageTransformer averageTransformer = new AverageTransformer(windowSize);

            byte[] bytes = new byte[AverageTransformer.FeatureWindowSize * AverageTransformer.FeatureWindowSize];

            RandomSource.NextBytes(bytes);

            integral.Clear();

            integral.AddRange(Enumerable.Repeat<double>(0.0, AverageTransformer.FeatureWindowSize + 1));

            byteIndex = 0;

            for (int y = 0; y < AverageTransformer.FeatureWindowSize; y++)
            {
               integral.Add(0);

               for (int x = 0; x < AverageTransformer.FeatureWindowSize; x++)
               {
                  integral.Add(bytes[byteIndex] +
                     integral[integral.Count() - (AverageTransformer.FeatureWindowSize + 1)] +
                     integral.Last() -
                     integral[integral.Count() - (AverageTransformer.FeatureWindowSize + 2)]);
                  byteIndex++;
               }
            }

            List<double> convertedValues = averageTransformer.ConvertAll(integral).ToList<double>();
            
            computedValues.Clear();

            for (int offsetY = 0; (offsetY + windowSize) <= AverageTransformer.FeatureWindowSize; offsetY += windowSize)
            {
               for (int offsetX = 0; (offsetX + windowSize) <= AverageTransformer.FeatureWindowSize; offsetX += windowSize)
               {
                  double computedValue = 0.0;

                  for (int y = 0; y < windowSize; y++)
                  {
                     for (int x = 0; x < windowSize; x++)
                     {
                        computedValue += bytes[offsetX + x + (offsetY + y) * AverageTransformer.FeatureWindowSize];
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
