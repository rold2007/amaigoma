global using BinaryTreeLeaf = (int id, int labelValue);

using Amaigoma;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
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

   public record IntegrationTestDataSet
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
      private static readonly ImmutableList<int> emptyHistogram8 = [.. Enumerable.Repeat(0, 8)];
      private static readonly ImmutableList<int> emptyHistogram254 = [.. Enumerable.Repeat(0, 255)];

      public TreeNodeSplit()
      {
      }

      private static double CalculateEntropy(IEnumerable<int> histogram)
      {
         int total = histogram.Sum();
         double entropy = 0.0;

         total.ShouldNotBe(0);

         foreach (int count in histogram)
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

      private static double CalculateNormalizedEntropy(ReadOnlySpan<int> histogram)
      {
         int total = 0;

         foreach (int count in histogram)
         {
            total += count;
         }

         total.ShouldNotBe(0);

         double entropy = 0.0;

         foreach (int count in histogram)
         {
            if (count > 0)
            {
               double p = (double)count / total;
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
                     double weightedEntropy = leftTotalCount * leftEntropy + rightTotalCount * rightEntropy;

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
                     bestSplitValue += bestSplitCount / 2;
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

      private double ComputeInflexionPoint(ImmutableList<double> values)
      {
         ImmutableList<double> sortedValues = values.Sort();
         ImmutableList<double> validValues = ImmutableList<double>.Empty.Add(sortedValues[0]);

         for (int i = 1; i < sortedValues.Count; i++)
         {
            // UNDONE This 0.01 constant could be parametrized
            if (sortedValues[i] > (sortedValues[i - 1] + 0.01))
            {
               validValues = validValues.Add(sortedValues[i]);
            }
            else
            {
               break;
            }
         }

         (double a, double b) = Fit.Logarithm(Enumerable.Range(1, sortedValues.Count).Select((x) => (double)x).ToArray(), sortedValues.ToArray());

         ImmutableList<double> localDerivative = ImmutableList<double>.Empty;

         for (int i = 0; i < validValues.Count; i++)
         {
            double x = i;
            double derivative = validValues.Count * b / (i + 1);

            localDerivative = localDerivative.Add(derivative);
         }

         return 0;
      }

      private double ComputeInflexionPoint2(ImmutableList<double> values)
      {
         // UNDONE Remove the Sort, it is only for debugging purpose
         ImmutableList<double> sortedValues = values.Sort();

         double minimumValue = StreamingStatistics.Minimum(values);
         double maximumValue = StreamingStatistics.Maximum(values);

         return minimumValue + 0.8 * (maximumValue - minimumValue);
      }

      public (int featureIndex, double splitThreshold) GetBestSplitClusteringMean(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         //const int sampleSize = 1000;
         int sampleSize = 1000;
         //const int sampleSize = 10000;
         //sampleSize = 5000;
         sampleSize = 10000;
         //sampleSize = 50000;
         sampleSize = 1000;

         double averageHistogram = sampleSize / 256;

         // UNDONE Replace 9999 by datadistribution
         ImmutableList<int> dataDistributionIds = [.. ids.Where(id => tanukiETL.TanukiLabelExtractor(id) == 9999).Take(sampleSize)];
         ImmutableList<int> trainingIds = [.. ids.Where(id => tanukiETL.TanukiLabelExtractor(id) != 9999)];

         if (dataDistributionIds.Count == sampleSize)
         {
            ImmutableList<double> weigthedEntropies = [];
            ImmutableList<double> normalizedEntropies = [];
            ImmutableList<double> splitValues = [];
            ImmutableList<double> shannonEntropies = [];

            ImmutableList<double> localShannonEntropies = [];
            ImmutableList<double> localWeightedEntropies = [];
            ImmutableList<int> localCumulativeSum = [];
            ImmutableList<int> localFeatureIndex = [];
            ImmutableList<int> localSplitValue = [];

            // UNDONE Idea: take 10% best Shannon, then best cumulative sum percentage then take the best Shannon with at least 50% max of cumulative

            ImmutableDictionary<int, int> trainingLabelCounts = [.. trainingIds
               .GroupBy(id => tanukiETL.TanukiLabelExtractor(id))
               .ToImmutableDictionary(group => group.Key, group => group.Count())];
            ImmutableDictionary<int, double> classWeights = [.. trainingLabelCounts
               .Select(labelCount => new KeyValuePair<int, double>(
                  labelCount.Key,
                  trainingIds.Count / (double)(trainingLabelCounts.Count * labelCount.Value)))];
            ImmutableList<ImmutableList<int>> histograms = [];
            Span<int> counts = stackalloc int[256];

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               counts.Clear();

               foreach (int id in dataDistributionIds)
               {
                  counts[tanukiETL.TanukiDataTransformer(id, featureIndex)]++;
               }

               normalizedEntropies = normalizedEntropies.Add(CalculateNormalizedEntropy(counts));
               histograms = histograms.Add([.. counts]);
            }

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               ImmutableList<int> histogram = histograms[featureIndex];

               // Skip uniform histograms
               //if (normalizedEntropy < 0.95)
               {
                  ImmutableList<double> localSum = ImmutableList<double>.Empty;
                  ImmutableList<double> localSplitPosition = ImmutableList<double>.Empty;

                  // UNDONE If this logic works, it should be unit tested with synthetic histograms. Don't forget to add histogram shape which would happen after one or more splits.
                  // UNDONE Still need to manage the best position in a bimodal histogram

                  int cumulativeSum = histogram[0] + histogram[1];

                  for (int i = 2; i < 253; i++)
                  {
                     cumulativeSum += histogram[i];

                     localCumulativeSum = localCumulativeSum.Add(Math.Min(cumulativeSum, sampleSize - cumulativeSum));
                     localFeatureIndex = localFeatureIndex.Add(featureIndex);
                     localSplitValue = localSplitValue.Add(i);

                     // UNDONE A good value should be 0.25 and 0.75, but for this the algorithm needs better features to handle difficult cases.
                     //if (cumulativeSum >= 0.05 * sampleSize && cumulativeSum < 0.95 * sampleSize)
                     //if (cumulativeSum >= 0.25 * sampleSize && cumulativeSum < 0.75 * sampleSize)
                     {
                        int previousSum = histogram[i - 2] + histogram[i - 1];
                        int currentSum = histogram[i] + histogram[i + 1];

                        localWeightedEntropies = localWeightedEntropies.Add(currentSum);

                        if (currentSum < previousSum)
                        {
                           int nextSum = histogram[i + 2] + histogram[i + 3];

                           if (currentSum < nextSum)
                           {
                              localSum = localSum.Add(currentSum);
                              localSplitPosition = localSplitPosition.Add(i);

                              if (currentSum < bestWeightedEntropy)
                              {
                                 bestWeightedEntropy = currentSum;
                                 bestSplitValue = i;
                              }
                           }
                        }
                     }
                     //else
                     //{
                     //   localWeightedEntropies = localWeightedEntropies.Add(500);
                     //}

                     {
                        ImmutableDictionary<int, int>.Builder leftTrainingLabelCounts = ImmutableDictionary.CreateBuilder<int, int>();
                        ImmutableDictionary<int, int>.Builder rightTrainingLabelCounts = ImmutableDictionary.CreateBuilder<int, int>();

                        foreach (int id in trainingIds)
                        {
                           int label = tanukiETL.TanukiLabelExtractor(id);
                           int transformedValue = tanukiETL.TanukiDataTransformer(id, featureIndex);
                           ImmutableDictionary<int, int>.Builder labelCounts = transformedValue <= i
                              ? leftTrainingLabelCounts
                              : rightTrainingLabelCounts;

                           labelCounts[label] = labelCounts.GetValueOrDefault(label) + 1;
                        }

                        localShannonEntropies = localShannonEntropies.Add(CalculateWeightedSplitEntropy(leftTrainingLabelCounts, rightTrainingLabelCounts, classWeights));
                     }
                  }

                  weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);
                  splitValues = splitValues.Add(bestSplitValue);

                  // UNDONE Add a threshold at which we accept the feature directly when it is good enough
                  // UNDONE Add a threshold at which we're kind-of statisfied with the result, so we can search for X more features and then keep the best so far.
                  if ((bestFeature == -1 && bestWeightedEntropy != double.MaxValue) ||
                     (bestFeature != -1 && weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature]))
                  {
                     bestFeature = featureIndex;
                     bestFeatureSplit = bestSplitValue;
                  }

                  if (bestSplitValue >= 0)
                  {
                     ImmutableDictionary<int, int>.Builder leftTrainingLabelCounts = ImmutableDictionary.CreateBuilder<int, int>();
                     ImmutableDictionary<int, int>.Builder rightTrainingLabelCounts = ImmutableDictionary.CreateBuilder<int, int>();

                     foreach (int id in trainingIds)
                     {
                        int label = tanukiETL.TanukiLabelExtractor(id);
                        int transformedValue = tanukiETL.TanukiDataTransformer(id, featureIndex);
                        ImmutableDictionary<int, int>.Builder labelCounts = transformedValue <= bestSplitValue
                           ? leftTrainingLabelCounts
                           : rightTrainingLabelCounts;

                        labelCounts[label] = labelCounts.GetValueOrDefault(label) + 1;
                     }

                     shannonEntropies = shannonEntropies.Add(CalculateWeightedSplitEntropy(leftTrainingLabelCounts, rightTrainingLabelCounts, classWeights));
                  }
                  else
                  {
                     shannonEntropies = shannonEntropies.Add(double.MaxValue);
                  }
               }
               //else
               //{
               //   weigthedEntropies = weigthedEntropies.Add(double.MaxValue);
               //   shannonEntropies = shannonEntropies.Add(double.MaxValue);
               //}
            }

            double bestWeight = weigthedEntropies.Min();
            double bestShannonEntropy = double.MaxValue;
            double weightTreshold = bestWeight * 1.33;

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               if (weigthedEntropies[featureIndex] <= weightTreshold)
               {
                  if (shannonEntropies[featureIndex] < bestShannonEntropy)
                  {
                     bestFeature = featureIndex;
                     bestFeatureSplit = splitValues[featureIndex];
                     bestShannonEntropy = shannonEntropies[featureIndex];
                  }
               }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            double inflexionPoint;

            inflexionPoint = ComputeInflexionPoint(localShannonEntropies);
            inflexionPoint = ComputeInflexionPoint(normalizedEntropies);

            inflexionPoint = ComputeInflexionPoint2(localShannonEntropies);
            inflexionPoint = ComputeInflexionPoint2(normalizedEntropies);

            ImmutableList<double> localShannonEntropiesSorted = localShannonEntropies.Sort().ToImmutableList();
            ImmutableList<double> temp = [];

            for (int i = 100; i < 2500; i++)
            {
               temp = temp.Add(localShannonEntropiesSorted[i]);
            }

            //cubicSpline = CubicSpline.(Enumerable.Range(0, localShannonEntropies.Count).Select((x) => (double)x).ToArray(), localShannonEntropies.Sort().ToArray());
            ImmutableList<double> localShannonEntropiesDerivatives = [];


            (double a, double b) = Fit.Logarithm(Enumerable.Range(1, temp.Count).Select((x) => (double)x).ToArray(), temp.Sort().ToArray());

            // Pour évaluer la dérivée analytique exacte au point X de ce polynôme :
            // f'(x) = b + 2cx + 3dx²
            for (int i = 0; i < temp.Count; i++)
            {
               double x = i;
               double derivative = b / (i + 1);

               localShannonEntropiesDerivatives = localShannonEntropiesDerivatives.Add(derivative);
            }

            temp = localShannonEntropiesSorted;
            temp = normalizedEntropies;

            //for (int i = 0; i < localShannonEntropies.Count; i++)
            //{
            //   localShannonEntropiesDerivatives = localShannonEntropiesDerivatives.Add(cubicSpline.Differentiate(i));
            //}


            //cubicSpline = CubicSpline.InterpolateNatural(Enumerable.Range(0, normalizedEntropies.Count).Select((x) => (double)x), normalizedEntropies);
            //ImmutableList<double> normalizedEntropiesDerivatives = [];

            //for (int i = 0; i < normalizedEntropies.Count; i++)
            //{
            //   normalizedEntropiesDerivatives = normalizedEntropiesDerivatives.Add(cubicSpline.Differentiate(i));
            //}
         }

         return (bestFeature, bestFeatureSplit);
      }

      private static double CalculateWeightedSplitEntropy(
         IReadOnlyDictionary<int, int> leftLabelCounts,
         IReadOnlyDictionary<int, int> rightLabelCounts,
         IReadOnlyDictionary<int, double> classWeights)
      {
         double leftTotal = leftLabelCounts.Sum(labelCount => labelCount.Value * classWeights[labelCount.Key]);
         double rightTotal = rightLabelCounts.Sum(labelCount => labelCount.Value * classWeights[labelCount.Key]);
         double total = leftTotal + rightTotal;

         if (total == 0) return 0;

         double leftEntropy = CalculateWeightedEntropy(leftLabelCounts, classWeights, leftTotal);
         double rightEntropy = CalculateWeightedEntropy(rightLabelCounts, classWeights, rightTotal);

         return (leftTotal / total) * leftEntropy + (rightTotal / total) * rightEntropy;
      }

      private static double CalculateWeightedEntropy(
         IReadOnlyDictionary<int, int> labelCounts,
         IReadOnlyDictionary<int, double> classWeights,
         double total)
      {
         if (total == 0) return 0;

         double entropy = 0;

         foreach ((int label, int count) in labelCounts)
         {
            if (count == 0) continue;

            double weightedCount = count * classWeights[label];
            double probability = weightedCount / total;
            entropy -= probability * Math.Log2(probability);
         }

         return entropy;
      }
   }

   public record ImageFixture : IDisposable
   {
      ImmutableList<string> imagePaths = [
       @"assets/text-extraction-for-ocr/507484246.tif",
       @"assets/mirflickr08/im164.jpg",
       @"assets/mirflickr08/im10.jpg",
       @"assets/text-extraction-for-ocr/ti31149327_9330.tif",
       @"assets/PineTools/image.png"];

      public ImmutableDictionary<string, Image<L8>> sourceImages = [];
      public ImmutableDictionary<string, Image<L8>> edgeImages = [];
      public ImmutableDictionary<string, Buffer2D<ulong>> integralImages = [];
      public ImmutableDictionary<string, Buffer2D<ulong>> edgeIntegralImages = [];

      public ImageFixture()
      {
         sourceImages = imagePaths.AsParallel().Select(image =>
         {
            string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), image);
            Image<L8> sourceImage = Image.Load<L8>(fullImagePath);
            return (image, sourceImage);
         }).ToImmutableDictionary(t => t.image, t => t.sourceImage);

         edgeImages = sourceImages.AsParallel().Select(kv =>
         {
            Image<L8> sourceImage = kv.Value.Clone(context => context.DetectEdges(EdgeDetectorKernel.Laplacian3x3));
            return (kv.Key, sourceImage);
         }).ToImmutableDictionary(t => t.Key, t => t.sourceImage);

         integralImages = sourceImages.AsParallel().Select(kv =>
         {
            Buffer2D<ulong> integralImage = kv.Value.CalculateIntegralImage();
            return (kv.Key, integralImage);
         }).ToImmutableDictionary(t => t.Key, t => t.integralImage);

         edgeIntegralImages = edgeImages.AsParallel().Select(kv =>
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
   public record AmaigomaIntegrationTests : IClassFixture<ImageFixture>
   {
      // TODO Add more classes
      static readonly int uppercaseA = 1;
      static readonly int other = 2;
      static readonly int datadistribution = 9999;

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
         // new Rectangle(20, 555, 480, 215),
      ];

      static private readonly ImmutableList<int> validation_507484246_Labels =
         [
         uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA,
         // other, other
         other
         ];

      static private readonly ImmutableList<Rectangle> test_507484246_Rectangles =
      [
         new Rectangle(218, 790, 1, 1),
         new Rectangle(411, 836, 1, 1),
         new Rectangle(137, 851, 1, 1),
         new Rectangle(257, 851, 1, 1),
         new Rectangle(605, 851, 1, 1),
         // new Rectangle(520, 550, 230, 216),
          new Rectangle(95, 810, 500, 20),
         // new Rectangle(20, 900, 740, 70),
         // new Rectangle(180, 960, 310, 23),
      ];

      static private readonly ImmutableList<int> test_507484246_Labels =
      [
         uppercaseA, uppercaseA, uppercaseA, uppercaseA, uppercaseA,
         // other, other, other, other
         other
      ];

      static private readonly ImmutableList<Rectangle> im164Rectangles = [new Rectangle(8, 8, 483, 358)];
      static private readonly ImmutableList<int> im164Labels = [datadistribution];
      static private readonly ImmutableList<Rectangle> im10Rectangles = [new Rectangle(8, 8, 483, 316)];
      static private readonly ImmutableList<int> im10Labels = [other];
      static private readonly ImmutableList<Rectangle> imageRectangles = [new Rectangle(8, 8, 483, 358)];
      static private readonly ImmutableList<int> imageLabels = [datadistribution];

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

         // TODO This could be parallelized to save some time
         dataSet = dataSet.AddRegion("Train", new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels));
         dataSet = dataSet.AddRegion("Validation", new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels));
         dataSet = dataSet.AddRegion("Test", new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels));
         dataSet = dataSet.AddRegion("im164", new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels));
         dataSet = dataSet.AddRegion("im10", new(@"assets/mirflickr08/im10.jpg", im10Rectangles, im10Labels));
         dataSet = dataSet.AddRegion("ti31149327_9330", new(@"assets/text-extraction-for-ocr/ti31149327_9330.tif", train_ti31149327_9330_Rectangles, train_ti31149327_9330_Labels));
         dataSet = dataSet.AddRegion("image", new(@"assets/PineTools/image.png", imageRectangles, imageLabels));

         yield return new object[] { dataSet };
      }

      private readonly ITestOutputHelper output;

      private readonly ImageFixture fixture;

      public AmaigomaIntegrationTests(ITestOutputHelper output, ImageFixture fixture)
      {
         this.output = output;
         this.fixture = fixture;
      }

      static ImmutableList<ImmutableDictionary<int, ImmutableList<int>>> ComputeModelClustering(ImmutableList<PakiraDecisionTreeModel> models, IEnumerable<int> allTrainIds, TanukiETL tanukiETL)
      {
         ImmutableList<ImmutableDictionary<int, ImmutableList<int>>> modelsTrainLeafMaps = Enumerable.Range(0, models.Count)
         .Select(i =>
         {
            PakiraDecisionTreeModel model = models[i];
            PakiraTreeWalker walker = new(model.Tree, tanukiETL);

            ImmutableDictionary<int, ImmutableList<int>> leafToTrainIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            foreach (int trainId in allTrainIds)
            {
               BinaryTreeLeaf leaf = walker.PredictLeaf(trainId);

               if (!leafToTrainIds.ContainsKey(leaf.id))
               {
                  leafToTrainIds = leafToTrainIds.Add(leaf.id, ImmutableList<int>.Empty);
               }

               leafToTrainIds = leafToTrainIds.SetItem(leaf.id, leafToTrainIds[leaf.id].Add(trainId));
            }

            return leafToTrainIds;
         }).ToImmutableList();

         return modelsTrainLeafMaps;
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

      static ImmutableList<Buffer2D<ulong>> PrepareIntegralImages(ImmutableList<string> dataSetNames, DataSet dataSet, ImmutableDictionary<string, Buffer2D<ulong>> sourceIntegralImages)
      {
         ImmutableSortedDictionary<int, Buffer2D<ulong>> integralImages = ImmutableSortedDictionary<int, Buffer2D<ulong>>.Empty;

         foreach (string regionName in dataSetNames)
         {
            string filename = dataSet.Region(regionName).filename;

            integralImages = integralImages.Add(dataSet.IntegralImageIndex(filename), sourceIntegralImages[filename]);
         }

         return integralImages.Values.ToImmutableList();
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      [Trait("Category", "Integration")]
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
         ImmutableList<Buffer2D<ulong>> integralImages = PrepareIntegralImages(dataSetNames, dataSet, fixture.integralImages);
         ImmutableDictionary<int, SampleData> allPositions = ImmutableDictionary<int, SampleData>.Empty;

         dataSetNames.ForEach(dataSetName => allPositions = allPositions.AddRange(dataSet.Position(dataSetName)));

         AverageWindowFeature dataExtractor = new(allPositions, integralImages);

         dataExtractor.AddAverageTransformer(averageTransformerSizes);

         TanukiETL tanukiETL = new(dataExtractor.ConvertAll, id => allPositions[id].Label, dataExtractor.FeaturesCount());

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
      [Trait("Category", "Integration")]
      public void UppercaseA_507484246_Clustering(DataSet dataSet)
      {
         // UNDONE Add some permanent benchmarks to identify the slow parts of the test
         TreeNodeSplit bestSplitLogic = new();
         // Number of transformers per size: 17->1, 7->1, 5->9, 3->25, 1->289
         // ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
         // ImmutableList<int> averageTransformerSizes = [7, 5, 3];
         //ImmutableList<int> averageTransformerSizes = [5, 3];
         ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
         //ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3];
         // ImmutableList<int> averageTransformerSizes = [17, 1];
         ImmutableDictionary<string, int> dataSetAccuracy = ImmutableDictionary.CreateRange(new Dictionary<string, int> {
          {"Train", 37},
          {"Validation", 30},
          {"Test", 31},
          {"im164", 35},
          {"im10", 37},
          {"ti31149327_9330", 30},
          {"image", 37}
         });
         ImmutableList<string> dataSetNames = dataSetAccuracy.Keys.ToImmutableList();
         ImmutableDictionary<int, SampleData> trainPositions = dataSet.Position("Train");
         ImmutableDictionary<int, SampleData> validationPositions = dataSet.Position("Validation");
         ImmutableDictionary<int, SampleData> testPositions = dataSet.Position("Test");
         ImmutableDictionary<int, SampleData> im164Positions = dataSet.Position("im164");
         ImmutableDictionary<int, SampleData> im10Positions = dataSet.Position("im10");
         ImmutableDictionary<int, SampleData> ti31149327_9330Positions = dataSet.Position("ti31149327_9330");
         ImmutableDictionary<int, SampleData> imagePositions = dataSet.Position("image");
         ImmutableList<Buffer2D<ulong>> integralImages = PrepareIntegralImages(dataSetNames, dataSet, fixture.integralImages);
         ImmutableList<Buffer2D<ulong>> edgeIntegralImages = PrepareIntegralImages(dataSetNames, dataSet, fixture.edgeIntegralImages);

         //   @"assets/text-extraction-for-ocr/507484246.tif",
         //@"assets/mirflickr08/im164.jpg",
         //@"assets/mirflickr08/im10.jpg",
         //@"assets/text-extraction-for-ocr/ti31149327_9330.tif"];

         //Image<L8> image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges());
         //image.SaveAsPng("sobel.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetectorCompassKernel.Kirsch));
         //image.SaveAsPng("Kirsch.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetectorCompassKernel.Robinson));
         //image.SaveAsPng("Robinson.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.KayyaliKernel));
         //image.SaveAsPng("KayyaliKernel.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.PrewittKernel));
         //image.SaveAsPng("PrewittKernel.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.RobertsCrossKernel));
         //image.SaveAsPng("RobertsCrossKernel.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetector2DKernel.ScharrKernel));
         //image.SaveAsPng("ScharrKernel.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetectorKernel.LaplacianOfGaussian));
         //image.SaveAsPng("LaplacianOfGaussian.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetectorKernel.Laplacian3x3));
         //image.SaveAsPng("Laplacian3x3.png");

         //image = fixture.sourceImages["assets/text-extraction-for-ocr/507484246.tif"].Clone(context => context.DetectEdges(SixLabors.ImageSharp.Processing.Processors.Convolution.EdgeDetectorKernel.Laplacian5x5));
         //image.SaveAsPng("Laplacian5x5.png");

         ImmutableDictionary<int, SampleData> allPositions = ImmutableDictionary<int, SampleData>.Empty;

         dataSetNames.ForEach(dataSetName => allPositions = allPositions.AddRange(dataSet.Position(dataSetName)));

         AverageWindowFeature dataExtractor = new(allPositions, integralImages);
         AverageWindowFeature edgeDataExtractor = new(allPositions, edgeIntegralImages);

         dataExtractor.AddAverageTransformer(averageTransformerSizes, 17);
         edgeDataExtractor.AddAverageTransformer(averageTransformerSizes, 17);

         TanukiETL tanukiETL = new(dataExtractor.ConvertAll, (id => allPositions[id].Label), dataExtractor.FeaturesCount());

         //tanukiETL = tanukiETL.AddDataTransformer(edgeDataExtractor.ConvertAll, edgeDataExtractor.FeaturesCount());


         PakiraDecisionTreeGenerator pakiraGeneratorClusteringHybrid = new(bestSplitLogic.GetBestSplitClusteringMean);

         //ImmutableList<int> abc = im164Positions.Keys.Union(trainPositions.Keys).ToImmutableList();
         //ImmutableList<int> trainingData = imagePositions.Keys.Union(trainPositions.Keys).ToImmutableList();
         IEnumerable<int> limitedTrainPositions = trainPositions.Keys.Take(300);
         const int modelCount = 50;
         limitedTrainPositions = trainPositions.Keys.Take(109);
         limitedTrainPositions = [2, 108];
         limitedTrainPositions = trainPositions.Keys.Take(216);

         PakiraDecisionTreeModel entropyTest;

         //entropyTest = pakiraGeneratorClusteringHybrid.Generate(new(), imagePositions.Keys.Shuffle(new Random(42)).Take(16000), tanukiETL);
         //entropyTest = pakiraGeneratorClusteringHybrid.Generate(new(), imagePositions.Keys.Shuffle(new Random(42)).Take(16000).Union(trainPositions.Keys), tanukiETL);
         //entropyTest = pakiraGeneratorClusteringHybrid.Generate(new(), imagePositions.Keys.Shuffle(new Random(42)).Take(16000).Union(limitedTrainPositions), tanukiETL);

         //entropyTest = pakiraGeneratorClusteringHybrid.Generate(new(), imagePositions.Keys.Shuffle(new Random(42)).Take(2).Union(limitedTrainPositions), tanukiETL);

         entropyTest = pakiraGeneratorClusteringHybrid.Generate(new(), imagePositions.Keys.Shuffle(new Random(42)).Take(16000).Union(limitedTrainPositions), tanukiETL);

         ImmutableList<PakiraDecisionTreeModel> models = Enumerable.Range(0, modelCount).AsParallel().Select(i =>
         {
            //IEnumerable<int> trainingData = imagePositions.Keys.Shuffle(new Random(54 + i)).Take(16000).Union(limitedTrainPositions);
            //IEnumerable<int> trainingData = imagePositions.Keys.Shuffle(new Random(54 + i)).Take(16000).Union(trainPositions.Keys.Take(200));
            IEnumerable<int> trainingData = imagePositions.Keys.Shuffle(new Random(54 + i)).Take(64000).Union(trainPositions.Keys.Take(200));

            return pakiraGeneratorClusteringHybrid.Generate(new(), trainingData, tanukiETL);
         }).ToImmutableList();

         //ImmutableList<string> testNames = ["Train", "Test", "Validation"];
         // ImmutableList<string> results = ImmutableList<string>.Empty;

         // testNames.ForEach(dataSetName =>
         // {
         //    IEnumerable<int> ids = dataSet.Position(dataSetName).Keys;
         //    File.AppendAllText("results.txt", dataSetName + Environment.NewLine);
         //    results = results.Add(dataSetName);

         //    foreach (int id in ids)
         //    {
         //       string result = id.ToString() + "," + dataSet.Position(dataSetName)[id].Label + ",";

         //       foreach (PakiraDecisionTreeModel model in models)
         //       {
         //          PakiraTreeWalker pakiraTreeWalker = new(model.Tree, tanukiETL);
         //          BinaryTreeLeaf binaryTreeLeafResult = pakiraTreeWalker.PredictLeaf(id);

         //          result += binaryTreeLeafResult.id.ToString() + ",";
         //       }

         //       File.AppendAllText("results.txt", result + Environment.NewLine);
         //       results = results.Add(result);
         //    }
         // });

         // TODO Use a random seed
         //IEnumerable<int> shuffledTrainData = im164Positions.Keys.Shuffle(new Random(42));

         // PakiraDecisionTreeModel pakiraDecisionTreeModelClusteringHybrid = pakiraGeneratorClusteringHybrid.Generate(new(), shuffledTrainData, tanukiETL);
         //PakiraDecisionTreeModel pakiraDecisionTreeModelClusteringHybrid = pakiraGeneratorClusteringHybrid.Generate(new(), trainPositions.Keys, tanukiETL);

         // Compute leaf ID of each tree for all Train positions and build mapping
         ImmutableList<ImmutableDictionary<int, ImmutableList<int>>> modelsTrainLeafMaps = ComputeModelClustering(models, limitedTrainPositions, tanukiETL);
         modelsTrainLeafMaps = ComputeModelClustering(models, trainPositions.Keys, tanukiETL);
         modelsTrainLeafMaps = ComputeModelClustering(models, trainPositions.Keys.Take(5000), tanukiETL);

         // UNDONE Add logic to select the best model trees used for majority vote evaluation.

         // For each Test position, collect votes from each model about which Train samples fall in the same leaf
         File.AppendAllText("results.txt", "MajorityVoteSimilarity\n");

         Dictionary<int, int> overallHighestVoteCountByLabel = new();

         //ImmutableList<int> activeModels = Enumerable.Range(0, 8).ToImmutableList();
         //ImmutableList<int> inactiveModels = Enumerable.Range(9, models.Count - 8).ToImmutableList();
         ImmutableList<int> activeModels = Enumerable.Range(0, modelCount).ToImmutableList();
         ////ImmutableList<int> inactiveModels = Enumerable.Range(9, models.Count - 8).ToImmutableList();

         ImmutableList<int> allTrueValues = ImmutableList<int>.Empty;
         ImmutableList<int> allFalseValues = ImmutableList<int>.Empty;

         // while (true)
         {
            //foreach (int testId in limitedTrainPositions)
            foreach (int testId in trainPositions.Keys.Take(5000))
            //foreach (int testId in validationPositions.Keys)
            {
               Dictionary<int, int> voteCounts = new(); // trainId -> votes

               foreach (int m in activeModels)
               {
                  PakiraDecisionTreeModel model = models[m];
                  PakiraTreeWalker walker = new(model.Tree, tanukiETL);
                  BinaryTreeLeaf testLeaf = walker.PredictLeaf(testId);

                  if (modelsTrainLeafMaps[m].ContainsKey(testLeaf.id))
                  {
                     foreach (int trainId in modelsTrainLeafMaps[m][testLeaf.id])
                     {
                        if (voteCounts.ContainsKey(trainId))
                        {
                           voteCounts[trainId]++;
                        }
                        else
                        {
                           voteCounts[trainId] = 1;
                        }
                     }
                  }
               }

               ImmutableDictionary<bool, int> maxVotePrediction = ImmutableDictionary<bool, int>.Empty.Add(true, 0).Add(false, 0);
               int testLabel = tanukiETL.TanukiLabelExtractor(testId);

               foreach (KeyValuePair<int, int> vote in voteCounts)
               {
                  int label = tanukiETL.TanukiLabelExtractor(vote.Key);

                  if (label == testLabel)
                  {
                     maxVotePrediction = maxVotePrediction.SetItem(true, Math.Max(maxVotePrediction[true], vote.Value));
                  }
                  else
                  {
                     maxVotePrediction = maxVotePrediction.SetItem(false, Math.Max(maxVotePrediction[false], vote.Value));
                  }
               }

               allTrueValues = allTrueValues.Add(maxVotePrediction[true]);
               allFalseValues = allFalseValues.Add(maxVotePrediction[false]);

               // pick the strongest train sample for each label
               //int maxVotes = voteCounts.Values.DefaultIfEmpty(0).Max();
               //var winners = voteCounts
               //   .GroupBy(kv => trainPositions[kv.Key].Label)
               //   .Select(grouping => grouping.OrderByDescending(kv => kv.Value).First().Key)
               //   .ToImmutableList();
               //var winnerLabels = winners.Select(winnerId => trainPositions[winnerId].Label).ToImmutableList();

               //var highestVoteCountByLabel = voteCounts
               //   .GroupBy(kv => trainPositions[kv.Key].Label)
               //   .ToImmutableDictionary(
               //      grouping => grouping.Key,
               //      grouping => grouping.Max(kv => kv.Value));

               //foreach (var labelVote in highestVoteCountByLabel)
               //{
               //   if (!overallHighestVoteCountByLabel.ContainsKey(labelVote.Key) || overallHighestVoteCountByLabel[labelVote.Key] < labelVote.Value)
               //   {
               //      overallHighestVoteCountByLabel[labelVote.Key] = labelVote.Value;
               //   }
               //}

               //string labelMaxVotes = string.Join(';', highestVoteCountByLabel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
               //string line = testId.ToString() + "," + validationPositions[testId].Label + ",Votes=" + maxVotes + ",Winners=" + string.Join(';', winners) + ",WinnerLabels=" + string.Join(';', winnerLabels) + ",LabelMaxVotes=" + labelMaxVotes;
               //File.AppendAllText("results.txt", line + Environment.NewLine);
            }
         }

         foreach (int testId in testPositions.Keys)
         {
            Dictionary<int, int> voteCounts = new(); // trainId -> votes

            for (int m = 0; m < models.Count; m++)
            {
               PakiraDecisionTreeModel model = models[m];
               PakiraTreeWalker walker = new(model.Tree, tanukiETL);
               BinaryTreeLeaf testLeaf = walker.PredictLeaf(testId);

               if (modelsTrainLeafMaps[m].ContainsKey(testLeaf.id))
               {
                  foreach (int trainId in modelsTrainLeafMaps[m][testLeaf.id])
                  {
                     if (voteCounts.ContainsKey(trainId)) voteCounts[trainId]++; else voteCounts[trainId] = 1;
                  }
               }
            }

            // pick the strongest train sample for each label
            int maxVotes = voteCounts.Values.DefaultIfEmpty(0).Max();
            var winners = voteCounts
               .GroupBy(kv => trainPositions[kv.Key].Label)
               .Select(grouping => grouping.OrderByDescending(kv => kv.Value).First().Key)
               .ToImmutableList();
            var winnerLabels = winners.Select(winnerId => trainPositions[winnerId].Label).ToImmutableList();

            var highestVoteCountByLabel = voteCounts
               .GroupBy(kv => trainPositions[kv.Key].Label)
               .ToImmutableDictionary(
                  grouping => grouping.Key,
                  grouping => grouping.Max(kv => kv.Value));

            foreach (var labelVote in highestVoteCountByLabel)
            {
               if (!overallHighestVoteCountByLabel.ContainsKey(labelVote.Key) || overallHighestVoteCountByLabel[labelVote.Key] < labelVote.Value)
               {
                  overallHighestVoteCountByLabel[labelVote.Key] = labelVote.Value;
               }
            }

            string labelMaxVotes = string.Join(';', highestVoteCountByLabel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"));
            string line = testId.ToString() + "," + testPositions[testId].Label + ",Votes=" + maxVotes + ",Winners=" + string.Join(';', winners) + ",WinnerLabels=" + string.Join(';', winnerLabels) + ",LabelMaxVotes=" + labelMaxVotes;
            File.AppendAllText("results.txt", line + Environment.NewLine);
         }

         File.AppendAllText("results.txt", "OverallLabelMaxVotes=" + string.Join(';', overallHighestVoteCountByLabel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")) + Environment.NewLine);

         // AccuracyResult accuracyResult1 = ComputeAccuracy(pakiraDecisionTreeModelClusteringHybrid.Tree, dataSet.Position("Train"), tanukiETL);
         // AccuracyResult accuracyResult2 = ComputeAccuracy(pakiraDecisionTreeModelClusteringHybrid.Tree, dataSet.Position("Test"), tanukiETL);

         // dataSetNames.ForEach(dataSetName =>
         // {
         //    AccuracyResult accuracyResult = ComputeAccuracy(pakiraDecisionTreeModelClusteringHybrid.Tree, dataSet.Position(dataSetName), tanukiETL);

         //    PrintConfusionMatrix(accuracyResult, dataSetName);
         //    PrintLeaveResults(accuracyResult);
         // });

         // UNDONE Next task: Create a random forest of about 200 clustering trees and evaluate the accuracy based on majority vote.

         PrintEnd();

         /*
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

                  //TanukiETL clusteringTanukiETL = new(dataExtractor.ConvertAll, id => idLeafId[id], dataExtractor.FeaturesCount());

                  //clusteringTanukiETL = clusteringTanukiETL.AddDataTransformer(edgeDataExtractor.ConvertAll, edgeDataExtractor.FeaturesCount());
                  TanukiETL clusteringTanukiETL = new(edgeDataExtractor.ConvertAll, id => idLeafId[id], edgeDataExtractor.FeaturesCount());

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

                     // UNDONE DO NOT COMMIT
                     //accuracyResult.leavesAfter.Count.ShouldBe(dataSetAccuracy[dataSetName], dataSetName);
                  });

                  PrintEnd();
         //*/
      }

      //private static void SaveAllImages(ImmutableDictionary<BinaryTreeLeaf, ImmutableList<int>> falsePositives, ImmutableDictionary<int, SampleData> positions, string imageName, Rgba32 color)
      //{
      //   using Image<Rgba32> image = new(1000, 1000);
      //   foreach (var kvp in falsePositives)
      //   {
      //      foreach (int id in kvp.Value)
      //      {
      //         Point position = positions[id].Position;

      //         image[position.X, position.Y] = color;
      //      }

      //      SaveImage(kvp.Value, positions, imageName + $"_{kvp.Key.id}_{kvp.Key.labelValue}_{kvp.Value.Count}" + $"" + ".png", color);
      //   }
      //}

      //private static void SaveImage(ImmutableList<int> ids, ImmutableDictionary<int, SampleData> positions, string imageName, Rgba32 color)
      //{
      //   using Image<Rgba32> image = new(1000, 1000);
      //   foreach (int id in ids)
      //   {
      //      Point position = positions[id].Position;

      //      image[position.X, position.Y] = color;
      //   }

      //   image.SaveAsPng(Path.GetTempPath() + imageName);
      //}

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
