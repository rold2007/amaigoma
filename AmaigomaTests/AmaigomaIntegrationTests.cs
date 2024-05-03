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

// UNDONE January 15th 2024: New algorithm idea. The strength of each node can be validated if, and only if, there are enough leaves under it to apply
// the logic of swapping the node condition and validating the success rate on train data. For nodes which do not have enough leaves under, this process
// will probably not give reliable results. The solution is probably to prune these nodes. This will force some leaves to have more than one class. So
// more trees need to be created, this way each data may eventually fall in a leaf with a single class. Not sure how to determine how many trees are needed
// to prevent having data to always fall in a multi-class leaf. Maybe a priority list of trees can be created, and each time a tree returns a multiclass
// it should lower its priority. This will not prevent infinite multiclass leaves, but it may help select the tree which returns a multiclass leaf less often.
namespace AmaigomaTests
{
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

   public struct AccuracyResult
   {
      public ImmutableHashSet<PakiraLeaf> leavesBefore;
      public ImmutableHashSet<PakiraLeaf> leavesAfter;
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public class AmaigomaIntegrationTests
   {
      static double uppercaseAClass = 1;
      static double otherClass = 2;

      // UNDONE Removed some samples in Train, Validation and Test sets to be able to run faster until the performances are improved
      static private readonly ImmutableList<Rectangle> trainNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[]
       {
          new Rectangle(83, 150, 1, 1),
          new Rectangle(0, 0, 300, 100),
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
          //new Rectangle(520, 40, 230, 90),
          //new Rectangle(20, 420, 380, 80),
      });

      static private readonly ImmutableList<double> trainNotUppercaseA_507484246_Classes = ImmutableList<double>.Empty.AddRange(new double[]
       {
          uppercaseAClass,
          otherClass,
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
          //otherClass,
          //otherClass,
      });

      static private readonly ImmutableList<Rectangle> validationNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[]
       {
         new Rectangle(190, 540, 280, 20),
         //new Rectangle(20, 555, 480, 215),
      });

