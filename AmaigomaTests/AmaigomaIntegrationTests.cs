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

// UNDONE January 15th 2024: New algorithm idea. The strenght of each node can be validated if, and only if, there are enough leaves under it to apply
// the logic of swapping the node condition and validating the success rate on train data. For nodes which do not have enough leaves under, this process
// will probably not give reliable results. The solution is probably to prune these nodes. This will force some leaves to have more than one class. So
// more trees need to be created, this way each data may eventually fall in a leaf with a single class. Not sure how to determine how many trees are needed
// to prevent having data to always fall in a multi-class leaf. Maybe a priority list of trees can be created, and each time a tree returns a multiclass
// it should lower its priority. This will not prevent infinit multiclass leaves, but it may help select the tree which returns a multiclass leaf less often. 
namespace AmaigomaTests
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   // TODO Use Skia to add more advanced features ?
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

   public class IntegrationTestDataSet
   {
      public string filename;
      public ImmutableList<Rectangle> regions;
      public ImmutableList<double> classes;

      public IntegrationTestDataSet(string filename, ImmutableList<Rectangle> regions, ImmutableList<double> classes)
      {
         regions.Count.ShouldBe(classes.Count);

         this.filename = filename;
         this.regions = regions;
         this.classes = classes;
      }
   }

   public struct DataSet
   {
      public List<IntegrationTestDataSet> train = new List<IntegrationTestDataSet>();
      public List<IntegrationTestDataSet> validation = new List<IntegrationTestDataSet>();
      public List<IntegrationTestDataSet> test = new List<IntegrationTestDataSet>();

      public DataSet()
      {
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public class AmaigomaIntegrationTests
   {
      static double uppercaseAClass = 1;
      static double otherClass = 2;

      static private readonly ImmutableList<Rectangle> trainNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[]
       {
         new Rectangle(83, 150, 1, 1),
         new Rectangle(0, 0, 300, 100),
      });

      static private readonly ImmutableList<double> trainNotUppercaseA_507484246_Classes = ImmutableList<double>.Empty.AddRange(new double[]
       {
         uppercaseAClass,
         otherClass,
      });

      static private readonly ImmutableList<Rectangle> validationNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[]
       {
         new Rectangle(624, 140, 1, 1),
         new Rectangle(670, 140, 1, 1),
         new Rectangle(688, 140, 1, 1),
         new Rectangle(36, 196, 1, 1),
         new Rectangle(192, 197, 1, 1),
         new Rectangle(181, 213, 1, 1),
         new Rectangle(576, 216, 1, 1),
         new Rectangle(603, 217, 1, 1),
         new Rectangle(658, 217, 1, 1),
         new Rectangle(109, 333, 1, 1),
         new Rectangle(127, 333, 1, 1),
         new Rectangle(520, 40, 230, 90),
         new Rectangle(20, 420, 380, 80),
         new Rectangle(190, 540, 280, 20),
         new Rectangle(20, 555, 480, 215),
      });

      static private readonly ImmutableList<double> validationNotUppercaseA_507484246_Classes = ImmutableList<double>.Empty.AddRange(new double[]
       {
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         otherClass,
         otherClass,
         otherClass,
         otherClass,
      });

      static private readonly ImmutableList<Rectangle> testNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[]
       {
         new Rectangle(228, 334, 1, 1),
         new Rectangle(283, 335, 1, 1),
         new Rectangle(153, 408, 1, 1),
         new Rectangle(217, 519, 1, 1),
         new Rectangle(155, 549, 1, 1),
         new Rectangle(218, 790, 1, 1),
         new Rectangle(411, 836, 1, 1),
         new Rectangle(137, 851, 1, 1),
         new Rectangle(257, 851, 1, 1),
         new Rectangle(605, 852, 1, 1),
         new Rectangle(520, 550, 250, 216),
         new Rectangle(95, 810, 500, 20),
         new Rectangle(20, 900, 756, 70),
         new Rectangle(180, 960, 310, 35)
      });

      static private readonly ImmutableList<double> testNotUppercaseA_507484246_Classes = ImmutableList<double>.Empty.AddRange(new double[]
       {
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         uppercaseAClass,
         otherClass,
         otherClass,
         otherClass,
         otherClass,
      });

      public static System.Collections.Generic.IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new DataSet();
         IntegrationTestDataSet trainIntegrationTestDataSet = new IntegrationTestDataSet(@"assets\text-extraction-for-ocr\507484246.tif", trainNotUppercaseA_507484246_Rectangles, trainNotUppercaseA_507484246_Classes);
         IntegrationTestDataSet validationIntegrationTestDataSet = new IntegrationTestDataSet(@"assets\text-extraction-for-ocr\507484246.tif", validationNotUppercaseA_507484246_Rectangles, validationNotUppercaseA_507484246_Classes);
         IntegrationTestDataSet testIntegrationTestDataSet = new IntegrationTestDataSet(@"assets\text-extraction-for-ocr\507484246.tif", testNotUppercaseA_507484246_Rectangles, testNotUppercaseA_507484246_Classes);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);

         yield return new object[] { dataSet };
      }

      TrainDataCache LoadDataSamples(TrainDataCache dataCache, ImmutableList<Rectangle> rectangles, ImmutableList<double> classes, Buffer2D<ulong> integralImage, int featureWindowSize)
      {
         int halfFeatureWindowSize = featureWindowSize / 2;
         List<double> sample;

         // TODO Move the rectangles and classes in a dictionary to get both values at the same time in the foreach
         int classesIndex = 0;

         foreach (Rectangle rectangle in rectangles)
         {
            double sampleClass = classes[classesIndex];

            for (int y = rectangle.Top; y < rectangle.Bottom; y++)
            {
               for (int x = rectangle.Left; x < rectangle.Right; x++)
               {
                  sample = new() { x, y };

                  int top = y + halfFeatureWindowSize;
                  int xPosition = x + halfFeatureWindowSize;

                  xPosition.ShouldBePositive();

                  for (int y2 = -halfFeatureWindowSize; y2 <= halfFeatureWindowSize; y2++)
                  {
                     int yPosition = top + y2;

                     yPosition.ShouldBeGreaterThanOrEqualTo(0);

                     foreach (ulong integralValue in integralImage.DangerousGetRowSpan(yPosition).Slice(xPosition - halfFeatureWindowSize, featureWindowSize))
                     {
                        sample.Add(integralValue);
                     }
                  }

                  dataCache = dataCache.AddSample(sample, sampleClass);
               }
            }

            classesIndex++;
         }

         return dataCache;
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      [Timeout(60000)]
      public void UppercaseA_507484246(DataSet dataSet)
      {
         string imagePath = dataSet.train[0].filename;
         ImmutableList<Rectangle> trainRectangles = dataSet.train[0].regions;
         ImmutableList<double> trainClasses = dataSet.train[0].classes;
         ImmutableList<Rectangle> validationRectangles = dataSet.validation[0].regions;
         ImmutableList<double> validationClasses = dataSet.validation[0].classes;
         ImmutableList<Rectangle> testRectangles = dataSet.test[0].regions;
         ImmutableList<double> testClasses = dataSet.test[0].classes;

         trainRectangles.Count.ShouldBe(trainClasses.Count);
         validationRectangles.Count.ShouldBe(validationClasses.Count);
         testRectangles.Count.ShouldBe(testClasses.Count);

         const int featureWindowSize = 17;
         const int halfFeatureWindowSize = featureWindowSize / 2;
         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), @"..\..\..\" + imagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();

         Image<L8> imageWithOverscan;

         {
            Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);
            // TODO Need to support different background values
            imageWithOverscan = new Image<L8>(fullTextImage.Width + featureWindowSize, fullTextImage.Height + featureWindowSize, new L8(255));

            // TODO Move this to a globally available helper method
            imageWithOverscan.Mutate(x => x.DrawImage(fullTextImage, new Point(halfFeatureWindowSize, halfFeatureWindowSize), 1.0f));
         }

         Buffer2D<ulong> integralImage = imageWithOverscan.CalculateIntegralImage();

         TrainDataCache trainDataCache = LoadDataSamples(new TrainDataCache(), trainRectangles, trainClasses, integralImage, featureWindowSize);
         TrainDataCache validationDataCache = LoadDataSamples(new TrainDataCache(), validationRectangles, validationClasses, integralImage, featureWindowSize);
         TrainDataCache testDataCache = LoadDataSamples(new TrainDataCache(), testRectangles, testClasses, integralImage, featureWindowSize);

         DataTransformer dataTransformers = null;

         dataTransformers += new TempDataTransformer(3).ConvertAll;
         dataTransformers += new TempDataTransformer(5).ConvertAll;
         dataTransformers += new TempDataTransformer(7).ConvertAll;
         dataTransformers += new TempDataTransformer(9).ConvertAll;
         dataTransformers += new TempDataTransformer(11).ConvertAll;
         dataTransformers += new TempDataTransformer(13).ConvertAll;
         dataTransformers += new TempDataTransformer(15).ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(PakiraTree.Empty, dataTransformers, trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);
         validationDataCache = pakiraDecisionTreeModel.PrefetchAll(validationDataCache);
         testDataCache = pakiraDecisionTreeModel.PrefetchAll(testDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainDataCache(trainDataCache.Samples[0], trainDataCache.Labels[0]));

         // TODO Evaluate the possibility of using shallow trees to serve as sub-routines. The features could be chosen based on the
         // best discrimination, like it was done a while ago. This will result in categories instead of a scalar so the leaves will need to be recombined
         // to provide a binary (scalar) answer. Many strategies could be use to combine leaves. All the left ones vs right ones, random?
         int previousRegenerateTreeCount = -1;
         int previousRegenerateTreeCountBatch = -1;
         int regenerateTreeCount = 0;
         bool processBackgroundTrainData = true;
         ImmutableHashSet<PakiraLeaf> leaves = ImmutableHashSet<PakiraLeaf>.Empty;
         int batchSize = 0;
         // int validationSetSize = 1000;

         // IEnumerable<SabotenCache> validationDataSet = updatedBackgroundTrainDataCache.Samples.Skip(updatedBackgroundTrainDataCache.Samples.Count - validationSetSize);
         ImmutableList<SabotenCache> validationDataSet = validationDataCache.Samples;
         ImmutableList<double> validationLabels = validationDataCache.Labels;

         while (processBackgroundTrainData)
         {
            previousRegenerateTreeCount = regenerateTreeCount;
            processBackgroundTrainData = false;

            // UNDONE Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the Test classes, inside the main library
            // UNDONE Validate the leaves/false positives on the VALIDATION+TEST sets
            // UNDONE Note this methodology somewhere: When the validation set contains too many unevaluated leaves we need to apply one of the following solution:
            // - Increase validation set size
            // - Optimize the tree size by replacing nodes with better discriminating nodes, thus reducing the number of leaves and/or the depth of the tree
            // - Other?
            for (int i = 0; i < trainDataCache.Samples.Count; i += batchSize)
            {
               batchSize = Math.Min(100, Math.Max(20, pakiraDecisionTreeModel.Tree.GetLeaves().Count()));
               IEnumerable<SabotenCache> batchSamples = trainDataCache.Samples.Skip(i).Take(batchSize);
               IEnumerable<double> batchLabels = trainDataCache.Labels.Skip(i).Take(batchSize);

               bool processBatch = true;

               while (processBatch)
               {
                  previousRegenerateTreeCountBatch = regenerateTreeCount;

                  foreach (var item in batchSamples.Zip(batchLabels, (sabotenCache, label) => new { sabotenCache, label }))
                  {
                     SabotenCache sabotenCache = item.sabotenCache;
                     double label = item.label;
                     PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(sabotenCache);
                     double resultClass = pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValue;

                     if (resultClass != label)
                     {
                        pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainDataCache(sabotenCache, label));

                        pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(pakiraDecisionTreePredictionResult.SabotenCache);

                        pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValues.Count().ShouldBe(1);
                        pakiraDecisionTreePredictionResult.PakiraLeaf.LabelValue.ShouldBe(label);

                        regenerateTreeCount++;

                        IEnumerable<KeyValuePair<PakiraNode, PakiraLeaf>> nodeLeaves = pakiraDecisionTreeModel.Tree.GetLeaves();

                        foreach (KeyValuePair<PakiraNode, PakiraLeaf> nodeLeaf in nodeLeaves)
                        {
                           leaves = leaves.Add(nodeLeaf.Value);
                        }

                        int countBefore = leaves.Count;

                        // TODO Move the rectangles and classes in a dictionary to get both values at the same time in the foreach
                        int validationDataSetIndex = 0;

                        foreach (SabotenCache validationSample in validationDataSet)
                        {
                           PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult2 = pakiraDecisionTreeModel.PredictLeaf(validationSample);
                           double sampleClass = validationLabels[validationDataSetIndex];

                           if (pakiraDecisionTreePredictionResult2.PakiraLeaf.LabelValue != sampleClass)
                           {
                              leaves = leaves.Remove(pakiraDecisionTreePredictionResult2.PakiraLeaf);

                              if (leaves.Count == 0)
                              {
                                 break;
                              }
                           }

                           validationDataSetIndex++;
                        }

                        int countAfter = leaves.Count;

                        leaves = leaves.Clear();
                     }
                  }

                  processBatch = (previousRegenerateTreeCountBatch != regenerateTreeCount);
               }
            }

            processBackgroundTrainData = (previousRegenerateTreeCount != regenerateTreeCount);
         }
      }
   }
}
