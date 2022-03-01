using Amaigoma;
using MathNet.Numerics.LinearAlgebra;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AmaigomaConsole
{
   using DataTransformer = System.Converter<IEnumerable<double>, IEnumerable<double>>;

   internal class TempDataTransformer
   {
      private int WindowSize
      {
         get;
      }

      public TempDataTransformer(int windowSize)
      {
         WindowSize = windowSize;
      }

      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         const int sizeX = /*24*/16;
         const int sizeY = /*24*/16;

         for (int y = 0; y < sizeY - WindowSize; y += WindowSize)
         {
            for (int x = 0; x < sizeX - WindowSize; x += WindowSize)
            {
               double sum = 0;

               for (int j = 0; j < WindowSize; j++)
               {
                  int offsetStart = x + ((y + j) * sizeX);

                  for (int i = 0; i < WindowSize; i++)
                  {
                     sum += list.ElementAt(offsetStart + i);
                  }
               }

               features = features.Add(sum / (WindowSize * WindowSize));
            }
         }

         return features;
      }
   }

   class Program
   {
      static void Main(string[] args)
      {
         const int featureWindowSize = /*24*/16;
         const int halfFeatureWindowSize = featureWindowSize / 2;
         string imageMainPath = args[0];

         Image<L8> fullTextImage = Image.Load<L8>(imageMainPath + @"Images\FS18800114.2.11-a2-427w-c32.png");
         Image<L8> completeA = Image.Load<L8>(imageMainPath + @"\Images\FS18800114.2.11-a2-427w-c32\a\CompleteA.png");

         PakiraDecisionTreeGenerator pakiraGenerator = new PakiraDecisionTreeGenerator();
         TrainData trainData = new TrainData();

         L8 whitePixel = new L8(255);
         L8 blackPixel = new L8(0);
         L8 dontCarePixel = new L8(128);
         byte dontCarePixelValue = dontCarePixel.PackedValue;
         byte[] imageCropPixelsData = new byte[featureWindowSize * featureWindowSize * Unsafe.SizeOf<L8>()];

         completeA.ProcessPixelRows(accessor =>
         {
            Span<byte> imageCropPixels = new Span<byte>(imageCropPixelsData);

            for (int y = 0; y < accessor.Height; y++)
            {
               Span<L8> pixelRow = accessor.GetRowSpan(y);

               for (int x = 0; x < pixelRow.Length; x++)
               {
                  // Get a reference to the pixel at position x
                  ref L8 pixel = ref pixelRow[x];

                  if (pixel != dontCarePixel)
                  {
                     Image<L8> whiteWindow = new Image<L8>(featureWindowSize, featureWindowSize, whitePixel);
                     Image<L8> imageCrop = whiteWindow.Clone(clone => clone.DrawImage(fullTextImage, new Point(halfFeatureWindowSize - x, halfFeatureWindowSize - y), 1));

                     imageCrop.CopyPixelDataTo(imageCropPixels);

                     trainData = trainData.AddSample(imageCropPixelsData.Select<byte, double>(s => s), pixel.PackedValue);
                  }
               }
            }
         });

         DataTransformer dataTransformers = null;

         //dataTransformers += new PassThroughTransformer().ConvertAll;
         dataTransformers += new TempDataTransformer(3).ConvertAll;
         dataTransformers += new TempDataTransformer(5).ConvertAll;
         dataTransformers += new TempDataTransformer(7).ConvertAll;
         dataTransformers += new TempDataTransformer(9).ConvertAll;
         dataTransformers += new TempDataTransformer(11).ConvertAll;
         dataTransformers += new TempDataTransformer(13).ConvertAll;
         dataTransformers += new TempDataTransformer(15).ConvertAll;
         //dataTransformers += new TempDataTransformer(17).ConvertAll;
         //dataTransformers += new TempDataTransformer(19).ConvertAll;
         //dataTransformers += new TempDataTransformer(21).ConvertAll;
         //dataTransformers += new TempDataTransformer(23).ConvertAll;

         pakiraGenerator.MinimumSampleCount = 10;
         //pakiraGenerator.MinimumSampleCount = 50;
         pakiraGenerator.MinimumSampleCount = 100;
         //pakiraGenerator.MinimumSampleCount = 200;
         //pakiraGenerator.MinimumSampleCount = 500;
         pakiraGenerator.MinimumSampleCount = 1000;
         //pakiraGenerator.MinimumSampleCount = 10000;

         pakiraGenerator.CertaintyScore = 1.0;
         pakiraGenerator.CertaintyScore = 4.0;
         //pakiraGenerator.CertaintyScore = double.MaxValue;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new PakiraDecisionTreeModel(PakiraTree.Empty, dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         string folder;

         ///*
         CropProcessor cropProcessor;
         double resultClass;
         Image<L8> completeAResult = completeA.Clone();

         //completeAResult = new Image<L8>(completeA.Width, completeA.Height, dontCarePixel);

         Span<byte> imagePixels = new Span<byte>(imageCropPixelsData);

         for (int y = 0; y < fullTextImage.Height - featureWindowSize; y++)
         {
            for (int x = 0; x < fullTextImage.Width - featureWindowSize; x++)
            {
               if (completeA[x + halfFeatureWindowSize, y + halfFeatureWindowSize] == dontCarePixel)
               {
                  cropProcessor = new CropProcessor(new Rectangle(x, y, featureWindowSize, featureWindowSize), fullTextImage.Size());

                  ICloningImageProcessor<L8> cloningImageProcessor = cropProcessor.CreatePixelSpecificCloningProcessor(Configuration.Default, fullTextImage, new Rectangle(x, y, featureWindowSize, featureWindowSize));

                  Image<L8> croppedImage = cloningImageProcessor.CloneAndExecute();

                  croppedImage.CopyPixelDataTo(imagePixels);

                  Vector<double> croppedSample = Vector<double>.Build.Dense(imagePixels.Length);

                  for (int pixelIndex = 0; pixelIndex < imagePixels.Length; pixelIndex++)
                  {
                     croppedSample.At(pixelIndex, imagePixels[pixelIndex]);
                  }

                  SabotenCache croppedSampleCache = new SabotenCache(croppedSample);

                  resultClass = pakiraDecisionTreeModel.PredictNode(croppedSampleCache).PakiraLeaf.Value;

                  if (resultClass == 0)
                  {
                     //completeAResult[x + halfFeatureWindowSize, y + halfFeatureWindowSize] = blackPixel;
                  }
                  else if (resultClass == 255)
                  {
                     //string resultClassString = resultClass.ToString();

                     //folder = "c:\\!\\argh\\" + resultClassString;
                     //Directory.CreateDirectory(folder);
                     //string path = folder + "\\" + x.ToString() + "x" + y.ToString() + ".png";

                     //croppedImage.SaveAsPng(path);

                     completeAResult[x + halfFeatureWindowSize, y + halfFeatureWindowSize] = blackPixel;
                     //completeAResult[x + 12, y + 12] = whitePixel;
                  }
               }
            }

            folder = "c:\\!\\argh\\" + 255;
            Directory.CreateDirectory(folder);
            string path = folder + "\\" + y.ToString() + ".png";

            completeAResult.SaveAsPng(path);
         }
         //*/
      }
   }

   public class ConfusionMatrix
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
