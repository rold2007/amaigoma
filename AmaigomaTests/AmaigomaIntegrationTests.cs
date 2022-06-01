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
using System.Runtime.CompilerServices;
using Xunit;

namespace AmaigomaTests
{
   using DataTransformer = System.Converter<IEnumerable<double>, IEnumerable<double>>;

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

      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         ImmutableList<double> features = ImmutableList<double>.Empty;

         const int sizeX = 16;
         const int sizeY = 16;

         Buffer2D<ulong> integralImage = Image.LoadPixelData<L8>(list.Select((x) => (byte)x).ToArray(), sizeX, sizeY).CalculateIntegralImage();

         for (int y = 0; y < sizeY - WindowSize; y += WindowSize)
         {
            for (int x = 0; x < sizeX - WindowSize; x += WindowSize)
            {
               double sum = integralImage[x + WindowSize - 1, y + WindowSize - 1];

               if (x > 0)
               {
                  sum -= integralImage[x - 1, y + WindowSize - 1];

                  if (y > 0)
                  {
                     sum -= integralImage[x + WindowSize - 1, y - 1];
                     sum += integralImage[x - 1, y - 1];
                  }
               }
               else if (y > 0)
               {
                  sum -= integralImage[x + WindowSize - 1, y - 1];
               }

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
         new Point(624, 140)
      });

      static private readonly ImmutableList<Rectangle> allNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] {
         new Rectangle(20, 420, 380, 80)
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
         const int featureWindowSize = 16;
         const int halfFeatureWindowSize = featureWindowSize / 2;
         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), @"..\..\..\" + imagePath);

         Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();
         TrainData trainData = new();
         TrainData backgroundTrainData = new();

         L8 whitePixel = new(255);
         byte[] imageCropPixelsData = new byte[featureWindowSize * featureWindowSize * Unsafe.SizeOf<L8>()];
         Span<byte> imageCropPixels = new(imageCropPixelsData);

         foreach (Point point in points)
         {
            Image<L8> whiteWindow = new(featureWindowSize, featureWindowSize, whitePixel);
            Image<L8> imageCrop = whiteWindow.Clone(clone => clone.DrawImage(fullTextImage, new Point(halfFeatureWindowSize - point.X, halfFeatureWindowSize - point.Y), 1));

            imageCrop.CopyPixelDataTo(imageCropPixels);

            trainData = trainData.AddSample(imageCropPixelsData.Select<byte, double>(s => s), 1);
         }

         foreach(Rectangle rectangle in rectangles)
         {
            for (int y = halfFeatureWindowSize; y < rectangle.Height - halfFeatureWindowSize; y++)
            {
               for (int x = halfFeatureWindowSize; x < rectangle.Width - halfFeatureWindowSize; x++)
               {
                  Image<L8> whiteWindow = new(featureWindowSize, featureWindowSize, whitePixel);
                  Image<L8> imageCrop = whiteWindow.Clone(clone => clone.DrawImage(fullTextImage, new Point(halfFeatureWindowSize - x, halfFeatureWindowSize - y), 1));

                  imageCrop.CopyPixelDataTo(imageCropPixels);

                  backgroundTrainData = backgroundTrainData.AddSample(imageCropPixelsData.Select<byte, double>(s => s), 2);
               }
            }
         }

         trainData = trainData.AddSample(backgroundTrainData.Samples[0], backgroundTrainData.Labels[0]);

         DataTransformer dataTransformers = null;

         dataTransformers += new TempDataTransformer(3).ConvertAll;
         dataTransformers += new TempDataTransformer(5).ConvertAll;
         dataTransformers += new TempDataTransformer(7).ConvertAll;
         dataTransformers += new TempDataTransformer(9).ConvertAll;
         dataTransformers += new TempDataTransformer(11).ConvertAll;
         dataTransformers += new TempDataTransformer(13).ConvertAll;
         dataTransformers += new TempDataTransformer(15).ConvertAll;

         pakiraGenerator.MinimumSampleCount = 1000;

         pakiraGenerator.CertaintyScore = 4.0;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(PakiraTree.Empty, dataTransformers, trainData.Samples[0]);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainData);

         SabotenCache sabotenCache = new(backgroundTrainData.Samples[0]);
         double resultClass = pakiraDecisionTreeModel.PredictLeaf(sabotenCache).PakiraLeaf.LabelValue;

         sabotenCache = new(backgroundTrainData.Samples[1]);
         resultClass = pakiraDecisionTreeModel.PredictLeaf(sabotenCache).PakiraLeaf.LabelValue;

         sabotenCache = new(backgroundTrainData.Samples[20000]);
         resultClass = pakiraDecisionTreeModel.PredictLeaf(sabotenCache).PakiraLeaf.LabelValue;

         foreach(ImmutableList<double> sample in backgroundTrainData.Samples)
         {
            sabotenCache = new(sample);
            resultClass = pakiraDecisionTreeModel.PredictLeaf(sabotenCache).PakiraLeaf.LabelValue;

            if(resultClass != 2)
            {
               pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainData(ImmutableList< ImmutableList<double>>.Empty.Add(sample), ImmutableList<double>.Empty.Add(2)));

               resultClass = pakiraDecisionTreeModel.PredictLeaf(sabotenCache).PakiraLeaf.LabelValue;
               resultClass.ShouldBe(2);
            }
         }
      }
   }
}
