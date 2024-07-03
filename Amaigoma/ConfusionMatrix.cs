using Shouldly;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record ConfusionMatrix
   {
      public int LabelCount
      {
         get
         {
            return Matrix.Count;
         }

         private set
         {
            int labelCount = value;

            Matrix.ShouldBeNull();

            Matrix = ImmutableList<ImmutableList<int>>.Empty;

            for (int i = 0; i < labelCount; i++)
            {
               Matrix = Matrix.Add(ImmutableList<int>.Empty.AddRange(Enumerable.Repeat(0, labelCount)));
            }
         }
      }

      private ImmutableList<ImmutableList<int>> Matrix
      {
         get;
         set;
      }

      public ConfusionMatrix(int labelCount)
      {
         LabelCount = labelCount;
      }

      public void AddPrediction(int expectedSampleLabel, int predictedSampleLabel)
      {
         ImmutableList<int> matrixLine = Matrix[expectedSampleLabel];

         matrixLine = matrixLine.SetItem(predictedSampleLabel, matrixLine[predictedSampleLabel] + 1);
         Matrix = Matrix.SetItem(expectedSampleLabel, matrixLine);
      }

      public ImmutableList<double> Compute()
      {
         ImmutableList<double> confusionMatrix = ImmutableList.Create<double>(0, 0, 0, 0);

         for (int labelIndex = 0; labelIndex < LabelCount; labelIndex++)
         {
            int truePositives = 0;
            int falsePositives = 0;
            int falseNegatives = 0;
            int trueNegatives = 0;

            for (int i = 0; i < LabelCount; i++)
            {
               for (int j = 0; j < LabelCount; j++)
               {
                  if (i == labelIndex)
                  {
                     if (j == labelIndex)
                     {
                        truePositives += Matrix[i][j];
                     }
                     else
                     {
                        falsePositives += Matrix[i][j];
                     }
                  }
                  else
                  {
                     if (i == j)
                     {
                        trueNegatives += Matrix[i][j];
                     }
                     else
                     {
                        falseNegatives += Matrix[i][j];
                     }
                  }
               }
            }
         }

         return confusionMatrix;
      }

      // Based on https://en.wikipedia.org/wiki/Matthews_correlation_coefficient
      public ImmutableList<double> ComputeMatthewsCorrelationCoefficient()
      {
         ImmutableList<double> matthewsCorrelationCoefficients = ImmutableList<double>.Empty;

         // Compute the Matthews coefficient for each class
         for (int labelIndex = 0; labelIndex < LabelCount; labelIndex++)
         {
            int truePositives = 0;
            int falsePositives = 0;
            int falseNegatives = 0;
            int trueNegatives = 0;

            for (int i = 0; i < LabelCount; i++)
            {
               for (int j = 0; j < LabelCount; j++)
               {
                  if (i == labelIndex)
                  {
                     if (j == labelIndex)
                     {
                        truePositives += Matrix[i][j];
                     }
                     else
                     {
                        falsePositives += Matrix[i][j];
                     }
                  }
                  else
                  {
                     if (i == j)
                     {
                        trueNegatives += Matrix[i][j];
                     }
                     else
                     {
                        falseNegatives += Matrix[i][j];
                     }
                  }
               }
            }

            double matthewsCorrelationCoefficientDenominator = (truePositives + falsePositives) *
                                    (truePositives + falseNegatives) *
                                    (trueNegatives + falsePositives) *
                                    (trueNegatives + falseNegatives);

            if (matthewsCorrelationCoefficientDenominator == 0.0)
            {
               matthewsCorrelationCoefficientDenominator = 1.0;
            }
            else
            {
               matthewsCorrelationCoefficientDenominator = Math.Sqrt(matthewsCorrelationCoefficientDenominator);
            }

            double matthewsCorrelationCoefficient = ((truePositives * trueNegatives) - (falsePositives * falseNegatives)) / matthewsCorrelationCoefficientDenominator;

            matthewsCorrelationCoefficients = matthewsCorrelationCoefficients.Add(matthewsCorrelationCoefficient);
         }

         return matthewsCorrelationCoefficients;
      }
   }
}
