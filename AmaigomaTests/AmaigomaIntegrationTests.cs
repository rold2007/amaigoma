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
using Xunit.Abstractions;

namespace AmaigomaTests
{
   public struct RegionLabel
   {
      public Rectangle rectangle;
      public int label;
   }

   public record IntegrationTestDataSet // ncrunch: no coverage
   {
      public string filename;
      public ImmutableList<RegionLabel> regionLabels = ImmutableList<RegionLabel>.Empty;

      public IntegrationTestDataSet(string filename, ImmutableList<Rectangle> regions, ImmutableList<int> labels)
      {
         regions.Count.ShouldBe(labels.Count);

         this.filename = filename;

         for (int i = 0; i < regions.Count; i++)
         {
            regionLabels = regionLabels.Add(new RegionLabel { rectangle = regions[i], label = labels[i] });
         }
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
      public ImmutableDictionary<PakiraLeaf, ImmutableList<int>> truePositives = ImmutableDictionary<PakiraLeaf, ImmutableList<int>>.Empty;
      public ImmutableDictionary<PakiraLeaf, ImmutableList<int>> falsePositives = ImmutableDictionary<PakiraLeaf, ImmutableList<int>>.Empty;

      public AccuracyResult()
      {
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests // ncrunch: no coverage
   {
      // UNDONE Add more classes
      static readonly int uppercaseA = 1; // ncrunch: no coverage
      static readonly int other = 2; // ncrunch: no coverage

      static private readonly ImmutableList<Rectangle> trainNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
       {
          new Rectangle(83, 150, 1, 1),
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
          new Rectangle(17, 17, 300, 100),
          new Rectangle(520, 40, 230, 90),
          new Rectangle(20, 420, 380, 80),
      });

      static private readonly ImmutableList<int> trainNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          other,
          other,
          other,
      });

      // UNDONE Need more background samples in the validation set to remove false positive on real letters
      static private readonly ImmutableList<Rectangle> validationNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
       {
         new Rectangle(190, 540, 280, 20),
         new Rectangle(20, 555, 480, 215),
      });

