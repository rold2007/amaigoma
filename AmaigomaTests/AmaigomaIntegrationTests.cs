using Amaigoma;
using NCrunch.Framework;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AmaigomaTests
{
   using DataTransformer = System.Converter<IEnumerable<double>, IEnumerable<double>>;

   // UNDONE Use Skia to add more advanced features ?
   internal class TempDataTransformer
   {
      private int WindowSize
      {
         get;
      }

      private int WindowSizeSquared
      {
         get;
      }

      public TempDataTransformer(int windowSize)
      {
         WindowSize = windowSize;
         WindowSizeSquared = windowSize * windowSize;
      }

      // TODO Add a unit test for this code to make sure it returns the proper result and share it in a separate class so that it can be used elsewhere
      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         const int sizeX = 16;
         const int sizeY = 16;

         double[] otherIntegral = list.Skip(3).ToArray();

         for (int y = 0; y < sizeY - WindowSize; y += WindowSize)
         {
            for (int x = 0; x < sizeX - WindowSize; x += WindowSize)
            {
               double sum;

               sum = otherIntegral[x + WindowSize + ((sizeX + 1) * (y + WindowSize))];
               sum -= otherIntegral[x + ((sizeX + 1) * (y + WindowSize))];
               sum -= otherIntegral[x + WindowSize + ((sizeX + 1) * y)];
               sum += otherIntegral[x + ((sizeX + 1) * y)];

               features = features.Add(sum / WindowSizeSquared);
            }
         }

         return features;
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public class AmaigomaIntegrationTests
   {
      static private readonly ImmutableList<Point> allUppercaseA_507484246_Points = ImmutableList<Point>.Empty.AddRange(new Point[] {
         new Point(83, 150),
         new Point(624, 140),
         new Point(670, 140),
         new Point(688, 140),
         new Point(36, 196),
         new Point(192, 197),
         new Point(181, 213),
         new Point(576, 216),
         new Point(603, 217),
         new Point(658, 217),
         new Point(109, 333),
         new Point(127, 333),
         new Point(228, 334),
         new Point(283, 335),
         new Point(153, 408),
         new Point(217, 519),
         new Point(155, 549),
         new Point(218, 790),
         new Point(411, 836),
         new Point(137, 851),
         new Point(257, 851),
         new Point(605, 852)
      });

      static private readonly ImmutableList<Rectangle> allNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] {
         new Rectangle(0, 0, 300, 100),
         // UNDONE Restore this data in a different way to make sure the test runs in a reasonable time
         //new Rectangle(520, 40, 230, 90),
         //new Rectangle(20, 420, 380, 80),
         //new Rectangle(190, 540, 280, 20),
         //new Rectangle(20, 555, 480, 215),
         //new Rectangle(520, 550, 250, 216),
         //new Rectangle(95, 810, 500, 20),
         //new Rectangle(20, 900, 756, 70),
         //new Rectangle(180, 960, 310, 35)
      });

      public static System.Collections.Generic.IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         yield return new object[] { @"assets\text-extraction-for-ocr\507484246.tif", allUppercaseA_507484246_Points, allNotUppercaseA_507484246_Rectangles };
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      [Timeout(60000)]
      public void UppercaseA_507484246(string imagePath, ImmutableList<Point> points, ImmutableList<Rectangle> rectangles)
      {
         const double uppercaseAClass = 1;
         const double otherClass = 2;
         const int featureWindowSize = 16;
         const int halfFeatureWindowSize = featureWindowSize / 2;
         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), @"..\..\..\" + imagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();
         TrainData trainData = new();
         TrainData backgroundTrainData = new();
         List<double> trainSample = new();

         Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);
         Buffer2D<ulong> integralImage = fullTextImage.CalculateIntegralImage();

         foreach (Point point in points)
         {
            // TODO No need to complicate things by generating an overscan in case the analysis window falls outside the image. Just assert that the window totally fits in the image. If it really becomes needed, the overscan could be easily added to the full image instead.
            trainSample.Add(trainData.Samples.Count);
            trainSample.Add(point.X);
            trainSample.Add(point.Y);

            // TODO This code needs to be moved to a new method and also used by the Rectangle loop below
            for (int y = point.Y - halfFeatureWindowSize - 1; y < point.Y + halfFeatureWindowSize; y++)
            {
               if (y >= 0)
               {
                  int x = point.X - halfFeatureWindowSize - 1;
                  int sliceLength = featureWindowSize;

                  if (x < 0)
                  {
                     trainSample.Add(0);
                     x++;
                  }
                  else
                  {
                     sliceLength++;
                  }

                  foreach (ulong integralValue in integralImage.DangerousGetRowSpan(y).Slice(x, sliceLength))
                  {
                     trainSample.Add(integralValue);
                  }
               }
               else
               {
                  trainSample.AddRange(Enumerable.Repeat<double>(0, featureWindowSize + 1));
               }
            }

            trainData = trainData.AddSample(trainSample, uppercaseAClass);
            trainSample.Clear();
         }

         foreach (Rectangle rectangle in rectangles)
         {
            for (int y = halfFeatureWindowSize; y < rectangle.Height - halfFeatureWindowSize; y++)
            {
               for (int x = halfFeatureWindowSize; x < rectangle.Width - halfFeatureWindowSize; x++)
               {
                  trainSample.Add(x + rectangle.Left);
                  trainSample.Add(y + rectangle.Top);

                  for (int y2 = y + rectangle.Top - halfFeatureWindowSize - 1; y2 < y + rectangle.Top + halfFeatureWindowSize; y2++)
                  {
                     if (y2 >= 0)
                     {
                        int x2 = x + rectangle.Left - halfFeatureWindowSize - 1;
                        int sliceLength = featureWindowSize;

                        if (x2 < 0)
                        {
                           trainSample.Add(0);
                           x2++;
                        }
                        else
                        {
                           sliceLength++;
                        }

                        foreach (ulong integralValue in integralImage.DangerousGetRowSpan(y2).Slice(x2, sliceLength))
                        {
                           trainSample.Add(integralValue);
                        }
                     }
                     else
                     {
                        trainSample.AddRange(Enumerable.Repeat<double>(0, featureWindowSize + 1));
                     }
                  }

                  backgroundTrainData = backgroundTrainData.AddSample(trainSample, otherClass);
                  trainSample.Clear();
               }
            }
         }

         trainSample.Add(trainData.Samples.Count);
         trainSample.AddRange(backgroundTrainData.Samples[0]);
         trainData = trainData.AddSample(trainSample, backgroundTrainData.Labels[0]);
         trainSample.Clear();

         DataTransformer dataTransformers = null;

         dataTransformers += new TempDataTransformer(3).ConvertAll;
         dataTransformers += new TempDataTransformer(5).ConvertAll;
         dataTransformers += new TempDataTransformer(7).ConvertAll;
         dataTransformers += new TempDataTransformer(9).ConvertAll;
         dataTransformers += new TempDataTransformer(11).ConvertAll;
         dataTransformers += new TempDataTransformer(13).ConvertAll;
         dataTransformers += new TempDataTransformer(15).ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(PakiraTree.Empty, dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         ImmutableList<SabotenCache> backgroundTrainDataCache = ImmutableList<SabotenCache>.Empty;

         foreach (List<double> sample in backgroundTrainData.Samples)
         {
            trainSample.Add(trainData.Samples.Count);
            trainSample.AddRange(sample);

            backgroundTrainDataCache = backgroundTrainDataCache.Add(new(trainSample.ToList()));
            trainSample.Clear();
         }

         backgroundTrainDataCache = pakiraDecisionTreeModel.PrefetchAll(backgroundTrainDataCache);

         // UNDONE Add method to compute tree quality (true positives, etc.)
         // UNDONE There are 250k samples... That's a little bit too much. Use biggest blobs of false positives ? Blob is possible with Skia ?
         for (int i = 0; i < backgroundTrainDataCache.Count; i++)
         {
            SabotenCache sabotenCache = backgroundTrainDataCache[i];

            PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(sabotenCache);
            double resultClass = pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValue;

            if (resultClass != otherClass)
            {
               // UNDONE Creating a new TrainData forces a new Prefetch, this is not efficient.
               // UNDONE Make it so that it is not necessary to call ToList()...
               pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainData(ImmutableList<List<double>>.Empty.Add(sabotenCache.Data.ToList()), ImmutableList<double>.Empty.Add(otherClass)));

               pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(pakiraDecisionTreePredictionResult.SabotenCache);

               pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValues.Count().ShouldBe(1);
               pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValue.ShouldBe(otherClass);
            }
         }
      }
   }
}
