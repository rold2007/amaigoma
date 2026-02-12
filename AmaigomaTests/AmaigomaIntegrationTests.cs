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
      private ImmutableDictionary<string, IntegrationTestDataSet> regions = [];
      private ImmutableDictionary<string, ImmutableDictionary<int, SampleData>> positions = [];
      private ImmutableDictionary<string, int> integralImagesIndex = [];
      private int sampleId = 0;

      public DataSet()
      {
      }

      private DataSet(ImmutableDictionary<string, IntegrationTestDataSet> regions, ImmutableDictionary<string, ImmutableDictionary<int, SampleData>> positions, ImmutableDictionary<string, int> integralImagesIndex, int sampleId)
      {
         this.regions = regions;
         this.positions = positions;
         this.integralImagesIndex = integralImagesIndex;
         this.sampleId = sampleId;
      }

      static private ImmutableDictionary<int, SampleData> LoadDataSamples(ImmutableList<RegionLabel> rectangles, int startingIndex, int integralImageIndex)
      {
         ImmutableDictionary<int, SampleData> result = [];

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

      public DataSet AddRegion(string regionName, IntegrationTestDataSet dataSet)
      {
         ImmutableDictionary<string, IntegrationTestDataSet> nextRegions = regions.Add(regionName, dataSet);
         ImmutableDictionary<string, ImmutableDictionary<int, SampleData>> nextPositions = positions;
         ImmutableDictionary<string, int> nextIntegralImagesIndex;
         int nextSampleId = sampleId;

         nextIntegralImagesIndex = integralImagesIndex.ContainsKey(dataSet.filename) ? integralImagesIndex : integralImagesIndex.Add(dataSet.filename, integralImagesIndex.Count);

         ImmutableDictionary<int, SampleData> samplePositions = LoadDataSamples(dataSet.regionLabels, nextSampleId, nextIntegralImagesIndex[dataSet.filename]);

         nextPositions = nextPositions.Add(regionName, samplePositions);
         nextSampleId += samplePositions.Count;

         return new DataSet(nextRegions, nextPositions, nextIntegralImagesIndex, nextSampleId);
      }

      public IntegrationTestDataSet Region(string regionName)
      {
         return regions[regionName];
      }

      public ImmutableDictionary<int, SampleData> Position(string regionName)
      {
         return positions[regionName];
      }

      public int IntegralImageIndex(string regionName)
      {
         return integralImagesIndex[regionName];
      }
   }

   public struct AccuracyResult
   {
      public ImmutableHashSet<BinaryTreeLeaf> leavesBefore;
      public ImmutableHashSet<BinaryTreeLeaf> leavesAfter;
      public ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> truePositives = [];
      public ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> falsePositives = [];

      public AccuracyResult()
      {
      }
   }

   public record TreeNodeSplit
   {
      private static readonly ImmutableList<int> emptyHistogram = [.. Enumerable.Repeat(0, 256)];

      public TreeNodeSplit()
      {
      }

      private static double CalculateEntropy(IEnumerable<int> counts)
      {
         int total = counts.Sum();
         double entropy = 0.0;

         total.ShouldNotBe(0);

         foreach (int count in counts)
         {
            if (count > 0)
            {
               count.ShouldBeGreaterThan(0);

               double p = (double)count / total;

               entropy -= p * Math.Log2(p);
            }
         }

         return entropy;
      }

      // TODO This can be optimzed as we're only dependent on the number of counts, not their values
      private static double CalculateEntropySingle(IEnumerable<int> counts)
      {
         int total = counts.Where(x => x > 0).Count();
         double entropy = 0.0;

         total.ShouldNotBe(0);

         foreach (int count in counts)
         {
            if (count > 0)
            {
               count.ShouldBeGreaterThan(0);

               double p = (double)1 / total;

               entropy -= p * Math.Log2(p);
            }
         }

         return entropy;
      }

      public static (int featureIndex, double splitThreshold) GetBestSplitBaseline(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;

         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = [];

         // TODO This Take(1000) should take 1000 of each class and make sure to spread the take over the whole dataset otherwise all samples will be similar
         ImmutableList<int> sampleIds = [.. ids.Take(1000)];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  if (histograms.TryGetValue(label, out ImmutableList<int> histogram))
                  {
                     histogram = histogram.SetItem(transformedData[i], histogram[transformedData[i]] + 1);
                  }
                  else
                  {
                     histogram = emptyHistogram.SetItem(transformedData[i], 1);
                  }

                  histograms = histograms.SetItem(label, histogram);

                  if (rightLabelTotalCount.TryGetValue(label, out int value))
                  {
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, value + 1);
                  }
                  else
                  {
                     rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                     leftLabelTotalCount = leftLabelTotalCount.Add(label, 0);
                  }
               }

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + binCount);
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] - binCount);
                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

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

      // TODO Add unit tests for this method
      public static (int featureIndex, double splitThreshold) GetBestSplitOptimized(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = [];
         ImmutableList<int> sampleIds = [.. ids];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  if (histograms.TryGetValue(label, out ImmutableList<int> histogram))
                  {
                     histogram = histogram.SetItem(transformedData[i], histogram[transformedData[i]] + 1);
                  }
                  else
                  {
                     histogram = emptyHistogram.SetItem(transformedData[i], 1);
                  }

                  histograms = histograms.SetItem(label, histogram);

                  if (rightLabelTotalCount.TryGetValue(label, out int value))
                  {
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, value + 1);
                  }
                  else
                  {
                     rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                     leftLabelTotalCount = leftLabelTotalCount.Add(label, 0);
                  }
               }

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + binCount);
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] - binCount);
                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

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

      public (int featureIndex, double splitThreshold) GetBestSplitClustering(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;

         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = [];
         ImmutableList<int> sampleIds = [.. ids];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  if (histograms.TryGetValue(label, out ImmutableList<int> histogram))
                  {
                     histogram = histogram.SetItem(transformedData[i], histogram[transformedData[i]] + 1);
                  }
                  else
                  {
                     histogram = emptyHistogram.SetItem(transformedData[i], 1);
                  }

                  histograms = histograms.SetItem(label, histogram);

                  if (rightLabelTotalCount.TryGetValue(label, out int value))
                  {
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, value + 1);
                  }
                  else
                  {
                     rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                     leftLabelTotalCount = leftLabelTotalCount.Add(label, 0);
                  }
               }

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + binCount);
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] - binCount);
                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

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

      public (int featureIndex, double splitThreshold) GetBestSplitClustering3(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         double bestFeatureSplitCount = double.MaxValue;

         // TODO No need to keep all entropies, only the best one
         ImmutableList<double> weigthedEntropies = [];
         ImmutableList<int> sampleIds = [.. ids];

         for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
         {
            ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

            if (!transformedData.IsEmpty)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  if (histograms.TryGetValue(label, out ImmutableList<int> histogram))
                  {
                     histogram = histogram.SetItem(transformedData[i], histogram[transformedData[i]] + 1);
                  }
                  else
                  {
                     histogram = emptyHistogram.SetItem(transformedData[i], 1);
                  }

                  histograms = histograms.SetItem(label, histogram);

                  if (rightLabelTotalCount.TryGetValue(label, out int value))
                  {
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, value + 1);
                  }
                  else
                  {
                     rightLabelTotalCount = rightLabelTotalCount.Add(label, 1);
                     leftLabelTotalCount = leftLabelTotalCount.Add(label, 0);
                  }
               }

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftLabelTotalCount = leftLabelTotalCount.SetItem(label, leftLabelTotalCount[label] + binCount);
                     rightLabelTotalCount = rightLabelTotalCount.SetItem(label, rightLabelTotalCount[label] - binCount);
                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

                     int leftTotalCountForEntropy = leftLabelTotalCount.Where(x => x.Value > 0).Count();
                     int rightTotalCountForEntropy = rightLabelTotalCount.Where(x => x.Value > 0).Count();

                     double leftEntropy = CalculateEntropySingle(leftLabelTotalCount.Select(c => c.Value));
                     double rightEntropy = CalculateEntropySingle(rightLabelTotalCount.Select(c => c.Value));
                     double weightedEntropy = (leftTotalCountForEntropy * leftEntropy + rightTotalCountForEntropy * rightEntropy);

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
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 ||
                  weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestSplitCount < bestFeatureSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += (bestSplitCount / 2);
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
         weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);

         return (bestFeature, bestFeatureSplit);
      }
   }

   public record ImageFixture : IDisposable
   {
      ImmutableList<string> imagePaths = [
         @"assets/text-extraction-for-ocr/507484246.tif",
         @"assets/mirflickr08/im164.jpg",
         @"assets/mirflickr08/im10.jpg",
         @"assets/text-extraction-for-ocr/ti31149327_9330.tif"];

      public ImmutableDictionary<string, Image<L8>> sourceImages = [];
      public ImmutableDictionary<string, Buffer2D<ulong>> integralImages = [];

      public ImageFixture()
      {
         sourceImages = imagePaths.AsParallel().Select(image =>
         {
            string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), image);
            Image<L8> sourceImage = Image.Load<L8>(fullImagePath);
            return (image, sourceImage);
         }).ToImmutableDictionary(t => t.image, t => t.sourceImage);

         integralImages = sourceImages.AsParallel().Select(kv =>
         {
            Buffer2D<ulong> integralImage = kv.Value.CalculateIntegralImage();
            return (kv.Key, integralImage);
         }).ToImmutableDictionary(t => t.Key, t => t.integralImage);
      }

      public void Dispose()
      {
         foreach (var image in sourceImages.Values)
         {
            image.Dispose();
         }

         foreach (var integralImage in integralImages.Values)
         {
            integralImage.Dispose();
         }
      }
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests : IClassFixture<ImageFixture> // ncrunch: no coverage
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
         new Rectangle(290, 17, 3, 3),
         new Rectangle(296, 17, 3, 3),
         new Rectangle(299, 19, 3, 3),
         new Rectangle(293, 21, 3, 3),
         new Rectangle(64, 33, 3, 3),
         new Rectangle(68, 29, 3, 3),
         new Rectangle(295, 28, 3, 3),
         new Rectangle(59, 29, 3, 3),
         new Rectangle(103, 57, 3, 3),
         new Rectangle(711, 99, 3, 3),
         new Rectangle(302, 17, 3, 3),
         new Rectangle(61, 26, 3, 3),
         new Rectangle(659, 55, 3, 3),
         new Rectangle(662, 53, 3, 3),
         new Rectangle(313, 53, 3, 3),
         new Rectangle(661, 60, 3, 3),
         new Rectangle(201, 43, 3, 3),
         new Rectangle(135, 424, 3, 3),
         new Rectangle(557, 57, 3, 3),
         new Rectangle(537, 59, 3, 3),
         new Rectangle(218, 54, 3, 3),
         new Rectangle(230, 424, 3, 3),
         new Rectangle(100, 471, 3, 3),
         new Rectangle(264, 428, 3, 3),
         new Rectangle(359, 471, 3, 3),

         new Rectangle(539, 74, 2, 2),
         new Rectangle(580, 58, 3, 2),
         new Rectangle(64, 424, 3, 3),
         new Rectangle(294, 488, 1, 5),
         new Rectangle(163, 457, 2, 2),
         new Rectangle(290, 426, 2, 3),
         new Rectangle(238, 439, 2, 4),
         new Rectangle(159, 106, 3, 3),
         new Rectangle(71, 422, 3, 2),
         new Rectangle(357, 477, 2, 2),
         new Rectangle(290, 471, 2, 3),
         new Rectangle(29, 455, 2, 2),
         new Rectangle(55, 439, 1, 1),
         new Rectangle(176, 424, 1, 3),
         new Rectangle(149, 53, 3, 3),
         new Rectangle(309, 440, 1, 2),
         new Rectangle(55, 457, 1, 3),
         new Rectangle(162, 425, 1, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
         //new Rectangle(, , 3, 3),
      ];

      static private readonly ImmutableList<int> trainOptimized_507484246_Labels =
      [
         uppercaseA,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,

         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
         //other,
      ];

      static public IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new();

         dataSet = dataSet.AddRegion("Train", new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels));
         dataSet = dataSet.AddRegion("Validation", new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels));
         dataSet = dataSet.AddRegion("Test", new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels));
         dataSet = dataSet.AddRegion("im164", new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels));
         dataSet = dataSet.AddRegion("im10", new(@"assets/mirflickr08/im10.jpg", im10Rectangles, im10Labels));
         dataSet = dataSet.AddRegion("ti31149327_9330", new(@"assets/text-extraction-for-ocr/ti31149327_9330.tif", train_ti31149327_9330_Rectangles, train_ti31149327_9330_Labels));
         dataSet = dataSet.AddRegion("TrainOptimized", new(@"assets/text-extraction-for-ocr/507484246.tif", trainOptimized_507484246_Rectangles, trainOptimized_507484246_Labels));

         yield return new object[] { dataSet };
      }

      private readonly ITestOutputHelper output;

      private readonly ImageFixture fixture;

      public AmaigomaIntegrationTests(ITestOutputHelper output, ImageFixture fixture)
      {
         this.output = output;
         this.fixture = fixture;
      }

      static private AccuracyResult ComputeAccuracy(PakiraTree tree, ImmutableDictionary<int, SampleData> positions, TanukiETL tanukiETL)
      {
         IEnumerable<int> ids = positions.Keys;
         ImmutableHashSet<BinaryTreeLeaf> leaves = [.. tree.Leaves()];
         AccuracyResult accuracyResult = new()
         {
            leavesBefore = leaves
         };

         PakiraTreeWalker pakiraTreeWalker = new(tree, tanukiETL);

         foreach (int id in ids)
         {
            BinaryTreeLeaf binaryTreeLeafResult = pakiraTreeWalker.PredictLeaf(id);
            int label = tanukiETL.TanukiLabelExtractor(id);

            if (binaryTreeLeafResult.labelValue == label)
            {
               if (accuracyResult.truePositives.ContainsKey(binaryTreeLeafResult))
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.SetItem(binaryTreeLeafResult, accuracyResult.truePositives[binaryTreeLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.truePositives = accuracyResult.truePositives.Add(binaryTreeLeafResult, [id]);
               }
            }
            else
            {
               if (accuracyResult.falsePositives.ContainsKey(binaryTreeLeafResult))
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.SetItem(binaryTreeLeafResult, accuracyResult.falsePositives[binaryTreeLeafResult].Add(id));
               }
               else
               {
                  accuracyResult.falsePositives = accuracyResult.falsePositives.Add(binaryTreeLeafResult, [id]);
               }

               leaves = leaves.Remove(binaryTreeLeafResult);
            }
         }

         accuracyResult.leavesAfter = leaves;

         return accuracyResult;
      }

      static ImmutableList<Buffer2D<ulong>> PrepareIntegralImages(ImmutableList<string> dataSetNames, DataSet dataSet, ImageFixture fixture)
      {
         ImmutableSortedDictionary<int, Buffer2D<ulong>> integralImages = ImmutableSortedDictionary<int, Buffer2D<ulong>>.Empty;

         foreach (string regionName in dataSetNames)
         {
            string filename = dataSet.Region(regionName).filename;

            integralImages = integralImages.Add(dataSet.IntegralImageIndex(filename), fixture.integralImages[filename]);
         }

         return integralImages.Values.ToImmutableList();
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      public void UppercaseA_507484246_Baseline(DataSet dataSet)
      {
         // Number of transformers per size: 17->1, 7->1, 5->9, 3->25, 1->289
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];
         ImmutableDictionary<string, int> dataSetAccuracy = ImmutableDictionary.CreateRange(new Dictionary<string, int> {
          {"Train", 38},
          {"Validation", 28},
          {"Test", 32},
          {"im164", 38},
          {"im10", 38},
          {"ti31149327_9330", 30}
         });
         ImmutableList<string> dataSetNames = dataSetAccuracy.Keys.ToImmutableList();

         PakiraDecisionTreeGenerator pakiraGenerator = new(TreeNodeSplit.GetBestSplitBaseline);
         ImmutableDictionary<int, SampleData> trainPositions = dataSet.Position("Train");
         ImmutableDictionary<int, SampleData> im164Positions = dataSet.Position("im164");
         ImmutableDictionary<int, SampleData> im10Positions = dataSet.Position("im10");
         ImmutableList<Buffer2D<ulong>> integralImages = PrepareIntegralImages(dataSetNames, dataSet, fixture);
         ImmutableDictionary<int, SampleData> allPositions = ImmutableDictionary<int, SampleData>.Empty;

         dataSetNames.ForEach(dataSetName => allPositions = allPositions.AddRange(dataSet.Position(dataSetName)));

         AverageWindowFeature dataExtractor = new(allPositions, integralImages);

         dataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL tanukiETL = new(dataExtractor.ConvertAll, dataExtractor.ExtractLabel, dataExtractor.FeaturesCount());

         IEnumerable<int> allTrainIds = trainPositions.Keys.Union(im164Positions.Keys).Union(im10Positions.Keys);
         PakiraDecisionTreeModel pakiraDecisionTreeModel = pakiraGenerator.Generate(new(), allTrainIds, tanukiETL);

         dataSetNames.ForEach(dataSetName =>
         {
            AccuracyResult accuracyResult = ComputeAccuracy(pakiraDecisionTreeModel.Tree, dataSet.Position(dataSetName), tanukiETL);

            PrintConfusionMatrix(accuracyResult, dataSetName);
            PrintLeaveResults(accuracyResult);

            accuracyResult.leavesAfter.Count.ShouldBe(dataSetAccuracy[dataSetName]);
         });

         PrintEnd();
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      public void UppercaseA_507484246_Clustering(DataSet dataSet)
      {
         // UNDONE Add some permanent benchmarks to identify the slow parts of the test
         TreeNodeSplit bestSplitLogic = new();
         // Number of transformers per size: 17->1, 7->1, 5->9, 3->25, 1->289
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];
         ImmutableDictionary<string, int> dataSetAccuracy = ImmutableDictionary.CreateRange(new Dictionary<string, int> {
          {"Train", 38},
          {"Validation", 30},
          {"Test", 33},
          {"im164", 36},
          {"im10", 38},
          {"ti31149327_9330", 30}
         });
         ImmutableList<string> dataSetNames = dataSetAccuracy.Keys.ToImmutableList();
         PakiraDecisionTreeGenerator pakiraGenerator = new(bestSplitLogic.GetBestSplitClustering);
         ImmutableDictionary<int, SampleData> trainPositions = dataSet.Position("Train");
         ImmutableDictionary<int, SampleData> validationPositions = dataSet.Position("Validation");
         ImmutableDictionary<int, SampleData> testPositions = dataSet.Position("Test");
         ImmutableDictionary<int, SampleData> im164Positions = dataSet.Position("im164");
         ImmutableDictionary<int, SampleData> im10Positions = dataSet.Position("im10");
         ImmutableDictionary<int, SampleData> ti31149327_9330Positions = dataSet.Position("ti31149327_9330");
         ImmutableList<Buffer2D<ulong>> integralImages = PrepareIntegralImages(dataSetNames, dataSet, fixture);

         ImmutableDictionary<int, SampleData> allPositions = ImmutableDictionary<int, SampleData>.Empty;

         dataSetNames.ForEach(dataSetName => allPositions = allPositions.AddRange(dataSet.Position(dataSetName)));

         AverageWindowFeature dataExtractor = new(allPositions, integralImages);

         dataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL tanukiETL = new(dataExtractor.ConvertAll, dataExtractor.ExtractLabel, dataExtractor.FeaturesCount());

         // Generate initial model for clustering
         // TODO Find a better way to do the initial clustering so that it is not dependent on the data distribution. Maybe consider all samples to be of a different class and then merge the leaves which have the same original class?
         // TODO Try not to use ALL data for the initial clustering. This will require to assign a new class to train data which were not seen yet. For "false positives" leaves, make sure to assign a different class based on the original class.
         PakiraDecisionTreeModel pakiraDecisionTreeModel = pakiraGenerator.Generate(new(), trainPositions.Keys, tanukiETL);

         ImmutableDictionary<int, ImmutableDictionary<int, int>> leafIdLabelDataId = [];
         ImmutableList<int> allDataSamples = [];
         ImmutableDictionary<int, int> labelCount = [];
         ImmutableDictionary<int, int> leafCount = [];
         ImmutableDictionary<int, int> idLeafId = [];
         ImmutableDictionary<int, int> leafIdETL = [];

         foreach (BinaryTreeLeaf leaf in pakiraDecisionTreeModel.Tree.Leaves())
         {
            ImmutableList<int> dataSamples = pakiraDecisionTreeModel.DataSamples(leaf.id);

            if (labelCount.ContainsKey(leaf.labelValue))
            {
               labelCount = labelCount.SetItem(leaf.labelValue, labelCount[leaf.labelValue] + 1);
            }
            else
            {
               labelCount = labelCount.Add(leaf.labelValue, 1);
            }

            foreach (int dataSample in dataSamples)
            {
               idLeafId = idLeafId.Add(dataSample, leaf.id);
               leafIdETL = leafIdETL.Add(leaf.id, leaf.labelValue);

               if (!leafIdLabelDataId.ContainsKey(leaf.id))
               {
                  leafIdLabelDataId = leafIdLabelDataId.Add(leaf.id, []);
               }

               int label = tanukiETL.TanukiLabelExtractor(dataSample);

               if (!leafIdLabelDataId[leaf.id].ContainsKey(label))
               {
                  leafIdLabelDataId = leafIdLabelDataId.SetItem(leaf.id, leafIdLabelDataId[leaf.id].Add(label, dataSample));
                  allDataSamples = allDataSamples.Add(dataSample);
               }

               if (leafCount.ContainsKey(leaf.id))
               {
                  leafCount = leafCount.SetItem(leaf.id, leafCount[leaf.id] + 1);
               }
               else
               {
                  leafCount = leafCount.Add(leaf.id, 1);
               }
            }
         }

         //ImmutableDictionary<int, double> idWeight = [];

         //foreach (BinaryTreeLeaf leaf in pakiraDecisionTreeModelAllData.Tree.Leaves())
         //{
         //   double leafWeight = 1.0 / labelCount[leaf.labelValue] / leafCount[leaf.id];

         //   ImmutableList<int> dataSamples = pakiraDecisionTreeModelAllData.DataSamples(leaf.id);

         //   foreach (int dataSample in dataSamples)
         //   {
         //      idWeight = idWeight.Add(dataSample, leafWeight);
         //   }
         //}

         //bestSplitLogic = new(idWeight);

         // UNDONE Document the strategy used step-by-step, with the reason for each decision
         // 0- Add more features OR data to see if the accuracy can be improved furthermore
         // 1- Create initial tree with a partial training set. This will allow to use the rest of the training set to evaluate if the accuracy can be improved by adding data.
         // 2- To improve the accuracy, add more train data, add more features or prune weak tree nodes.
         // TODO Add random features which will act as honeypot to identify overfitting
         // TODO Invert the result of each node one after the other. This will help identify nodes that are no better than random. Doesn't seem to work well.
         // If inverting a node's decision does not significantly impact accuracy, it suggests that the node may not be contributing
         // meaningful information and could be a candidate for removal or further scrutiny. This requires to have enough samples per leaf to be statistically relevant.
         //PakiraDecisionTreeGenerator pakiraGenerator2 = new(bestSplitLogic.GetBestSplitClustering2);
         PakiraDecisionTreeGenerator pakiraGenerator2 = new(bestSplitLogic.GetBestSplitClustering3);

         // TODO Les sous-classes seront conserveees dans le pakira generator. Chaque training va assigner de nouvelles sous-classes en fonction de la leaf ou est tombe le sample. De cette facon, pas besoin de weigths en floating-point. On peut facilement ajouter de nouveaux samples a mesure et ils auront leur sous-classe automatiquement. Utiliser quand meme tout le data de train pour avoir toute la plage de distribution, mais ne calcuer lentropie que sur un sample de chaque cluster. Non, tout utiliser tout le temps sinon on depend trop de quel sample on a choisit. A la fin il faudra peut-être eliminer les nodes du haut en faisant des swap de condition.

         //56: 
         //48: 3, 194, 9x9
         //41: 16, 157, 3x3
         //38: 32, 241, 3x3
         //29: 33, 235, 3x3
         //23: 35, 238, 3x3
         //19: 9, 222, 9x9
         //15: 29, 214, 3x3
         //11: 18, 199, 3x3
         //8: 27, 219, 3x3
         //4: 23, 155, 3x3
         //1: 0, 197, 17x17
         //0: 1, 195, 7x7
         // Need to apply the no-weight logic and THEN analyze one false positive leaf

         TanukiETL clusteringTanukiETL = new(tanukiETL.TanukiDataTransformer, id => idLeafId[id], tanukiETL.TanukiFeatureCount);

         pakiraDecisionTreeModel = pakiraGenerator2.Generate(new(), trainPositions.Keys, clusteringTanukiETL);
         pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeafValues(leafIdETL));

         dataSetNames.ForEach(dataSetName =>
         {
            AccuracyResult accuracyResult = ComputeAccuracy(pakiraDecisionTreeModel.Tree, dataSet.Position(dataSetName), tanukiETL);

            PrintConfusionMatrix(accuracyResult, dataSetName);
            PrintLeaveResults(accuracyResult);
         });

         PrintEnd();

         // TODO Use Spectre.Console to print tree structure if possible

         dataSetNames.ForEach(dataSetName =>
         {
            AccuracyResult accuracyResult = ComputeAccuracy(pakiraDecisionTreeModel.Tree, dataSet.Position(dataSetName), tanukiETL);

            PrintConfusionMatrix(accuracyResult, dataSetName);
            PrintLeaveResults(accuracyResult);

            accuracyResult.leavesAfter.Count.ShouldBe(dataSetAccuracy[dataSetName], dataSetName);
         });

         PrintEnd();
      }

      private static void SaveAllImages(ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> falsePositives, ImmutableDictionary<int, SampleData> positions, string imageName, Rgba32 color)
      {
         using Image<Rgba32> image = new(1000, 1000);
         foreach (var kvp in falsePositives)
         {
            foreach (int id in kvp.Value)
            {
               Point position = positions[id].Position;

               image[position.X, position.Y] = color;
            }

            SaveImage(kvp.Value, positions, imageName + $"_{kvp.Key.id}_{kvp.Key.labelValue}_{kvp.Value.Count}" + $"" + ".png", color);
         }
      }

      private static void SaveImage(ImmutableList<int> ids, ImmutableDictionary<int, SampleData> positions, string imageName, Rgba32 color)
      {
         using Image<Rgba32> image = new(1000, 1000);
         foreach (int id in ids)
         {
            Point position = positions[id].Position;

            image[position.X, position.Y] = color;
         }

         image.SaveAsPng(Path.GetTempPath() + imageName);
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