      static private readonly ImmutableList<double> validationNotUppercaseA_507484246_Classes = ImmutableList<double>.Empty.AddRange(new double[]
       {
         otherClass,
         //otherClass,
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
         //new Rectangle(520, 550, 250, 216),
         new Rectangle(95, 810, 500, 20),
         //new Rectangle(20, 900, 756, 70),
         //new Rectangle(180, 960, 310, 35)
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
         //otherClass,
         otherClass,
         //otherClass,
         //otherClass,
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

      // UNDONE 4 This is insane. It is slow and uses a LOT of memory. After everything is well unit tested. Replaced the samples
      // by an ID and another class will be responsible to extract the needed data directly from the (integral) image when required.
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
                  // UNDONE The pixel position was removed to simplify unit testing. Find a different way of doing it.
                  //sample = new() { x, y };
                  sample = new();
                  sample.EnsureCapacity(2 + (featureWindowSize + 1) * (featureWindowSize + 1));

                  int top = y + halfFeatureWindowSize;
                  int xPosition = x + halfFeatureWindowSize;

                  xPosition.ShouldBePositive();

                  // +1 length to support first row of integral image
                  for (int y2 = -halfFeatureWindowSize; y2 <= halfFeatureWindowSize + 1; y2++)
                  {
                     int yPosition = top + y2;

                     yPosition.ShouldBeGreaterThanOrEqualTo(0);

                     // +1 length to support first column of integral image
                     foreach (ulong integralValue in integralImage.DangerousGetRowSpan(yPosition).Slice(xPosition - halfFeatureWindowSize, featureWindowSize + 1))
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

      AccuracyResult ComputeAccuracy(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainDataCache dataCache)
      {
         ImmutableList<double> labels = dataCache.Labels;
         ImmutableHashSet<PakiraLeaf> leaves = ImmutableHashSet<PakiraLeaf>.Empty.Union(pakiraDecisionTreeModel.Tree.GetLeaves().Select(x => x.Value));
         AccuracyResult accuracyResult = new AccuracyResult();

         accuracyResult.leavesBefore = leaves;

         // TODO Move the rectangles and classes in a dictionary to get both values at the same time in the foreach
         int validationDataSetIndex = 0;

         foreach (SabotenCache sample in dataCache.Samples)
         {
            PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult2 = pakiraDecisionTreeModel.PredictLeaf(sample);
            double sampleClass = labels[validationDataSetIndex];

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

         accuracyResult.leavesAfter = leaves;

         return accuracyResult;
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      // UNDONE 3 This test is becoming way too slow, even for an integration test. Simplify/optimize it
      [Timeout(600000)]
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

         const int halfFeatureWindowSize = AverageTransformer.FeatureWindowSize / 2;
         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), @"..\..\..\" + imagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();
         Image<L8> imageWithOverscan;

         {
            Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);

            // TODO Need to support different background values
            imageWithOverscan = new Image<L8>(fullTextImage.Width + (2 * halfFeatureWindowSize) + 1, fullTextImage.Height + (2 * halfFeatureWindowSize) + 1, new L8(255));

            // TODO Move this to a globally available helper method
            // There is a +1 in coordinates to make sure we have at least one row and one column that we never use at the beginning so that we never go outside the image for integral values
            imageWithOverscan.Mutate(x => x.DrawImage(fullTextImage, new Point(halfFeatureWindowSize + 1, halfFeatureWindowSize + 1), 1.0f));
         }

         Buffer2D<ulong> integralImage = imageWithOverscan.CalculateIntegralImage();

         TrainDataCache trainDataCache = LoadDataSamples(new TrainDataCache(), trainRectangles, trainClasses, integralImage, AverageTransformer.FeatureWindowSize);
         TrainDataCache validationDataCache = LoadDataSamples(new TrainDataCache(), validationRectangles, validationClasses, integralImage, AverageTransformer.FeatureWindowSize);
         TrainDataCache testDataCache = LoadDataSamples(new TrainDataCache(), testRectangles, testClasses, integralImage, AverageTransformer.FeatureWindowSize);

         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         Converter<IEnumerable<double>, IEnumerable<double>> dataTransformers = null;

         // UNDONE Removed a data transformer to be able to run faster until the performances are improved
         //dataTransformers += new AverageTransformer(1).ConvertAll;
         dataTransformers += new AverageTransformer(3).ConvertAll;
         dataTransformers += new AverageTransformer(5).ConvertAll;
         dataTransformers += new AverageTransformer(7).ConvertAll;
         dataTransformers += new AverageTransformer(17).ConvertAll;

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new(PakiraTree.Empty, dataTransformers, trainDataCache.Samples[0].Data);

         trainDataCache = pakiraDecisionTreeModel.PrefetchAll(trainDataCache);

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new TrainDataCache(trainDataCache.Samples[0], trainDataCache.Labels[0]));

         // TODO Evaluate the possibility of using shallow trees to serve as sub-routines. The features could be chosen based on the
         // best discrimination, like it was done a while ago. This will result in categories instead of a scalar so the leaves will need to be recombined
         // to provide a binary (scalar) answer. Many strategies could be use to combine leaves. All the left ones vs right ones, random?
         int previousRegenerateTreeCount = -1;
         int previousRegenerateTreeCountBatch = -1;
         int regenerateTreeCount = 0;
         bool processBackgroundTrainData = true;
         int batchSize = 0;

         ImmutableList<SabotenCache> validationDataSet = validationDataCache.Samples;
         ImmutableList<double> validationLabels = validationDataCache.Labels;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult trainAccuracyResult;

         while (processBackgroundTrainData)
         {
            previousRegenerateTreeCount = regenerateTreeCount;
            processBackgroundTrainData = false;

            // UNDONE Run many tree generations to evaluate if the average accuracy is lower when the root is using a data transformer
            // with a lower size (1 vs 15)
            // TODO Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the Test classes, inside the main library
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

               // TODO The validation set should be used to identify the leaves which are not predicting correctly. Then find
               //       some data in the train set to improve these leaves
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
                     }
                  }

                  processBatch = (previousRegenerateTreeCountBatch != regenerateTreeCount);
               }
            }

            trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainDataCache);
            validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationDataCache);
            testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testDataCache);

            processBackgroundTrainData = (trainAccuracyResult.leavesBefore.Count != trainAccuracyResult.leavesAfter.Count);
         }
      }
   }
}
