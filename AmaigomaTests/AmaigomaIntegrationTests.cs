global using BinaryTreeLeaf = (int id, int labelValue);

using Amaigoma;
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
      public List<IntegrationTestDataSet> im10 = [];
      public List<IntegrationTestDataSet> ti31149327_9330 = [];
      public List<IntegrationTestDataSet> trainOptimized = [];

      public DataSet()
      {
      }
   }

   public struct AccuracyResult
   {
      public ImmutableHashSet<BinaryTreeLeaf> leavesBefore;
      public ImmutableHashSet<BinaryTreeLeaf> leavesAfter;
      public ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> truePositives = ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>>.Empty;
      public ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> falsePositives = ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>>.Empty;

      public AccuracyResult()
      {
      }
   }

   public record TreeNodeSplit
   {
      static double CalculateEntropy(IEnumerable<int> counts)
      {
         int total = counts.Sum();
         double entropy = 0.0;

         foreach (int count in counts)
         {
            count.ShouldNotBe(0);

            double p = (double)count / total;

            entropy -= p * Math.Log2(p);
         }

         return entropy;
      }

      public static (int featureIndex, double splitThreshold) GetBestSplitBaseline(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;
         double bestWeigthedEntropy = 128.0;

         ImmutableList<int> sampleIds = [.. ids.Take(1000)];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               double splitValue = transformedData.Average();

               ImmutableDictionary<int, int> leftLabelTotalCount = ImmutableDictionary<int, int>.Empty;
               ImmutableDictionary<int, int> rightLabelTotalCount = ImmutableDictionary<int, int>.Empty;
               int leftTotalCount = 0;
               int rightTotalCount = 0;

               for (int i = 0; i < transformedData.Count; i++)
               {
                  int id = sampleIds[i];
                  int label = tanukiETL.TanukiLabelExtractor(id);

                  if (transformedData[i] <= splitValue)
                  {
                     if (leftLabelTotalCount.ContainsKey(label))
                     {
                        leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + 1);
                     }
                     else
                     {
                        leftLabelTotalCount = leftLabelTotalCount.Add(label, 1);
                     }

                     leftTotalCount++;
                  }
                  else
                  {
                     if (rightLabelTotalCount.ContainsKey(label))
                     {
                        rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] + 1);
                     }
                     else
                     {
                        rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                     }

                     rightTotalCount++;
                  }
               }

               double leftEntropy = CalculateEntropy(leftLabelTotalCount.Select(c => c.Value));
               double rightEntropy = CalculateEntropy(rightLabelTotalCount.Select(c => c.Value));
               double weightedEntropy = (leftTotalCount * leftEntropy + rightTotalCount * rightEntropy);

               if (bestFeature == -1 || weightedEntropy < bestWeigthedEntropy)
               {
                  bestFeature = featureIndex;
                  bestFeatureSplit = splitValue;
                  bestWeigthedEntropy = weightedEntropy;
               }
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);
         bestWeigthedEntropy /= sampleIds.Count;

         return (bestFeature, bestFeatureSplit);
      }

      public static (int featureIndex, double splitThreshold) GetBestSplitOptimized(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = ImmutableList<double>.Empty;
         ImmutableList<int> sampleIds = [.. ids];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;

               // UNDONE Optimization: Instead of iterating over all possible split values, put the transformed data in histograms and then find the split value using the histograms
               (int minValue, int maxValue) = transformedData.Aggregate(
                     (minValue: int.MaxValue, maxValue: int.MinValue),
                     (accumulator, transformedValue) =>
                     (Math.Min(transformedValue, accumulator.minValue), Math.Max(transformedValue, accumulator.maxValue)));

               for (int splitValue = minValue; splitValue < maxValue; splitValue++)
               {
                  ImmutableDictionary<int, int> leftLabelTotalCount = ImmutableDictionary<int, int>.Empty;
                  ImmutableDictionary<int, int> rightLabelTotalCount = ImmutableDictionary<int, int>.Empty;
                  int leftTotalCount = 0;
                  int rightTotalCount = 0;

                  for (int i = 0; i < transformedData.Count; i++)
                  {
                     int id = sampleIds[i];
                     int label = tanukiETL.TanukiLabelExtractor(id);

                     if (transformedData[i] <= splitValue)
                     {
                        if (leftLabelTotalCount.ContainsKey(label))
                        {
                           leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + 1);
                        }
                        else
                        {
                           leftLabelTotalCount = leftLabelTotalCount.Add(label, 1);
                        }

                        leftTotalCount++;
                     }
                     else
                     {
                        if (rightLabelTotalCount.ContainsKey(label))
                        {
                           rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] + 1);
                        }
                        else
                        {
                           rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                        }

                        rightTotalCount++;
                     }
                  }

                  double leftEntropy = CalculateEntropy(leftLabelTotalCount.Select(c => c.Value));
                  double rightEntropy = CalculateEntropy(rightLabelTotalCount.Select(c => c.Value));
                  double weightedEntropy = (leftTotalCount * leftEntropy + rightTotalCount * rightEntropy);

                  if (weightedEntropy < bestWeightedEntropy)
                  {
                     bestWeightedEntropy = weightedEntropy;
                     bestSplitValue = splitValue;
                     bestSplitCount = 0;
                  }
                  else if (weightedEntropy == bestWeightedEntropy)
                  {
                     bestSplitValue.ShouldBeGreaterThanOrEqualTo(0);
                     bestSplitCount++;
                  }
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 || weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature])
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += (bestSplitCount / 2);
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
               }
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
         weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);

         return (bestFeature, bestFeatureSplit);
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests // ncrunch: no coveraget
   {
      // TODO Add more classes
      static readonly int uppercaseA = 1; // ncrunch: no coverage
      static readonly int other = 2; // ncrunch: no coverage

      static private readonly ImmutableList<Rectangle> train_507484246_Rectangles =
      [
         new Rectangle(82, 149, 3, 3),
         new Rectangle(623, 139, 3, 3),
         new Rectangle(669, 139, 3, 3),
         new Rectangle(687, 139, 3, 3),
         new Rectangle(35, 195, 3, 3),
         new Rectangle(191, 196, 3, 3),
         new Rectangle(180, 212, 3, 3),
         new Rectangle(575, 215, 3, 3),
         new Rectangle(602, 216, 3, 3),
         new Rectangle(657, 216, 3, 3),
         new Rectangle(108, 332, 3, 3),
         new Rectangle(126, 332, 3, 3),

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

      static private readonly ImmutableList<Rectangle> validation_507484246_Rectangles =
      [
         new Rectangle(229, 334, 1, 1),
         new Rectangle(283, 335, 1, 1),
         new Rectangle(153, 409, 1, 1),
         new Rectangle(217, 519, 1, 1),
         new Rectangle(155, 549, 1, 1),
         new Rectangle(190, 540, 280, 20),
         new Rectangle(20, 555, 480, 215),
      ];

      static private readonly ImmutableList<int> validation_507484246_Labels =
         [
         uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA,
         other, other
         ];

      static private readonly ImmutableList<Rectangle> test_507484246_Rectangles =
      [
         new Rectangle(218, 790, 1, 1),
         new Rectangle(411, 836, 1, 1),
         new Rectangle(137, 851, 1, 1),
         new Rectangle(257, 851, 1, 1),
         new Rectangle(605, 851, 1, 1),
         new Rectangle(520, 550, 230, 216),
         new Rectangle(95, 810, 500, 20),
         new Rectangle(20, 900, 740, 70),
         new Rectangle(180, 960, 310, 23),
      ];

      static private readonly ImmutableList<int> test_507484246_Labels =
      [
         uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA,
         other, other, other, other
      ];

      static private readonly ImmutableList<Rectangle> im164Rectangles = [new Rectangle(8, 8, 483, 358)];
      static private readonly ImmutableList<int> im164Labels = [other];
      static private readonly ImmutableList<Rectangle> im10Rectangles = [new Rectangle(8, 8, 483, 316)];
      static private readonly ImmutableList<int> im10Labels = [other];

      static private readonly ImmutableList<Rectangle> train_ti31149327_9330_Rectangles =
      [
         new Rectangle(270, 360, 95, 65),
         new Rectangle(120, 525, 500, 210),
         new Rectangle(130, 815, 480, 30),
      ];

      static private readonly ImmutableList<int> train_ti31149327_9330_Labels =
      [
         other,
         other,
         other,
      ];

      static private readonly ImmutableList<Rectangle> trainOptimized_507484246_Rectangles =
      [
         new Rectangle(82, 149, 3, 3),
         new Rectangle(20, 420, 3, 3),
      ];

      static private readonly ImmutableList<int> trainOptimized_507484246_Labels =
      [
         uppercaseA,
         other,
      ];

      static public IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new();
         // UNDONE Regroup all images and prepare the integral images in parallel and a as pre-step for the test
         IntegrationTestDataSet trainIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels);
         IntegrationTestDataSet validationIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels);
         IntegrationTestDataSet testIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels);
         IntegrationTestDataSet im164IntegrationTestDataSet = new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels);
         IntegrationTestDataSet im10IntegrationTestDataSet = new(@"assets/mirflickr08/im10.jpg", im10Rectangles, im10Labels);
         IntegrationTestDataSet ti31149327_9330IntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/ti31149327_9330.tif", train_ti31149327_9330_Rectangles, train_ti31149327_9330_Labels);
         IntegrationTestDataSet trainOptimizedIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", trainOptimized_507484246_Rectangles, trainOptimized_507484246_Labels);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);
         dataSet.im164.Add(im164IntegrationTestDataSet);
         dataSet.im10.Add(im10IntegrationTestDataSet);
         dataSet.ti31149327_9330.Add(ti31149327_9330IntegrationTestDataSet);
         dataSet.trainOptimized.Add(trainOptimizedIntegrationTestDataSet);

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

      static private AccuracyResult ComputeAccuracy(PakiraDecisionTreeModel pakiraDecisionTreeModel, ImmutableDictionary<int, SampleData> positions, TanukiETL tanukiETL)
      {
         IEnumerable<int> ids = positions.Keys;
         ImmutableHashSet<BinaryTreeLeaf> leaves = [.. pakiraDecisionTreeModel.Tree.Leaves()];
         AccuracyResult accuracyResult = new()
         {
            leavesBefore = leaves
         };

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         foreach (int id in ids)
         {
            BinaryTreeLeaf BinaryTreeLeafResult = pakiraTreeWalker.PredictLeaf(id);
            int label = tanukiETL.TanukiLabelExtractor(id);

            if (BinaryTreeLeafResult.labelValue == label)
            {
               if (accuracyResult.truePositives.ContainsKey(BinaryTreeLeafResult))
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.SetItem(BinaryTreeLeafResult, accuracyResult.truePositives[BinaryTreeLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.Add(BinaryTreeLeafResult, [id]);
               }
            }
            else
            {
               if (accuracyResult.falsePositives.ContainsKey(BinaryTreeLeafResult))
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.SetItem(BinaryTreeLeafResult, accuracyResult.falsePositives[BinaryTreeLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.Add(BinaryTreeLeafResult, [id]);
               }

               leaves = leaves.Remove(BinaryTreeLeafResult);
            }
         }

         accuracyResult.leavesAfter = leaves;

         return accuracyResult;
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      public void UppercaseA_507484246_Baseline(DataSet dataSet)
      {
         ImmutableDictionary<string, Image<L8>> sourceImages = ImmutableDictionary<string, Image<L8>>.Empty;
         ImmutableDictionary<string, Buffer2D<ulong>> integralImages = ImmutableDictionary<string, Buffer2D<ulong>>.Empty;
         ImmutableList<RegionLabel> trainRectangles = dataSet.train[0].regionLabels;
         ImmutableList<RegionLabel> validationRectangles = dataSet.validation[0].regionLabels;
         ImmutableList<RegionLabel> testRectangles = dataSet.test[0].regionLabels;
         ImmutableList<RegionLabel> im164Rectangles = dataSet.im164[0].regionLabels;
         ImmutableList<RegionLabel> im10Rectangles = dataSet.im10[0].regionLabels;
         ImmutableList<RegionLabel> ti31149327_9330Rectangles = dataSet.ti31149327_9330[0].regionLabels;
         ImmutableList<IntegrationTestDataSet> allDataSets = [.. dataSet.train, .. dataSet.validation, .. dataSet.test, .. dataSet.im164, .. dataSet.im10, .. dataSet.ti31149327_9330];

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

         TreeNodeSplit bestSplitLogic = new();

         PakiraDecisionTreeGenerator pakiraGenerator = new(TreeNodeSplit.GetBestSplitBaseline);
         ImmutableDictionary<int, SampleData> trainPositions;
         ImmutableDictionary<int, SampleData> validationPositions;
         ImmutableDictionary<int, SampleData> testPositions;
         ImmutableDictionary<int, SampleData> im164Positions;
         ImmutableDictionary<int, SampleData> im10Positions;
         ImmutableDictionary<int, SampleData> ti31149327_9330Positions;

         // TODO Need to simplify the logic to add more data samples
         trainPositions = LoadDataSamples(trainRectangles, 0, 0);
         validationPositions = LoadDataSamples(validationRectangles, trainPositions.Count, 0);
         testPositions = LoadDataSamples(testRectangles, trainPositions.Count + validationPositions.Count, 0);
         im164Positions = LoadDataSamples(im164Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count, 1);
         im10Positions = LoadDataSamples(im10Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count + im164Positions.Count, 2);
         ti31149327_9330Positions = LoadDataSamples(ti31149327_9330Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count + im164Positions.Count + im10Positions.Count, 3);

         Buffer2D<ulong> integralImage507484246 = integralImages[dataSet.train[0].filename];
         Buffer2D<ulong> integralImageim164 = integralImages[dataSet.im164[0].filename];
         Buffer2D<ulong> integralImageim10 = integralImages[dataSet.im10[0].filename];
         Buffer2D<ulong> integralImageti31149327_9330 = integralImages[dataSet.ti31149327_9330[0].filename];

         // Number of transformers per size: 17->1, 7->1, 5->9, 3->25, 1->289
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];

         // TODO Maybe AverageWindowFeature could be used to create a new instance with the same internal values but by only changing the positions/intergralImage ?
         AverageWindowFeature trainDataExtractor = new(trainPositions.AddRange(im164Positions).AddRange(im10Positions).AddRange(ti31149327_9330Positions), [integralImage507484246, integralImageim164, integralImageim10, integralImageti31149327_9330]);
         AverageWindowFeature validationDataExtractor = new(validationPositions, [integralImage507484246]);
         AverageWindowFeature testDataExtractor = new(testPositions, [integralImage507484246]);

         trainDataExtractor.AddAverageTransformer(averageTransformerSizes);
         validationDataExtractor.AddAverageTransformer(averageTransformerSizes);
         testDataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL trainTanukiETL = new(trainDataExtractor.ConvertAll, trainDataExtractor.ExtractLabel, trainDataExtractor.FeaturesCount());
         TanukiETL validationTanukiETL = new(validationDataExtractor.ConvertAll, validationDataExtractor.ExtractLabel, validationDataExtractor.FeaturesCount());
         TanukiETL testTanukiETL = new(testDataExtractor.ConvertAll, testDataExtractor.ExtractLabel, testDataExtractor.FeaturesCount());

         PakiraDecisionTreeModel initialPakiraDecisionTreeModel = new();
         AccuracyResult trainAccuracyResult;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult im164AccuracyResult;
         AccuracyResult im10AccuracyResult;
         IEnumerable<int> allTrainIds = trainPositions.Keys.Union(im164Positions.Keys).Union(im10Positions.Keys);//.Union(ti31149327_9330Positions.Keys);

         PakiraDecisionTreeModel pakiraDecisionTreeModelAllData;

         pakiraDecisionTreeModelAllData = pakiraGenerator.Generate(initialPakiraDecisionTreeModel, allTrainIds, trainTanukiETL);

         trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, trainPositions, trainTanukiETL);
         validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, validationPositions, validationTanukiETL);
         testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, testPositions, testTanukiETL);
         im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im164Positions, trainTanukiETL);
         im10AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im10Positions, trainTanukiETL);

         PrintConfusionMatrix(trainAccuracyResult, "Train");
         PrintConfusionMatrix(validationAccuracyResult, "Validation");
         PrintConfusionMatrix(testAccuracyResult, "Test");
         PrintConfusionMatrix(im164AccuracyResult, "im164");
         PrintConfusionMatrix(im10AccuracyResult, "im10");
         PrintLeaveResults(trainAccuracyResult);
         PrintLeaveResults(validationAccuracyResult);
         PrintLeaveResults(testAccuracyResult);
         PrintLeaveResults(im164AccuracyResult);
         PrintLeaveResults(im10AccuracyResult);
         PrintEnd();

         trainAccuracyResult.leavesAfter.Count.ShouldBe(91);
         validationAccuracyResult.leavesAfter.Count.ShouldBe(71);
         validationAccuracyResult.leavesBefore.Count.ShouldBe(91);
         testAccuracyResult.leavesAfter.Count.ShouldBe(76);
         testAccuracyResult.leavesBefore.Count.ShouldBe(91);
         im164AccuracyResult.leavesAfter.Count.ShouldBe(91);
         im10AccuracyResult.leavesAfter.Count.ShouldBe(91);
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      // UNDONE Complete this integration test
      public void UppercaseA_507484246_AccuracyOptimized(DataSet dataSet)
      {
         ImmutableDictionary<string, Image<L8>> sourceImages = ImmutableDictionary<string, Image<L8>>.Empty;
         ImmutableDictionary<string, Buffer2D<ulong>> integralImages = ImmutableDictionary<string, Buffer2D<ulong>>.Empty;
         ImmutableList<RegionLabel> trainRectangles = dataSet.train[0].regionLabels;
         ImmutableList<RegionLabel> validationRectangles = dataSet.validation[0].regionLabels;
         ImmutableList<RegionLabel> testRectangles = dataSet.test[0].regionLabels;
         ImmutableList<RegionLabel> im164Rectangles = dataSet.im164[0].regionLabels;
         ImmutableList<RegionLabel> im10Rectangles = dataSet.im10[0].regionLabels;
         ImmutableList<RegionLabel> ti31149327_9330Rectangles = dataSet.ti31149327_9330[0].regionLabels;
         ImmutableList<RegionLabel> trainOptimizedRectangles = dataSet.trainOptimized[0].regionLabels;
         ImmutableList<IntegrationTestDataSet> allDataSets = [.. dataSet.train, .. dataSet.validation, .. dataSet.test, .. dataSet.im164, .. dataSet.im10, .. dataSet.ti31149327_9330, .. dataSet.trainOptimized];

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

         TreeNodeSplit bestSplitLogic = new();

         PakiraDecisionTreeGenerator pakiraGenerator = new(TreeNodeSplit.GetBestSplitOptimized);
         ImmutableDictionary<int, SampleData> trainPositions;
         ImmutableDictionary<int, SampleData> validationPositions;
         ImmutableDictionary<int, SampleData> testPositions;
         ImmutableDictionary<int, SampleData> im164Positions;
         ImmutableDictionary<int, SampleData> im10Positions;
         ImmutableDictionary<int, SampleData> ti31149327_9330Positions;
         ImmutableDictionary<int, SampleData> trainOptimizedPositions;

         // TODO Need to simplify the logic to add more data samples
         trainPositions = LoadDataSamples(trainRectangles, 0, 0);
         validationPositions = LoadDataSamples(validationRectangles, trainPositions.Count, 0);
         testPositions = LoadDataSamples(testRectangles, trainPositions.Count + validationPositions.Count, 0);
         im164Positions = LoadDataSamples(im164Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count, 1);
         im10Positions = LoadDataSamples(im10Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count + im164Positions.Count, 2);
         ti31149327_9330Positions = LoadDataSamples(ti31149327_9330Rectangles, trainPositions.Count + validationPositions.Count + testPositions.Count + im164Positions.Count + im10Positions.Count, 3);
         trainOptimizedPositions = LoadDataSamples(trainOptimizedRectangles, trainPositions.Count + validationPositions.Count + testPositions.Count + im164Positions.Count + im10Positions.Count + ti31149327_9330Positions.Count, 0);

         Buffer2D<ulong> integralImage507484246 = integralImages[dataSet.train[0].filename];
         Buffer2D<ulong> integralImageim164 = integralImages[dataSet.im164[0].filename];
         Buffer2D<ulong> integralImageim10 = integralImages[dataSet.im10[0].filename];
         Buffer2D<ulong> integralImageti31149327_9330 = integralImages[dataSet.ti31149327_9330[0].filename];

         // Number of transformers per size: 17->1, 7->1, 5->9, 3->25, 1->289
         //ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];

         // TODO Maybe AverageWindowFeature could be used to create a new instance with the same internal values but by only changing the positions/intergralImage ?
         AverageWindowFeature trainDataExtractor = new(trainPositions.AddRange(im164Positions).AddRange(im10Positions).AddRange(ti31149327_9330Positions).AddRange(trainOptimizedPositions), [integralImage507484246, integralImageim164, integralImageim10, integralImageti31149327_9330]);
         AverageWindowFeature validationDataExtractor = new(validationPositions, [integralImage507484246]);
         AverageWindowFeature testDataExtractor = new(testPositions, [integralImage507484246]);

         trainDataExtractor.AddAverageTransformer(averageTransformerSizes);
         validationDataExtractor.AddAverageTransformer(averageTransformerSizes);
         testDataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL trainTanukiETL = new(trainDataExtractor.ConvertAll, trainDataExtractor.ExtractLabel, trainDataExtractor.FeaturesCount());
         TanukiETL validationTanukiETL = new(validationDataExtractor.ConvertAll, validationDataExtractor.ExtractLabel, validationDataExtractor.FeaturesCount());
         TanukiETL testTanukiETL = new(testDataExtractor.ConvertAll, testDataExtractor.ExtractLabel, testDataExtractor.FeaturesCount());

         PakiraDecisionTreeModel initialPakiraDecisionTreeModel = new();
         AccuracyResult trainAccuracyResult;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult im164AccuracyResult;
         AccuracyResult im10AccuracyResult;
         AccuracyResult ti31149327_9330AccuracyResult;
         AccuracyResult trainOptimizedAccuracyResult;
         IEnumerable<int> allTrainIds = trainPositions.Keys.Take(18);

         PakiraDecisionTreeModel pakiraDecisionTreeModelAllData;

         pakiraDecisionTreeModelAllData = pakiraGenerator.Generate(initialPakiraDecisionTreeModel, allTrainIds, trainTanukiETL);

         trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, trainPositions, trainTanukiETL);
         validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, validationPositions, validationTanukiETL);
         testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, testPositions, testTanukiETL);
         im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im164Positions, trainTanukiETL);
         im10AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im10Positions, trainTanukiETL);
         ti31149327_9330AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, ti31149327_9330Positions, trainTanukiETL);
         trainOptimizedAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, trainOptimizedPositions, trainTanukiETL);

         PrintConfusionMatrix(trainAccuracyResult, "Train");
         PrintConfusionMatrix(validationAccuracyResult, "Validation");
         PrintConfusionMatrix(testAccuracyResult, "Test");
         PrintConfusionMatrix(im164AccuracyResult, "im164");
         PrintConfusionMatrix(im10AccuracyResult, "im10");
         PrintConfusionMatrix(ti31149327_9330AccuracyResult, "ti31149327_9330");
         PrintConfusionMatrix(trainOptimizedAccuracyResult, "TrainOptimized");
         PrintLeaveResults(trainAccuracyResult);
         PrintLeaveResults(validationAccuracyResult);
         PrintLeaveResults(testAccuracyResult);
         PrintLeaveResults(im164AccuracyResult);
         PrintLeaveResults(im10AccuracyResult);
         PrintLeaveResults(ti31149327_9330AccuracyResult);
         PrintLeaveResults(trainOptimizedAccuracyResult);
         PrintEnd();

         // UNDONE Add the appropriate validations here when the test is completed
         //trainAccuracyResult.leavesAfter.Count.ShouldBe(91);
         //validationAccuracyResult.leavesAfter.Count.ShouldBe(71);
         //validationAccuracyResult.leavesBefore.Count.ShouldBe(91);
         //testAccuracyResult.leavesAfter.Count.ShouldBe(76);
         //testAccuracyResult.leavesBefore.Count.ShouldBe(91);
         //im164AccuracyResult.leavesAfter.Count.ShouldBe(91);
         //im10AccuracyResult.leavesAfter.Count.ShouldBe(91);
      }

      private void PrintConfusionMatrix(AccuracyResult accuracyResult, string title)
      {
         int totalFalsePositivesCount = 0;

         output.WriteLine("Confusion matrix for {0}", title);

         foreach (BinaryTreeLeaf leaf in accuracyResult.leavesBefore)
         {
            int falsePositivesCount = accuracyResult.falsePositives.GetValueOrDefault(leaf, []).Count;

            if (falsePositivesCount > 0)
            {
               int truePositivesCount = accuracyResult.truePositives.GetValueOrDefault(leaf, []).Count;

               output.WriteLine("Leaf: Id: {3} Label:{0} - {1} true positives, {2} false positives", String.Join(" ", leaf.labelValue.ToString()), truePositivesCount, falsePositivesCount, leaf.id);
               totalFalsePositivesCount += falsePositivesCount;
            }
         }

         if (totalFalsePositivesCount > 0)
         {
            output.WriteLine("Total false positives {0}", totalFalsePositivesCount);
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