      static private readonly ImmutableList<int> validationNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
         other,
         other,
      });

      static private readonly ImmutableList<Rectangle> testNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
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
         new Rectangle(520, 550, 230, 216),
         new Rectangle(95, 810, 500, 20),
         new Rectangle(20, 900, 740, 70),
         new Rectangle(180, 960, 310, 23)
      });

      static private readonly ImmutableList<int> testNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         other,
         other,
         other,
         other,
      });

      public static IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new DataSet();
         IntegrationTestDataSet trainIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", trainNotUppercaseA_507484246_Rectangles, trainNotUppercaseA_507484246);
         IntegrationTestDataSet validationIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", validationNotUppercaseA_507484246_Rectangles, validationNotUppercaseA_507484246);
         IntegrationTestDataSet testIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", testNotUppercaseA_507484246_Rectangles, testNotUppercaseA_507484246);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);

         yield return new object[] { dataSet };
      }

      private readonly ITestOutputHelper output;

      public AmaigomaIntegrationTests(ITestOutputHelper output)
      {
         this.output = output;
      }

      ImmutableDictionary<int, SampleData> LoadDataSamples(ImmutableList<RegionLabel> rectangles, int startingIndex)
      {
         ImmutableDictionary<int, SampleData> result = ImmutableDictionary<int, SampleData>.Empty;

         foreach (RegionLabel regionLabel in rectangles)
         {
            for (int y = regionLabel.rectangle.Top; y < regionLabel.rectangle.Bottom; y++)
            {
               for (int x = regionLabel.rectangle.Left; x < regionLabel.rectangle.Right; x++)
               {
                  result = result.Add(startingIndex, new SampleData { Position = new Point(x, y), Label = regionLabel.label });
                  startingIndex++;
               }
            }
         }

         return result;
      }

      AccuracyResult ComputeAccuracy(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         ImmutableHashSet<PakiraLeaf> leaves = ImmutableHashSet<PakiraLeaf>.Empty.Union(pakiraDecisionTreeModel.Tree.GetLeaves().Select(x => x.Value));
         AccuracyResult accuracyResult = new AccuracyResult();

         accuracyResult.leavesBefore = leaves;

         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

         foreach (int id in ids)
         {
            PakiraLeaf pakiraLeafResult = pakiraTreeWalker.PredictLeaf(id);
            int label = tanukiETL.TanukiLabelExtractor(id);

            if (pakiraLeafResult.LabelValues.Contains(label))
            {
               if (accuracyResult.truePositives.ContainsKey(pakiraLeafResult))
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.SetItem(pakiraLeafResult, accuracyResult.truePositives[pakiraLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.Add(pakiraLeafResult, ImmutableList<int>.Empty.Add(id));
               }
            }
            else
            {
               if (accuracyResult.falsePositives.ContainsKey(pakiraLeafResult))
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.SetItem(pakiraLeafResult, accuracyResult.falsePositives[pakiraLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.Add(pakiraLeafResult, ImmutableList<int>.Empty.Add(id));
               }

               leaves = leaves.Remove(pakiraLeafResult);
            }
         }

         accuracyResult.leavesAfter = leaves;

         return accuracyResult;
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      [Timeout(600000)]
      public void UppercaseA_507484246(DataSet dataSet)
      {
         const int FeatureFullWindowSize = 17;
         string imagePath = dataSet.train[0].filename;
         ImmutableList<RegionLabel> trainRectangles = dataSet.train[0].regionLabels;
         ImmutableList<RegionLabel> validationRectangles = dataSet.validation[0].regionLabels;
         ImmutableList<RegionLabel> testRectangles = dataSet.test[0].regionLabels;

         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), imagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();

         Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);

         Buffer2D<ulong> integralImage = fullTextImage.CalculateIntegralImage();
         ImmutableDictionary<int, SampleData> trainPositions;
         ImmutableDictionary<int, SampleData> validationPositions;
         ImmutableDictionary<int, SampleData> testPositions;

         trainPositions = LoadDataSamples(trainRectangles, 0);
         validationPositions = LoadDataSamples(validationRectangles, trainPositions.Count());
         testPositions = LoadDataSamples(testRectangles, trainPositions.Count() + validationPositions.Count());

         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];

         // TODO Maybe AverageWindowFeature could be used to create a new instance with the same internal values but by only changing the positions/intergralImage ?
         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         AverageWindowFeature trainDataExtractor = new AverageWindowFeature(trainPositions, integralImage, FeatureFullWindowSize);
         AverageWindowFeature validationDataExtractor = new AverageWindowFeature(validationPositions, integralImage, FeatureFullWindowSize);
         AverageWindowFeature testDataExtractor = new AverageWindowFeature(testPositions, integralImage, FeatureFullWindowSize);

         trainDataExtractor.AddAverageTransformer(averageTransformerSizes);
         validationDataExtractor.AddAverageTransformer(averageTransformerSizes);
         testDataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL trainTanukiETL = new(trainDataExtractor.ConvertAll, trainDataExtractor.ExtractLabel, trainDataExtractor.FeaturesCount());
         TanukiETL validationTanukiETL = new(validationDataExtractor.ConvertAll, validationDataExtractor.ExtractLabel, validationDataExtractor.FeaturesCount());
         TanukiETL testTanukiETL = new(testDataExtractor.ConvertAll, testDataExtractor.ExtractLabel, testDataExtractor.FeaturesCount());

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainPositions.Keys.Take(100), trainTanukiETL);

         int previousRegenerateTreeCount = -1;
         int previousRegenerateTreeCountBatch = -1;
         int regenerateTreeCount = 0;
         bool processBackgroundTrainData = true;
         int batchSize = 0;
         int totalTrainSamples = trainPositions.Keys.Count();

         AccuracyResult trainAccuracyResult;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;

         // UNDONE Remove SabotenCache to see if the integrated test runs faster or slower.
         // UNDONE Review the batch concept. Start the first tree with about 100 samples (including all classes). Then identify the leaves which are
         //        not predicting correctly in the validation set, and add more train samples of the same class to these leaves. Keep an eye on
         //        the prediction rate on th test set, which should match closely with the validation set.
         // UNDONE Integrate images from mirflickr
         // UNDONE The test accuracy result shows that not a single uppercase A is correctly predicted. See if the new batch system and a proper leaf selection fixes this.
         // UNDONE The validation set should be used to identify the leaves which are not predicting correctly. Then find
         //       some data in the train set to improve these leaves
         // UNDONE Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the
         //        Test classes, inside the main library
         // UNDONE Note this methodology somewhere: When the validation set contains too many unevaluated leaves we need to apply one of the following solution:
         //        - Increase validation set size
         //        - Optimize the tree size by replacing nodes with better discriminating nodes, thus reducing the number of leaves and/or the depth of the tree
         //        - Other?
         // UNDONE Add parallelism to the test. I'm, tired of waiting. Make sure it is easy to remove for the day there are too many long running tests.
         while (processBackgroundTrainData)
         {
            previousRegenerateTreeCount = regenerateTreeCount;
            processBackgroundTrainData = false;

            for (int i = 0; i < totalTrainSamples; i += batchSize)
            {
               batchSize = Math.Min(100, Math.Max(20, pakiraDecisionTreeModel.Tree.GetLeaves().Count()));
               IEnumerable<int> batchSamples = trainPositions.Keys.Skip(i).Take(batchSize);

               bool processBatch = true;
               PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);

               while (processBatch)
               {
                  int batchIndex = 0;

                  previousRegenerateTreeCountBatch = regenerateTreeCount;

                  foreach (int id in batchSamples)
                  {
                     int expectedLabel = trainPositions[id].Label;
                     IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

                     if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
                     {
                        pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, [id], trainTanukiETL);
                        pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);

                        IEnumerable<int> labelValues = pakiraTreeWalker.PredictLeaf(id).LabelValues;

                        labelValues.Count().ShouldBe(1);
                        labelValues.First().ShouldBe(expectedLabel);

                        regenerateTreeCount++;
                     }

                     batchIndex++;
                  }

                  processBatch = previousRegenerateTreeCountBatch != regenerateTreeCount;
               }
            }

            trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);
            validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiETL);
            testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiETL);

            PrintFirstNodeIndex(pakiraDecisionTreeModel);
            PrintConfusionMatrix(trainAccuracyResult, "Train");
            PrintConfusionMatrix(validationAccuracyResult, "Validation");
            PrintConfusionMatrix(testAccuracyResult, "Test");
            PrintLeaveResults(trainAccuracyResult);
            PrintLeaveResults(validationAccuracyResult);
            PrintLeaveResults(testAccuracyResult);
            PrintEnd();

            processBackgroundTrainData = (trainAccuracyResult.leavesBefore.Count != trainAccuracyResult.leavesAfter.Count);
         }
      }

      private void PrintFirstNodeIndex(PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         output.WriteLine("First node index: " + pakiraDecisionTreeModel.Tree.Root.Column.ToString());
      }

      private void PrintConfusionMatrix(AccuracyResult accuracyResult, string title)
      {
         output.WriteLine("Confusion matrix for {0}", title);

         foreach (PakiraLeaf leaf in accuracyResult.leavesBefore)
         {
            int falsePositivesCount = accuracyResult.falsePositives.GetValueOrDefault(leaf, ImmutableList<int>.Empty).Count;

            if (falsePositivesCount > 0)
            {
               int truePositivesCount = accuracyResult.truePositives.GetValueOrDefault(leaf, ImmutableList<int>.Empty).Count;

               output.WriteLine("Leaf: {0} - {1} true positives, {2} false positives", String.Join(" ", leaf.LabelValues.Select(item => item.ToString()).ToArray()), truePositivesCount, falsePositivesCount);
            }
         }
      }

      private void PrintLeaveResults(AccuracyResult accuracyResult)
      {
         output.WriteLine("{0}/{1} = {2}%", accuracyResult.leavesAfter.Count().ToString(), accuracyResult.leavesBefore.Count().ToString(), 100.0 * accuracyResult.leavesAfter.Count() / accuracyResult.leavesBefore.Count());
      }

      private void PrintEnd()
      {
         output.WriteLine("---");
      }
   }
}
