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
      public List<IntegrationTestDataSet> im10 = [];
      public List<IntegrationTestDataSet> ti31149327_9330 = [];

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

   public record TreeNodeSplit
   {
      static double CalculateEntropy(IEnumerable<int> counts)
      {
         int total = 0;

         foreach (int count in counts)
         {
            total += count;
         }

         double entropy = 0.0;

         foreach (int count in counts)
         {
            count.ShouldNotBe(0);

            double p = (double)count / total;

            entropy -= p * Math.Log(p, 2); // log base 2
         }

         return entropy;
      }

      public Tuple<int, double> GetBestSplit(IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO Instead of shuffling randomly, it might make more sense to simply cycle through all available feature indices sequentially. or all data transformers sequentially
         // and then randomly within each transformer.
         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         IEnumerable<int> featureIndices = Enumerable.Range(0, tanukiETL.TanukiFeatureCount);
         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = ImmutableList<double>.Empty;
         ImmutableList<int> sampleIds = ids.Take(1000).ToImmutableList();

         foreach (int featureIndex in featureIndices)
         {
            ImmutableList<int> transformedData = sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex)).ToImmutableList();
            // UNDONE The split value could be iterated as we are very dependent on data distribution here.
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
            double weightedEntropy = (leftTotalCount * leftEntropy + rightTotalCount * rightEntropy) / transformedData.Count;

            weigthedEntropies = weigthedEntropies.Add(weightedEntropy);

            if (bestFeature == -1 || weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature])
            {
               bestFeature = featureIndex;
               bestFeatureSplit = splitValue;
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
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests // ncrunch: no coveraget
   {
      // TODO Add more classes
      static readonly int uppercaseA = 1; // ncrunch: no coverage
      static readonly int other = 2; // ncrunch: no coverage

      static private readonly ImmutableList<Rectangle> train_507484246_Rectangles =
      [
         //new Rectangle(83, 150, 1, 1),
         //new Rectangle(624, 140, 1, 1),
         //new Rectangle(670, 140, 1, 1),
         //new Rectangle(688, 140, 1, 1),
         //new Rectangle(36, 196, 1, 1),
         //new Rectangle(192, 197, 1, 1),
         //new Rectangle(181, 213, 1, 1),
         //new Rectangle(576, 216, 1, 1),
         //new Rectangle(603, 217, 1, 1),
         //new Rectangle(658, 217, 1, 1),
         //new Rectangle(109, 333, 1, 1),
         //new Rectangle(127, 333, 1, 1),

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

      static public IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new();
         IntegrationTestDataSet trainIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels);
         IntegrationTestDataSet validationIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels);
         IntegrationTestDataSet testIntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels);
         IntegrationTestDataSet im164IntegrationTestDataSet = new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels);
         IntegrationTestDataSet im10IntegrationTestDataSet = new(@"assets/mirflickr08/im10.jpg", im10Rectangles, im10Labels);
         IntegrationTestDataSet ti31149327_9330IntegrationTestDataSet = new(@"assets/text-extraction-for-ocr/ti31149327_9330.tif", train_ti31149327_9330_Rectangles, train_ti31149327_9330_Labels);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);
         dataSet.im164.Add(im164IntegrationTestDataSet);
         dataSet.im10.Add(im10IntegrationTestDataSet);
         dataSet.ti31149327_9330.Add(ti31149327_9330IntegrationTestDataSet);

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

         PakiraDecisionTreeGenerator pakiraGenerator = new(bestSplitLogic.GetBestSplit);
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
         //ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
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

         ImmutableHashSet<int> retrainIds = ImmutableHashSet<int>.Empty;
         PakiraDecisionTreeModel initialPakiraDecisionTreeModel = new();
         int previousRetrainIdCount = -1;
         AccuracyResult trainAccuracyResult;
         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult im164AccuracyResult;
         AccuracyResult im10AccuracyResult;
         IEnumerable<int> allTrainIds = trainPositions.Keys.Union(im164Positions.Keys).Union(im10Positions.Keys);//.Union(ti31149327_9330Positions.Keys);
         //IEnumerable<int> allTrainIds = trainPositions.Keys.Union(ti31149327_9330Positions.Keys);

         // UNDONE Find a better condition to stop the training loop
         while (previousRetrainIdCount != retrainIds.Count)
         {
            PakiraDecisionTreeModel pakiraDecisionTreeModelAllData;
            PakiraDecisionTreeModel pakiraDecisionTreeModelLimitedDistribution = initialPakiraDecisionTreeModel;

            previousRetrainIdCount = retrainIds.Count;

            pakiraDecisionTreeModelAllData = pakiraGenerator.Generate(initialPakiraDecisionTreeModel, allTrainIds, trainTanukiETL);

            trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, trainPositions, trainTanukiETL);
            validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, validationPositions, validationTanukiETL);
            testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, testPositions, testTanukiETL);
            im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im164Positions, trainTanukiETL);
            im10AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelAllData, im10Positions, trainTanukiETL);

            PrintFirstNodeIndex(pakiraDecisionTreeModelAllData);
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

            return;
            foreach (KeyValuePair<PakiraNode, PakiraLeaf> pair in pakiraDecisionTreeModelAllData.Tree.GetLeaves())
            {
               PakiraLeaf pakiraLeaf = pair.Value;
               ImmutableList<int> dataSamples = pakiraDecisionTreeModelAllData.DataSamples(pakiraLeaf);
               ImmutableHashSet<int> foundlabels = ImmutableHashSet<int>.Empty;

               foreach (int id in dataSamples)
               {
                  int label = trainPositions[id].Label;

                  if (!foundlabels.Contains(label))
                  {
                     foundlabels = foundlabels.Add(label);
                     retrainIds = retrainIds.Add(id);
                  }
               }
            }

            pakiraDecisionTreeModelLimitedDistribution = pakiraGenerator.Generate(pakiraDecisionTreeModelLimitedDistribution, retrainIds, trainTanukiETL);
            initialPakiraDecisionTreeModel = pakiraDecisionTreeModelLimitedDistribution;

            trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelLimitedDistribution, trainPositions, trainTanukiETL);
            validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelLimitedDistribution, validationPositions, validationTanukiETL);
            testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelLimitedDistribution, testPositions, testTanukiETL);
            im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModelLimitedDistribution, im164Positions, trainTanukiETL);

            PrintFirstNodeIndex(pakiraDecisionTreeModelLimitedDistribution);
            PrintConfusionMatrix(trainAccuracyResult, "Train2");
            PrintConfusionMatrix(validationAccuracyResult, "Validation2");
            PrintConfusionMatrix(testAccuracyResult, "Test2");
            PrintConfusionMatrix(im164AccuracyResult, "im164");
            PrintLeaveResults(trainAccuracyResult);
            PrintLeaveResults(validationAccuracyResult);
            PrintLeaveResults(testAccuracyResult);
            PrintLeaveResults(im164AccuracyResult);
            PrintEnd();
         }

         initialPakiraDecisionTreeModel = pakiraGenerator.Generate(new(), retrainIds, trainTanukiETL);
         initialPakiraDecisionTreeModel = pakiraGenerator.Generate(initialPakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);

         trainAccuracyResult = ComputeAccuracy(initialPakiraDecisionTreeModel, trainPositions, trainTanukiETL);
         validationAccuracyResult = ComputeAccuracy(initialPakiraDecisionTreeModel, validationPositions, validationTanukiETL);
         testAccuracyResult = ComputeAccuracy(initialPakiraDecisionTreeModel, testPositions, testTanukiETL);
         im164AccuracyResult = ComputeAccuracy(initialPakiraDecisionTreeModel, im164Positions, trainTanukiETL);

         PrintFirstNodeIndex(initialPakiraDecisionTreeModel);
         PrintConfusionMatrix(trainAccuracyResult, "Train3");
         PrintConfusionMatrix(validationAccuracyResult, "Validation3");
         PrintConfusionMatrix(testAccuracyResult, "Test3");
         PrintConfusionMatrix(im164AccuracyResult, "im164");
         PrintLeaveResults(trainAccuracyResult);
         PrintLeaveResults(validationAccuracyResult);
         PrintLeaveResults(testAccuracyResult);
         PrintLeaveResults(im164AccuracyResult);
         PrintEnd();

         int totalTrainSamples = trainPositions.Keys.Count();
         ImmutableList<int> trainSampleIds = [];
         ImmutableHashSet<int> trainSampleIdsSet = [.. trainPositions.Keys];

         // TODO Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the
         //        Test classes, inside the main library
         //while (!trainSampleIdsSet.IsEmpty)
         //{
         //   PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, trainTanukiETL);

         //   foreach (int id in trainSampleIdsSet.Take(10))
         //   {
         //      int expectedLabel = trainPositions[id].Label;
         //      IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

         //      if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
         //      {
         //         trainSampleIds = trainSampleIds.Add(id);
         //      }
         //      else
         //      {
         //         trainSampleIdsSet = trainSampleIdsSet.Remove(id);
         //      }
         //   }

         //   if (!trainSampleIds.IsEmpty)
         //   {
         //      pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainSampleIds, trainTanukiETL);
         //      pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);
         //      trainSampleIds = [];
         //   }
         //}

         //trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);
         //validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiETL);
         //testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiETL);
         //im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, im164Positions.Keys, trainTanukiETL);

         //PrintFirstNodeIndex(pakiraDecisionTreeModel);
         //PrintConfusionMatrix(trainAccuracyResult, "Train");
         //PrintConfusionMatrix(validationAccuracyResult, "Validation");
         //PrintConfusionMatrix(testAccuracyResult, "Test");
         //PrintConfusionMatrix(im164AccuracyResult, "im164");
         //PrintLeaveResults(trainAccuracyResult);
         //PrintLeaveResults(validationAccuracyResult);
         //PrintLeaveResults(testAccuracyResult);
         //PrintLeaveResults(im164AccuracyResult);
         //PrintEnd();

         //// UNDONE Move the batch batch training logic in a seperate method/class to prevent code duplication
         //trainSampleIds = ImmutableList<int>.Empty;
         //trainSampleIdsSet = [.. im164Positions.Keys];

         //while (!trainSampleIdsSet.IsEmpty)
         //{
         //   PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);

         //   foreach (int id in trainSampleIdsSet.Take(10))
         //   {
         //      int expectedLabel = im164Positions[id].Label;
         //      IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

         //      if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
         //      {
         //         trainSampleIds = trainSampleIds.Add(id);
         //      }
         //      else
         //      {
         //         trainSampleIdsSet = trainSampleIdsSet.Remove(id);
         //      }
         //   }

         //   if (!trainSampleIds.IsEmpty)
         //   {
         //      pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, trainSampleIds, trainTanukiETL);
         //      pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiETL);
         //      trainSampleIds = ImmutableList<int>.Empty;
         //   }
         //}

         //trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiETL);
         //validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiETL);
         //testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiETL);
         //im164AccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, im164Positions.Keys, trainTanukiETL);

         //PrintFirstNodeIndex(pakiraDecisionTreeModel);
         //PrintConfusionMatrix(trainAccuracyResult, "Train");
         //PrintConfusionMatrix(validationAccuracyResult, "Validation");
         //PrintConfusionMatrix(testAccuracyResult, "Test");
         //PrintConfusionMatrix(im164AccuracyResult, "im164");
         //PrintLeaveResults(trainAccuracyResult);
         //PrintLeaveResults(validationAccuracyResult);
         //PrintLeaveResults(testAccuracyResult);
         //PrintLeaveResults(im164AccuracyResult);
         //PrintEnd();
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
