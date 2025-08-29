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
      public ImmutableList<RegionLabel> regionLabels = [];

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
      public List<IntegrationTestDataSet> train = [];
      public List<IntegrationTestDataSet> validation = [];
      public List<IntegrationTestDataSet> test = [];
      public List<IntegrationTestDataSet> im164 = [];

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

   // UNDONE Find a better name for this class
   public record BestSplitLogic
   {
      private ImmutableDictionary<int, double> idWeights = ImmutableDictionary<int, double>.Empty;

      // UNDONE DO NOT COMMIT DO BEFORE COMMIT put the result of each feature index in a dictionary to evaluate the best logic
      // UNDONE The Gini coefficient is calcutaled, correctly I think, but there may be better coefficient. Try other ones if the results are not satisfying.
      // UNDONE This new method SHOULD fix most false positive uppecase A. If not, find why.
      public Tuple<int, double> GetBestSplit(IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO Instead of shuffling randomly, it might make more sense to simply cycle through all available feature indices sequentially. or all data transformers sequentially
         // and then randomly within each transformer.
         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         IEnumerable<int> featureIndices = Enumerable.Range(0, tanukiETL.TanukiFeatureCount);
         // TODO Using entropy instead of Gini coefficient would be more accurate, but it is also more expensive to compute.
         ImmutableList<double> giniCoefficients = ImmutableList<double>.Empty;

         foreach (int featureIndex in featureIndices)
         {
            ImmutableList<int> idsList = ids.ToImmutableList();
            // UNDONE clearly, the wrong transformer is called here
            ImmutableList<int> transformedData = ids.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex)).ToImmutableList();
            int minimumValue = transformedData.Min();
            int maximumValue = transformedData.Max();
            double average = transformedData.Average();
            ImmutableList<int> leftIds = ImmutableList<int>.Empty;
            ImmutableList<int> rightIds = ImmutableList<int>.Empty;
            ImmutableDictionary<int, double> leftLabelWeights = ImmutableDictionary<int, double>.Empty;
            ImmutableDictionary<int, double> rightLabelWeights = ImmutableDictionary<int, double>.Empty;
            double leftTotalWeight = 0.0;
            double rightTotalWeight = 0.0;

            for (int i = 0; i < transformedData.Count; i++)
            {
               int id = idsList[i];
               int label = tanukiETL.TanukiLabelExtractor(id);
               double idWeight;

               if (!idWeights.ContainsKey(id))
               {
                  // UNDONE All weights should be initialized to 1.0/NbSamplesInClass when starting a new tree
                  idWeights = idWeights.Add(id, 1.0);
               }

               idWeight = idWeights[id];

               if (transformedData[i] <= average)
               {
                  leftIds = leftIds.Add(idsList[i]);

                  if (leftLabelWeights.ContainsKey(label))
                  {
                     leftLabelWeights = leftLabelWeights.SetItem(label, leftLabelWeights[label] + idWeight);
                  }
                  else
                  {
                     leftLabelWeights = leftLabelWeights.Add(label, idWeight);
                  }

                  leftTotalWeight += idWeight;
               }
               else
               {
                  rightIds = rightIds.Add(idsList[i]);

                  if (rightLabelWeights.ContainsKey(label))
                  {
                     rightLabelWeights = rightLabelWeights.SetItem(label, rightLabelWeights[label] + idWeight);
                  }
                  else
                  {
                     rightLabelWeights = rightLabelWeights.Add(label, idWeight);
                  }

                  rightTotalWeight += idWeight;
               }
            }

            ImmutableList<int> allLabels = leftLabelWeights.Keys.Union(rightLabelWeights.Keys).ToImmutableList();
            double leftSumWeightsSquared = 0.0;
            double rightSumWeightsSquared = 0.0;

            foreach (int label in allLabels)
            {
               double leftWeight = leftLabelWeights.GetValueOrDefault(label, 0.0);

               // Change weights to probabilities
               leftWeight /= leftTotalWeight;

               leftSumWeightsSquared += leftWeight * leftWeight;

               if (rightTotalWeight > 0)
               {
                  double rightWeight = rightLabelWeights.GetValueOrDefault(label, 0.0);

                  // Change weights to probabilities
                  rightWeight /= rightTotalWeight;

                  rightSumWeightsSquared += rightWeight * rightWeight;
               }
            }

            double leftGiniCoefficient = 1.0 - leftSumWeightsSquared;
            double rightGiniCoefficient = 1.0 - rightSumWeightsSquared;

            if (rightTotalWeight == 0)
            {
               rightGiniCoefficient = 0.5;
            }

            leftGiniCoefficient.ShouldBeInRange(0.0, 0.5);
            rightGiniCoefficient.ShouldBeInRange(0.0, 0.5);

            // From https://www.analyticsvidhya.com/articles/gini-impurity/
            double weightedGiniCoefficient = (leftTotalWeight * leftGiniCoefficient + rightTotalWeight * rightGiniCoefficient) / (leftTotalWeight + rightTotalWeight);

            weightedGiniCoefficient.ShouldBeInRange(0.0, 0.5);

            giniCoefficients = giniCoefficients.Add(weightedGiniCoefficient);

            if (bestFeature == -1 || weightedGiniCoefficient < giniCoefficients[bestFeature])
            {
               bestFeature = featureIndex;
               bestFeatureSplit = average;
            }

            if (weightedGiniCoefficient <= 0.0)
            {
               break;
            }

            // UNDONE Restore this logic once it works well. Make sure that an early exit does not affect the accuracy too much.
            // if (quickAccept)
            // {
            //    break;
            // }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         return new Tuple<int, double>(bestFeature, bestFeatureSplit);
      }

      public bool EndBuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, TanukiETL tanukiETL)
      {
         ImmutableDictionary<int, int> labelsNodeCount = ImmutableDictionary<int, int>.Empty;

         foreach (KeyValuePair<PakiraNode, PakiraLeaf> nodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves())
         {
            foreach (int label in nodeLeaf.Value.LabelValues)
            {
               labelsNodeCount.TryGetValue(label, out int count);
               labelsNodeCount = labelsNodeCount.SetItem(label, count + 1);
            }
         }

         foreach (int label in labelsNodeCount.Keys)
         {
            int nodeCount = labelsNodeCount[label];
            double baseLeafWeight = 1.0 / nodeCount;

            foreach (KeyValuePair<PakiraNode, PakiraLeaf> nodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves())
            {
               if (nodeLeaf.Value.LabelValues.Contains(label))
               {
                  ImmutableList<int> ids = pakiraDecisionTreeModel.DataSamples(nodeLeaf.Value);
                  double weight = baseLeafWeight / ids.Count;

                  foreach (int id in ids)
                  {
                     idWeights = idWeights.SetItem(id, weight);
                  }
               }
            }
         }

         return false;
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests // ncrunch: no coveraget
   {
      // UNDONE Add more classes
      static readonly int uppercaseA = 1; // ncrunch: no coverage
      static readonly int other = 2; // ncrunch: no coverage

      static private readonly ImmutableList<Rectangle> train_507484246_Rectangles =
      [
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
         new Rectangle(20, 420, 380, 80),
         new Rectangle(17, 17, 300, 100),
         new Rectangle(520, 40, 230, 90),
      ];

      static private readonly ImmutableList<int> train_507484246_Labels =
      [
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
      ];

      // UNDONE Need more background samples in the validation set to remove false positive on real letters
      static private readonly ImmutableList<Rectangle> validation_507484246_Rectangles =
      [
         new Rectangle(228, 334, 1, 1),
         new Rectangle(283, 335, 1, 1),
         new Rectangle(153, 408, 1, 1),
         new Rectangle(217, 519, 1, 1),
         new Rectangle(155, 549, 1, 1),
         new Rectangle(190, 540, 280, 20),
         new Rectangle(20, 555, 480, 215),
      ];

      static private readonly ImmutableList<int> validation_507484246_Labels = [uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA, other, other];

      static private readonly ImmutableList<Rectangle> test_507484246_Rectangles =
      [
         new Rectangle(218, 790, 1, 1),
         new Rectangle(411, 836, 1, 1),
         new Rectangle(137, 851, 1, 1),
         new Rectangle(257, 851, 1, 1),
         new Rectangle(605, 852, 1, 1),
         new Rectangle(520, 550, 230, 216),
         new Rectangle(95, 810, 500, 20),
         new Rectangle(20, 900, 740, 70),
         new Rectangle(180, 960, 310, 23)
,
      ];

      static private readonly ImmutableList<int> test_507484246_Labels = [uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA, other, other, other, other];

      static private readonly ImmutableList<Rectangle> im164Rectangles = [new Rectangle(8, 8, 483, 358)];

      static private readonly ImmutableList<int> im164Labels = [other];

      static public IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new();
         IntegrationTestDataSet trainIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels);
         IntegrationTestDataSet validationIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels);
         IntegrationTestDataSet testIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels);
         IntegrationTestDataSet im164IntegrationTestDataSet = new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);
         dataSet.im164.Add(im164IntegrationTestDataSet);

         yield return new object[] { dataSet };
      }

      private readonly ITestOutputHelper output;

      public AmaigomaIntegrationTests(ITestOutputHelper output)
      {
         this.output = output;
      }

      static private ImmutableDictionary<int, SampleData> LoadDataSamples(ImmutableList<RegionLabel> rectangles, int startingIndex, int integralImageIndex)
      {
         ImmutableDictionary<int, SampleData> result = ImmutableDictionary<int, SampleData>.Empty;

         foreach (RegionLabel regionLabel in rectangles)
         {
            for (int y = regionLabel.rectangle.Top; y < regionLabel.rectangle.Bottom; y++)
            {
               for (int x = regionLabel.rectangle.Left; x < regionLabel.rectangle.Right; x++)
               {
                  result = result.Add(startingIndex, new SampleData { IntegralImageIndex = integralImageIndex, Position = new Point(x, y), Label = regionLabel.label });
                  startingIndex++;
               }
            }
         }

         return result;
      }

      static private AccuracyResult ComputeAccuracy(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         ImmutableHashSet<PakiraLeaf> leaves = ImmutableHashSet<PakiraLeaf>.Empty.Union(pakiraDecisionTreeModel.Tree.GetLeaves().Select(x => x.Value));
         AccuracyResult accuracyResult = new()
         {
            leavesBefore = leaves
         };

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

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
                  accuracyResult.truePositives = accuracyResult.truePositives.Add(pakiraLeafResult, [id]);
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
                  accuracyResult.falsePositives = accuracyResult.falsePositives.Add(pakiraLeafResult, [id]);
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
         ImmutableDictionary<string, Image<L8>> sourceImages = ImmutableDictionary<string, Image<L8>>.Empty;
         ImmutableDictionary<string, Buffer2D<ulong>> integralImages = ImmutableDictionary<string, Buffer2D<ulong>>.Empty;
         ImmutableList<RegionLabel> trainRectangles = dataSet.train[0].regionLabels;
         ImmutableList<RegionLabel> validationRectangles = dataSet.validation[0].regionLabels;
         ImmutableList<RegionLabel> testRectangles = dataSet.test[0].regionLabels;
         ImmutableList<RegionLabel> im164Rectangles = dataSet.im164[0].regionLabels;
         ImmutableList<IntegrationTestDataSet> allDataSets = [.. dataSet.train, .. dataSet.validation, .. dataSet.test, .. dataSet.im164];

         foreach (IntegrationTestDataSet integrationTestDataSet in allDataSets)
         {
            if (!sourceImages.ContainsKey(integrationTestDataSet.filename))
            {
               string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), integrationTestDataSet.filename);
               Image<L8> sourceImage = Image.Load<L8>(fullImagePath);

               sourceImages = sourceImages.Add(integrationTestDataSet.filename, sourceImage);
               integralImages = integralImages.Add(integrationTestDataSet.filename, sourceImage.CalculateIntegralImage());
            }
         }

         BestSplitLogic bestSplitLogic = new();

         PakiraDecisionTreeGenerator pakiraGenerator = new(bestSplitLogic.GetBestSplit, bestSplitLogic.EndBuildTree);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();
         ImmutableDictionary<int, SampleData> trainPositions;
         ImmutableDictionary<int, SampleData> validationPositions;
         ImmutableDictionary<int, SampleData> testPositions;
         ImmutableDictionary<int, SampleData> im164Positions;

         trainPositions = LoadDataSamples(trainRectangles, 0, 0);
         validationPositions = LoadDataSamples(validationRectangles, trainPositions.Count, 0);
         testPositions = LoadDataSamples(testRectangles, trainPositions.Count + validationPositions.Count, 0);
         im164Positions = LoadDataSamples(im164Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count, 1);

         Buffer2D<ulong> integralImage507484246 = integralImages[dataSet.train[0].filename];
         Buffer2D<ulong> integralImageim164 = integralImages[dataSet.im164[0].filename];

         // Number of transformers per size: 17->1, 7->4, 5->9, 3->25, 1->289
         //ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];
         //ImmutableList<int> averageTransformerSizes = [1, 3, 5, 7, 17];
         //averageTransformerSizes = [17, 7];

         // TODO Maybe AverageWindowFeature could be used to create a new instance with the same internal values but by only changing the positions/intergralImage ?
         AverageWindowFeature trainDataExtractor = new(trainPositions.AddRange(im164Positions), [integralImage507484246, integralImageim164]);
         AverageWindowFeature validationDataExtractor = new(validationPositions, [integralImage507484246]);
         AverageWindowFeature testDataExtractor = new(testPositions, [integralImage507484246]);

         trainDataExtractor.AddAverageTransformer(averageTransformerSizes);
         validationDataExtractor.AddAverageTransformer(averageTransformerSizes);
         testDataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL trainTanukiETL = new(trainDataExtractor.ConvertAll, trainDataExtractor.ExtractLabel, trainDataExtractor.FeaturesCount());
         TanukiETL validationTanukiETL = new(validationDataExtractor.ConvertAll, validationDataExtractor.ExtractLabel, validationDataExtractor.FeaturesCount());
         TanukiETL testTanukiETL = new(testDataExtractor.ConvertAll, testDataExtractor.ExtractLabel, testDataExtractor.FeaturesCount());

         //pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainPositions.Keys.Take(24), trainTanukiETL);
         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainPositions.Keys.Take(1012), trainTanukiETL);

         int totalTrainSamples = trainPositions.Keys.Count();
         ImmutableList<int> trainSampleIds = [];
         ImmutableHashSet<int> trainSampleIdsSet = [.. trainPositions.Keys];

         AccuracyResult trainAccuracyResult;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult im164AccuracyResult;

         // UNDONE The test accuracy result shows that not a single uppercase A is correctly predicted. See if the new batch system and a proper leaf selection fixes this.
         // UNDONE Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the
         //        Test classes, inside the main library
         // UNDONE Note this methodology somewhere: When the validation set contains too many unevaluated leaves we need to apply one of the following solution:
         //        - Increase validation set size
         //        - Optimize the tree size by replacing nodes with better discriminating nodes, thus reducing the number of leaves and/or the depth of the tree
         //        - Other?
         while (!trainSampleIdsSet.IsEmpty)
         {
            PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, trainTanukiETL);

            foreach (int id in trainSampleIdsSet.Take(10))
            {
               int expectedLabel = trainPositions[id].Label;
               IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

               if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
               {
                  trainSampleIds = trainSampleIds.Add(id);
               }
               else
               {
                  trainSampleIdsSet = trainSampleIdsSet.Remove(id);
               }
            }

            if (!trainSampleIds.IsEmpty)
            {
               pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainSampleIds, trainTanukiETL);
               pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);
               trainSampleIds = [];
            }
         }

         trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);
         validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiETL);
         testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiETL);
         im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, im164Positions.Keys, trainTanukiETL);

         PrintFirstNodeIndex(pakiraDecisionTreeModel);
         PrintConfusionMatrix(trainAccuracyResult, "Train");
         PrintConfusionMatrix(validationAccuracyResult, "Validation");
         PrintConfusionMatrix(testAccuracyResult, "Test");
         PrintConfusionMatrix(im164AccuracyResult, "im164");
         PrintLeaveResults(trainAccuracyResult);
         PrintLeaveResults(validationAccuracyResult);
         PrintLeaveResults(testAccuracyResult);
         PrintLeaveResults(im164AccuracyResult);
         PrintEnd();

         // UNDONE Move the batch batch training logic in a seperate method/class to prevent code duplication
         trainSampleIds = ImmutableList<int>.Empty;
         trainSampleIdsSet = [.. im164Positions.Keys];

         while (!trainSampleIdsSet.IsEmpty)
         {
            PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);

            foreach (int id in trainSampleIdsSet.Take(10))
            {
               int expectedLabel = im164Positions[id].Label;
               IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

               if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
               {
                  trainSampleIds = trainSampleIds.Add(id);
               }
               else
               {
                  trainSampleIdsSet = trainSampleIdsSet.Remove(id);
               }
            }

            if (!trainSampleIds.IsEmpty)
            {
               pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainSampleIds, trainTanukiETL);
               pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);
               trainSampleIds = ImmutableList<int>.Empty;
            }
         }

         trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);
         validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiETL);
         testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiETL);
         im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, im164Positions.Keys, trainTanukiETL);

         PrintFirstNodeIndex(pakiraDecisionTreeModel);
         PrintConfusionMatrix(trainAccuracyResult, "Train");
         PrintConfusionMatrix(validationAccuracyResult, "Validation");
         PrintConfusionMatrix(testAccuracyResult, "Test");
         PrintConfusionMatrix(im164AccuracyResult, "im164");
         PrintLeaveResults(trainAccuracyResult);
         PrintLeaveResults(validationAccuracyResult);
         PrintLeaveResults(testAccuracyResult);
         PrintLeaveResults(im164AccuracyResult);
         PrintEnd();
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
            int falsePositivesCount = accuracyResult.falsePositives.GetValueOrDefault(leaf, []).Count;

            if (falsePositivesCount > 0)
            {
               int truePositivesCount = accuracyResult.truePositives.GetValueOrDefault(leaf, []).Count;

               output.WriteLine("Leaf: {0} - {1} true positives, {2} false positives", String.Join(" ", leaf.LabelValues.Select(item => item.ToString()).ToArray()), truePositivesCount, falsePositivesCount);
            }
         }
      }

      private void PrintLeaveResults(AccuracyResult accuracyResult)
      {
         output.WriteLine("{0}/{1} = {2}%", accuracyResult.leavesAfter.Count.ToString(), accuracyResult.leavesBefore.Count.ToString(), 100.0 * accuracyResult.leavesAfter.Count / accuracyResult.leavesBefore.Count);
      }

      private void PrintEnd()
      {
         output.WriteLine("---");
      }
   }
}
