global using BinaryTreeLeaf = (int id, int labelValue);

using Amaigoma;
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
   using System;
   using System.Collections.Generic;

   public static class DipApprox
   {

      public struct DipResult
      {
         public double Dip;      // dip value
         public int SplitIndex;  // best split bin index
      }

      // Hartigan’s dip
      public static DipResult ApproxDipAndSplit(double[] counts)
      {
         int nBins = counts.Length;
         if (nBins < 3)
            return new DipResult { Dip = 0.0, SplitIndex = -1 };

         // 1) Build normalized CDF
         double total = 0.0;
         for (int i = 0; i < nBins; i++) total += counts[i];
         if (total <= 0.0)
            return new DipResult { Dip = 0.0, SplitIndex = -1 };

         double[] cdf = new double[nBins];
         double cum = 0.0;
         for (int i = 0; i < nBins; i++)
         {
            cum += counts[i];
            cdf[i] = cum / total;
         }

         // 2) GCM and LCM
         double[] gcm = BuildGCM(cdf);
         double[] lcm = BuildLCM(cdf);

         // 3) Find dip and split index
         double dip = 0.0;
         int splitIndex = -1;

         for (int i = 0; i < nBins; i++)
         {
            double d1 = cdf[i] - gcm[i];
            double d2 = lcm[i] - cdf[i];
            double d = Math.Max(d1, d2);

            if (d > dip)
            {
               dip = d;
               splitIndex = i;   // NEW: record where dip occurs
            }
         }

         return new DipResult { Dip = dip, SplitIndex = splitIndex };
      }

      // Main entry: counts = histogram bin counts (non-negative)
      //public static double ApproxDipFromHistogram(double[] counts)
      //{
      //   int nBins = counts.Length;
      //   if (nBins < 3) return 0.0;

      //   // 1) Build normalized CDF over bin centers
      //   double total = 0.0;
      //   for (int i = 0; i < nBins; i++) total += counts[i];
      //   if (total <= 0.0) return 0.0;

      //   double[] cdf = new double[nBins];
      //   double cum = 0.0;
      //   for (int i = 0; i < nBins; i++)
      //   {
      //      cum += counts[i];
      //      cdf[i] = cum / total;
      //   }

      //   // 2) Build greatest convex minorant (GCM) of CDF
      //   double[] gcm = BuildGCM(cdf);

      //   // 3) Build least concave majorant (LCM) of CDF
      //   double[] lcm = BuildLCM(cdf);

      //   // 4) Dip ≈ max vertical distance between CDF and [GCM, LCM]
      //   double dip = 0.0;
      //   for (int i = 0; i < nBins; i++)
      //   {
      //      double d1 = cdf[i] - gcm[i];
      //      double d2 = lcm[i] - cdf[i];
      //      double d = Math.Max(d1, d2);
      //      if (d > dip) dip = d;
      //   }

      //   return dip;
      //}

      // Greatest Convex Minorant (from below) of discrete CDF
      private static double[] BuildGCM(double[] cdf)
      {
         int n = cdf.Length;
         double[] x = new double[n];
         for (int i = 0; i < n; i++) x[i] = i; // bin indices as x

         // Pool-adjacent-violators style for convexity on slopes
         List<int> idx = new List<int>();
         idx.Add(0);

         for (int i = 1; i < n; i++)
         {
            idx.Add(i);
            while (idx.Count >= 3)
            {
               int k = idx.Count;
               int i1 = idx[k - 3];
               int i2 = idx[k - 2];
               int i3 = idx[k - 1];

               double s12 = (cdf[i2] - cdf[i1]) / (x[i2] - x[i1]);
               double s23 = (cdf[i3] - cdf[i2]) / (x[i3] - x[i2]);

               // For convex minorant, slopes must be non-decreasing
               if (s23 < s12)
               {
                  idx.RemoveAt(k - 2); // merge middle point
               }
               else break;
            }
         }

         // Now interpolate linearly between knots in idx
         double[] gcm = new double[n];
         for (int k = 0; k < idx.Count - 1; k++)
         {
            int i1 = idx[k];
            int i2 = idx[k + 1];
            double x1 = x[i1], x2 = x[i2];
            double y1 = cdf[i1], y2 = cdf[i2];
            double slope = (y2 - y1) / (x2 - x1);

            for (int i = i1; i <= i2; i++)
            {
               gcm[i] = y1 + slope * (x[i] - x1);
            }
         }

         return gcm;
      }

      // Least Concave Majorant (from above) of discrete CDF
      private static double[] BuildLCM(double[] cdf)
      {
         int n = cdf.Length;
         double[] x = new double[n];
         for (int i = 0; i < n; i++) x[i] = i;

         List<int> idx = new List<int>();
         idx.Add(n - 1);

         for (int i = n - 2; i >= 0; i--)
         {
            idx.Add(i);
            while (idx.Count >= 3)
            {
               int k = idx.Count;
               int i1 = idx[k - 1];
               int i2 = idx[k - 2];
               int i3 = idx[k - 3];

               double s12 = (cdf[i2] - cdf[i1]) / (x[i2] - x[i1]);
               double s23 = (cdf[i3] - cdf[i2]) / (x[i3] - x[i2]);

               // For concave majorant, slopes must be non-increasing
               if (s23 > s12)
               {
                  idx.RemoveAt(k - 2); // merge middle point
               }
               else break;
            }
         }

         idx.Sort();

         double[] lcm = new double[n];
         for (int k = 0; k < idx.Count - 1; k++)
         {
            int i1 = idx[k];
            int i2 = idx[k + 1];
            double x1 = x[i1], x2 = x[i2];
            double y1 = cdf[i1], y2 = cdf[i2];
            double slope = (y2 - y1) / (x2 - x1);

            for (int i = i1; i <= i2; i++)
            {
               lcm[i] = y1 + slope * (x[i] - x1);
            }
         }

         return lcm;
      }
   }

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
      //private static readonly ImmutableList<double> emptyHistogramDouble = [.. Enumerable.Repeat(0.0, 256)];
      private static readonly ImmutableList<int> emptyHistogram8 = [.. Enumerable.Repeat(0, 8)];
      private static readonly ImmutableList<int> emptyHistogram254 = [.. Enumerable.Repeat(0, 255)];

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
         int total = counts.Count(x =>
         {
            x.ShouldBeGreaterThanOrEqualTo(0);
            return x > 0;
         });

         total.ShouldNotBe(0);

         double p = 1.0 / total;

         return -total * p * Math.Log2(p);
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

      // UNDONE Change this name (3)
      public (int featureIndex, double splitThreshold) GetBestSplitClustering3(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         double bestFeatureSplitCount = double.MaxValue;

         if (ids.Count > 0)
         {
            // TODO No need to keep all entropies, only the best one
            ImmutableList<double> weigthedEntropies = [];
            ImmutableList<int> sampleIds = [.. ids];
            ImmutableHashSet<int> allLabels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
            ImmutableDictionary<int, ImmutableList<int>> allHistograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            // UNDONE DO NOT COMMIT
            ImmutableList<string> tempDebugData = [];

            foreach (int label in allLabels)
            {
               allHistograms = allHistograms.SetItem(label, emptyHistogram);
            }

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = allHistograms;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  ImmutableList<int> histogram = histograms[label];
                  int currentData = transformedData[i];

                  histograms = histograms.SetItem(label, histogram.SetItem(currentData, histogram[currentData] + 1));

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

               // UNDONE DO NOT COMMIT
               ImmutableList<double> splitValuesWeigthedEntropies = [];

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

                     int leftTotalCountForEntropy = leftLabelTotalCount.Count(x => x.Value > 0);
                     int rightTotalCountForEntropy = rightLabelTotalCount.Count(x => x.Value > 0);

                     double leftEntropy = CalculateEntropySingle(leftLabelTotalCount.Select(c => c.Value));
                     double rightEntropy = CalculateEntropySingle(rightLabelTotalCount.Select(c => c.Value));
                     double weightedEntropy = leftTotalCountForEntropy * leftEntropy + rightTotalCountForEntropy * rightEntropy;

                     tempDebugData = tempDebugData.Add($"Feature {featureIndex}, split {splitValue}, left count {leftTotalCountForEntropy}, right count {rightTotalCountForEntropy}, left entropy {leftEntropy}, right entropy {rightEntropy}, weighted entropy {weightedEntropy}");

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

                     splitValuesWeigthedEntropies = splitValuesWeigthedEntropies.Add(weightedEntropy);
                  }
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 ||
                  weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestFeatureSplitCount < bestSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += bestSplitCount / 2;
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
            //weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);
         }

         return (bestFeature, bestFeatureSplit);
      }

      private static ImmutableList<int> BinHistogramInto8(IReadOnlyList<int> histogram, int startInclusive, int endExclusive)
      {
         ImmutableList<int> binnedHistogram = emptyHistogram8;
         int binSize = 256 / 8;

         for (int i = startInclusive; i < endExclusive; i++)
         {
            int binIndex = i / binSize;
            binnedHistogram = binnedHistogram.SetItem(binIndex, binnedHistogram[binIndex] + histogram[i]);
         }

         return binnedHistogram;
      }

      private static double JensenShannonDivergence(IReadOnlyList<int> leftCounts, IReadOnlyList<int> rightCounts)
      {
         double leftTotal = leftCounts.Sum();
         double rightTotal = rightCounts.Sum();

         if (leftTotal == 0 || rightTotal == 0)
            return 0; // No divergence if one side is empty

         int k = leftCounts.Count;
         double js = 0.0;

         for (int i = 0; i < k; i++)
         {
            double p = leftCounts[i] / leftTotal;
            double q = rightCounts[i] / rightTotal;
            double m = 0.5 * (p + q);

            if (p > 0)
               js += 0.5 * p * Math.Log2(p / m);

            if (q > 0)
               js += 0.5 * q * Math.Log2(q / m);
         }

         return Math.Min(js, 1.0);
      }

      private static double GiniImpurity(IReadOnlyList<int> counts)
      {
         double total = counts.Sum();
         if (total == 0)
            return 0.0;

         double sumSq = 0.0;

         foreach (int c in counts)
         {
            if (c == 0) continue;
            double p = c / total;
            sumSq += p * p;
         }

         return 1.0 - sumSq;
      }

      public (int featureIndex, double splitThreshold) GetBestSplitClusteringJensenShannon(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         double bestFeatureSplitCount = double.MaxValue;

         if (ids.Count > 0)
         {
            // TODO No need to keep all entropies, only the best one
            ImmutableList<double> weigthedEntropies = [];
            // TODO Parametrize the Take count
            ImmutableList<int> sampleIds = [.. ids.Take(1000)];
            ImmutableHashSet<int> allLabels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
            ImmutableDictionary<int, ImmutableList<int>> allHistograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            // UNDONE Debugging purpose.
            // ImmutableList<double> shannonEntropies = [];

            // UNDONE DO NOT COMMIT
            ImmutableList<string> tempDebugData = [];

            foreach (int label in allLabels)
            {
               allHistograms = allHistograms.SetItem(label, emptyHistogram);
            }

            // UNDONE Use ShannonEntropy to decide if the feature is worth splitting. Usually lower than 0.85 starts to be good, but a range of 0.4-0.7 is safer.
            // Anything under 0.4 should be great. NOTE that the current ShannonEntropy is not normalized. Since the histogram used has 256 bins, the max value is 8.
            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

               int bestSplitValue = -1;
               double bestWeightedEntropy = 0;
               int bestSplitCount = 0;
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = allHistograms;
               ImmutableList<int> mergedHistogram = emptyHistogram;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  ImmutableList<int> histogram = histograms[label];
                  int currentData = transformedData[i];

                  histograms = histograms.SetItem(label, histogram.SetItem(currentData, histogram[currentData] + 1));

                  mergedHistogram = mergedHistogram.SetItem(currentData, mergedHistogram[currentData] + 1);
               }

               // shannonEntropies = shannonEntropies.Add(ShannonEntropy(mergedHistogram));

               int minimumSampleCount = Convert.ToInt32(0.33 * sampleIds.Count);
               int maximumSampleCount = Convert.ToInt32(0.66 * sampleIds.Count);
               int lowSplitValue = 0;
               int highSplitValue = 0;
               int sampleCountSum = 0;

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  sampleCountSum += mergedHistogram[splitValue];

                  if (sampleCountSum <= minimumSampleCount)
                  {
                     lowSplitValue = splitValue;
                  }

                  if (sampleCountSum < maximumSampleCount)
                  {
                     highSplitValue = splitValue;
                  }
               }

               // UNDONE DO NOT COMMIT
               ImmutableList<double> splitValuesWeigthedEntropies = [];

               for (int splitValue = lowSplitValue; splitValue <= highSplitValue; splitValue++)
               {
                  foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

                     ImmutableList<int> leftHistogram = BinHistogramInto8(mergedHistogram, 0, splitValue + 1 + 10);
                     ImmutableList<int> rightHistogram = BinHistogramInto8(mergedHistogram, splitValue + 1 - 10, 256);

                     double weightedEntropy = JensenShannonDivergence(leftHistogram, rightHistogram);

                     weightedEntropy.ShouldBeLessThan(1.0);

                     // tempDebugData = tempDebugData.Add($"Feature {featureIndex}, split {splitValue}, weightedEntropy {weightedEntropy}");

                     if (weightedEntropy > bestWeightedEntropy)
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

                     splitValuesWeigthedEntropies = splitValuesWeigthedEntropies.Add(weightedEntropy);
                  }
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 ||
                  weigthedEntropies[featureIndex] > weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestFeatureSplitCount < bestSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += bestSplitCount / 2;
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }

            // double minShannonEntropies = shannonEntropies.Min();
            // double bestShannonEntropies = shannonEntropies[bestFeature];

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
            //weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);
         }

         return (bestFeature, bestFeatureSplit);
      }

      public (int featureIndex, double splitThreshold) GetBestSplitClusteringMean(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;

         // UNDONE Replace 9999 by datadistribution
         ImmutableList<int> dataDistributionIds = [.. ids.Where(id => tanukiETL.TanukiLabelExtractor(id) == 9999).Take(1000)];

         if (dataDistributionIds.Count == 1000)
         {
            double bestFeatureSplitCount = double.MaxValue;
            ImmutableList<double> weigthedEntropies = [];

            ImmutableList<int> trainDataIds = [.. ids.Where(id => tanukiETL.TanukiLabelExtractor(id) != 9999)];

            //ImmutableList<int> sampleIds = [.. ids.Take(1000)];
            //ImmutableHashSet<int> allLabels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
            //ImmutableDictionary<int, ImmutableList<int>> allHistograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            //foreach (int label in allLabels)
            //{
            //   allHistograms = allHistograms.SetItem(label, emptyHistogram);
            //}

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               ImmutableList<int> transformedData = [.. dataDistributionIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

               int bestSplitValue = -1;
               double bestWeightedEntropy = 0;
               int bestSplitCount = 0;
               int leftTotalCount = 0;
               int rightTotalCount = dataDistributionIds.Count;
               int leftTotalWeightedCount = 0;
               int rightTotalWeightedCount = 0;
               //ImmutableDictionary<int, ImmutableList<int>> histograms = allHistograms;
               //ImmutableList<int> mergedHistogram = emptyHistogram;
               //ImmutableList<double> mergedHistogramDouble = emptyHistogramDouble;
               ImmutableList<int> histogram = emptyHistogram;

               double standardDeviation = ArrayStatistics.StandardDeviation(transformedData.ToArray());

               for (int i = 0; i < dataDistributionIds.Count; i++)
               {
                  //int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  //ImmutableList<int> histogram = histograms[label];
                  int currentData = transformedData[i];

                  histogram = histogram.SetItem(currentData, histogram[currentData] + 1);

                  //mergedHistogram = mergedHistogram.SetItem(currentData, mergedHistogram[currentData] + 1);
                  //mergedHistogramDouble = mergedHistogramDouble.SetItem(currentData, mergedHistogram[currentData] + 1);

                  rightTotalWeightedCount += currentData;
               }

               double fullHistogramMean = (double)rightTotalWeightedCount / rightTotalCount;
               int minimumSampleCount = Convert.ToInt32(0.33 * dataDistributionIds.Count);
               int maximumSampleCount = Convert.ToInt32(0.66 * dataDistributionIds.Count);
               int lowSplitValue = 0;
               int highSplitValue = 0;
               int sampleCountSum = 0;

               for (int splitValue = 0; splitValue < 256; splitValue++)
               {
                  sampleCountSum += histogram[splitValue];

                  if (sampleCountSum <= minimumSampleCount)
                  {
                     lowSplitValue = splitValue;
                  }

                  if (sampleCountSum < maximumSampleCount)
                  {
                     highSplitValue = splitValue;
                  }
               }

               //ImmutableList<int> leftHistogram = emptyHistogram254;
               //ImmutableList<int> rightHistogram = mergedHistogram;

               for (int splitValue = 0; splitValue < lowSplitValue; splitValue++)
               {
                  leftTotalCount += histogram[splitValue];
                  rightTotalCount -= histogram[splitValue];
                  leftTotalWeightedCount += splitValue * histogram[splitValue];
                  rightTotalWeightedCount -= splitValue * histogram[splitValue];
                  //leftHistogram = leftHistogram.SetItem(splitValue, mergedHistogram[splitValue]);
                  //rightHistogram = rightHistogram.SetItem(splitValue, 0);
               }

               for (int splitValue = lowSplitValue; splitValue <= highSplitValue; splitValue++)
               {
                  //foreach ((int label, ImmutableList<int> histogram) in histograms)
                  {
                     int binCount = histogram[splitValue];

                     leftTotalCount += binCount;
                     rightTotalCount -= binCount;
                     leftTotalWeightedCount += splitValue * histogram[splitValue];
                     rightTotalWeightedCount -= splitValue * histogram[splitValue];
                     //leftHistogram = leftHistogram.SetItem(splitValue, mergedHistogram[splitValue]);
                     //rightHistogram = rightHistogram.SetItem(splitValue, 0);
                  }

                  if (leftTotalCount > 0 && rightTotalCount > 0)
                  {
                     leftTotalCount.ShouldBeGreaterThanOrEqualTo(0);
                     rightTotalCount.ShouldBeGreaterThanOrEqualTo(0);

                     //for (int i = 0; i <= splitValue; i++)
                     //{
                     //   leftHistogram = leftHistogram.SetItem(i, mergedHistogram[i]);
                     //}

                     //for (int i = splitValue + 1; i < 256; i++)
                     //{
                     //   rightHistogram = rightHistogram.SetItem(i - splitValue - 1, mergedHistogram[i]);
                     //}

                     double leftHistogramMean = (double)leftTotalWeightedCount / leftTotalCount;
                     double rightHistogramMean = (double)rightTotalWeightedCount / rightTotalCount;
                     double weightedEntropy = Math.Pow(fullHistogramMean - leftHistogramMean, 2) + Math.Pow(fullHistogramMean - rightHistogramMean, 2);

                     weightedEntropy = Math.Abs(leftHistogramMean - rightHistogramMean) / (standardDeviation + double.Epsilon);

                     //ImmutableList<double> normalizedHistogram = ImmutableList<double>.Empty;
                     double k = 1.0 / 256;

                     double uniformShapeWeight = 0;

                     for (int i = 0; i < 256; i++)
                     {
                        double normalizedBin = (double)histogram[i] / transformedData.Count;
                        //normalizedHistogram.Add(histogram[i] / transformedData.Count);
                        double localWeight = normalizedBin - k;

                        uniformShapeWeight += localWeight * localWeight;
                     }

                     weightedEntropy *= uniformShapeWeight;

                     if (weightedEntropy > bestWeightedEntropy)
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
                  weigthedEntropies[featureIndex] > weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestFeatureSplitCount < bestSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += bestSplitCount / 2;
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);
         }

         return (bestFeature, bestFeatureSplit);
      }

      private static double ShannonEntropy(IEnumerable<int> counts)
      {
         double total = counts.Sum();
         if (total == 0) return 0;

         double sum = 0;
         foreach (int c in counts)
         {
            if (c == 0) continue;
            double p = c / total;
            sum += -p * Math.Log2(p);
         }
         return sum;
      }

      public (int featureIndex, double splitThreshold) GetBestSplitClusteringShannon(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         double bestFeatureSplitCount = double.MaxValue;

         if (ids.Count > 0)
         {
            // TODO No need to keep all entropies, only the best one
            ImmutableList<double> weigthedEntropies = [];
            ImmutableList<int> sampleIds = [.. ids];
            ImmutableHashSet<int> allLabels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
            ImmutableDictionary<int, ImmutableList<int>> allHistograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            // UNDONE DO NOT COMMIT
            ImmutableList<string> tempDebugData = [];

            foreach (int label in allLabels)
            {
               allHistograms = allHistograms.SetItem(label, emptyHistogram);
            }

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = allHistograms;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  ImmutableList<int> histogram = histograms[label];
                  int currentData = transformedData[i];

                  histograms = histograms.SetItem(label, histogram.SetItem(currentData, histogram[currentData] + 1));

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

               // UNDONE DO NOT COMMIT
               ImmutableList<double> splitValuesWeigthedEntropies = [];

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

                     int leftTotalCountForEntropy = leftLabelTotalCount.Count(x => x.Value > 0);
                     int rightTotalCountForEntropy = rightLabelTotalCount.Count(x => x.Value > 0);

                     double leftEntropy = ShannonEntropy(leftLabelTotalCount.Select(c => c.Value));
                     double rightEntropy = ShannonEntropy(rightLabelTotalCount.Select(c => c.Value));
                     double weightedEntropy =
                         (leftTotalCount / (double)sampleIds.Count) * leftEntropy +
                         (rightTotalCount / (double)sampleIds.Count) * rightEntropy;

                     tempDebugData = tempDebugData.Add($"Feature {featureIndex}, split {splitValue}, left count {leftTotalCountForEntropy}, right count {rightTotalCountForEntropy}, left entropy {leftEntropy}, right entropy {rightEntropy}, weighted entropy {weightedEntropy}");

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

                     splitValuesWeigthedEntropies = splitValuesWeigthedEntropies.Add(weightedEntropy);
                  }
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 ||
                  weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestFeatureSplitCount < bestSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += bestSplitCount / 2;
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
            //weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);
         }

         return (bestFeature, bestFeatureSplit);
      }

      bool UseJSD(int N, int K, double imbalance)
      {
         if (N < 20)
            return false; // Gini more stable with small sample sizes

         if (imbalance > 0.75)
            return true; // JSD handles imbalance better

         if (K >= 4)
            return true; // many labels => JSD captures structure better

         return false; // default to Gini
      }

      public (int featureIndex, double splitThreshold) GetBestSplitClusteringHybrid(IReadOnlyList<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = double.MaxValue;
         double bestFeatureSplitCount = double.MaxValue;

         if (ids.Count > 0)
         {
            // TODO No need to keep all entropies, only the best one
            ImmutableList<double> weigthedEntropies = [];
            ImmutableList<int> sampleIds = [.. ids];
            ImmutableHashSet<int> allLabels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
            ImmutableDictionary<int, ImmutableList<int>> allHistograms = ImmutableDictionary<int, ImmutableList<int>>.Empty;

            ImmutableDictionary<int, int> labelCounts;
            var builder = ImmutableDictionary.CreateBuilder<int, int>();

            foreach (int id in ids)
            {
               int label = tanukiETL.TanukiLabelExtractor(id);

               if (builder.TryGetValue(label, out int count))
                  builder[label] = count + 1;
               else
                  builder[label] = 1;
            }

            labelCounts = builder.ToImmutable();

            int totalSamples = sampleIds.Count;
            int K = allLabels.Count; // number of distinct labels
            int majority = labelCounts.Values.Max();
            double imbalance = majority / (double)totalSamples;

            bool useJSD = UseJSD(totalSamples, K, imbalance);

            // UNDONE DO NOT COMMIT
            ImmutableList<string> tempDebugData = [];

            foreach (int label in allLabels)
            {
               allHistograms = allHistograms.SetItem(label, emptyHistogram);
            }

            for (int featureIndex = 0; featureIndex < tanukiETL.TanukiFeatureCount; featureIndex++)
            {
               ImmutableList<int> transformedData = [.. sampleIds.Select(id => tanukiETL.TanukiDataTransformer(id, featureIndex))];

               int bestSplitValue = -1;
               double bestWeightedEntropy = double.MaxValue;
               int bestSplitCount = 0;
               ImmutableDictionary<int, int> leftLabelTotalCount = [];
               ImmutableDictionary<int, int> rightLabelTotalCount = [];
               int leftTotalCount = 0;
               int rightTotalCount = sampleIds.Count;
               ImmutableDictionary<int, ImmutableList<int>> histograms = allHistograms;

               for (int i = 0; i < sampleIds.Count; i++)
               {
                  int label = tanukiETL.TanukiLabelExtractor(sampleIds[i]);

                  ImmutableList<int> histogram = histograms[label];
                  int currentData = transformedData[i];

                  histograms = histograms.SetItem(label, histogram.SetItem(currentData, histogram[currentData] + 1));

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

               // UNDONE DO NOT COMMIT
               ImmutableList<double> splitValuesWeigthedEntropies = [];

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

                     double weightedEntropy;
                     //int leftTotalCountForEntropy = leftLabelTotalCount.Count(x => x.Value > 0);
                     //int rightTotalCountForEntropy = rightLabelTotalCount.Count(x => x.Value > 0);

                     //double leftEntropy = ShannonEntropy(leftLabelTotalCount.Select(c => c.Value));
                     //double rightEntropy = ShannonEntropy(rightLabelTotalCount.Select(c => c.Value));
                     //double weightedEntropy =
                     //    (leftTotalCount / (double)sampleIds.Count) * leftEntropy +
                     //    (rightTotalCount / (double)sampleIds.Count) * rightEntropy;

                     //tempDebugData = tempDebugData.Add($"Feature {featureIndex}, split {splitValue}, left count {leftTotalCountForEntropy}, right count {rightTotalCountForEntropy}, left entropy {leftEntropy}, right entropy {rightEntropy}, weighted entropy {weightedEntropy}");

                     List<int> labels = allLabels.OrderBy(x => x).ToList();

                     List<int> leftCounts = labels.Select(l => leftLabelTotalCount[l]).ToList();
                     List<int> rightCounts = labels.Select(l => rightLabelTotalCount[l]).ToList();

                     if (useJSD)
                     {
                        List<int> leftHistogram = [.. Enumerable.Repeat(0, 256)];
                        List<int> rightHistogram = [.. Enumerable.Repeat(0, 256)];

                        for (int i = 0; i <= splitValue; i++)
                        {
                           leftHistogram[i] = histograms[2][i];
                        }

                        for (int i = splitValue + 1; i < 256; i++)
                        {
                           rightHistogram[i - splitValue + 1] = histograms[2][i];
                        }

                        weightedEntropy = -JensenShannonDivergence(leftHistogram, rightHistogram);
                     }
                     else
                     {
                        double leftGini = GiniImpurity(leftCounts);
                        double rightGini = GiniImpurity(rightCounts);

                        weightedEntropy =
                            (leftTotalCount / (double)totalSamples) * leftGini +
                            (rightTotalCount / (double)totalSamples) * rightGini;
                     }

                     tempDebugData = tempDebugData.Add($"Feature {featureIndex}, split {splitValue}, weighted entropy {weightedEntropy}");

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

                     splitValuesWeigthedEntropies = splitValuesWeigthedEntropies.Add(weightedEntropy);
                  }
               }

               weigthedEntropies = weigthedEntropies.Add(bestWeightedEntropy);

               if (bestFeature == -1 ||
                  weigthedEntropies[featureIndex] < weigthedEntropies[bestFeature] ||
                  (weigthedEntropies[featureIndex] == weigthedEntropies[bestFeature] && bestFeatureSplitCount < bestSplitCount))
               {
                  if (bestSplitCount > 1)
                  {
                     bestSplitValue += bestSplitCount / 2;
                  }

                  bestFeature = featureIndex;
                  bestFeatureSplit = bestSplitValue;
                  bestFeatureSplitCount = bestSplitCount;
               }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            // TODO This is not even returned, but maybe it could be returned and then used as tree quality criteria
            //weigthedEntropies = weigthedEntropies.SetItem(bestFeature, weigthedEntropies[bestFeature] / sampleIds.Count);
         }

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

         dataSet = dataSet.AddRegion("Train", new(@"assets/text-extraction-for-ocr/507484246.tif", train_507484246_Rectangles, train_507484246_Labels));
         dataSet = dataSet.AddRegion("Validation", new(@"assets/text-extraction-for-ocr/507484246.tif", validation_507484246_Rectangles, validation_507484246_Labels));
         dataSet = dataSet.AddRegion("Test", new(@"assets/text-extraction-for-ocr/507484246.tif", test_507484246_Rectangles, test_507484246_Labels));
         dataSet = dataSet.AddRegion("im164", new(@"assets/mirflickr08/im164.jpg", im164Rectangles, im164Labels));
         dataSet = dataSet.AddRegion("im10", new(@"assets/mirflickr08/im10.jpg", im10Rectangles, im10Labels));
         dataSet = dataSet.AddRegion("ti31149327_9330", new(@"assets/text-extraction-for-ocr/ti31149327_9330.tif", train_ti31149327_9330_Rectangles, train_ti31149327_9330_Labels));

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
         ImmutableList<int> averageTransformerSizes = [5, 3];
         //ImmutableList<int> averageTransformerSizes = [17, 7, 5, 3, 1];
         ImmutableDictionary<string, int> dataSetAccuracy = ImmutableDictionary.CreateRange(new Dictionary<string, int> {
          {"Train", 37},
          {"Validation", 30},
          {"Test", 31},
          {"im164", 35},
          {"im10", 37},
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

         ImmutableList<int> abc = im164Positions.Keys.Union(trainPositions.Keys).ToImmutableList();

         PakiraDecisionTreeModel modelabc = pakiraGeneratorClusteringHybrid.Generate(new(), abc.Shuffle(new Random(-42)), tanukiETL);

         ImmutableList<PakiraDecisionTreeModel> models = Enumerable.Range(0, 100).AsParallel().Select(i =>
         {
            return pakiraGeneratorClusteringHybrid.Generate(new(), im164Positions.Keys.Shuffle(new Random(42 + i)), tanukiETL);
         }).ToImmutableList();

         ImmutableList<string> testNames = ["Train", "Test", "Validation"];
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
         IEnumerable<int> shuffledTrainData = im164Positions.Keys.Shuffle(new Random(42));

         // PakiraDecisionTreeModel pakiraDecisionTreeModelClusteringHybrid = pakiraGeneratorClusteringHybrid.Generate(new(), shuffledTrainData, tanukiETL);
         //PakiraDecisionTreeModel pakiraDecisionTreeModelClusteringHybrid = pakiraGeneratorClusteringHybrid.Generate(new(), trainPositions.Keys, tanukiETL);

         // Compute leaf ID of each tree for all Train positions and build mapping
         ImmutableList<ImmutableDictionary<int, ImmutableList<int>>> modelsTrainLeafMaps = Enumerable.Range(0, models.Count)
            .Select(i =>
            {
               PakiraDecisionTreeModel model = models[i];
               PakiraTreeWalker walker = new(model.Tree, tanukiETL);

               ImmutableDictionary<int, ImmutableList<int>> leafToTrainIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;

               foreach (int trainId in trainPositions.Keys)
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

         // UNDONE Add logic to select the best model trees used for majority vote evaluation.

         // For each Test position, collect votes from each model about which Train samples fall in the same leaf
         File.AppendAllText("results.txt", "MajorityVoteSimilarity\n");

         Dictionary<int, int> overallHighestVoteCountByLabel = new();

         foreach (int testId in validationPositions.Keys)
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
            string line = testId.ToString() + "," + validationPositions[testId].Label + ",Votes=" + maxVotes + ",Winners=" + string.Join(';', winners) + ",WinnerLabels=" + string.Join(';', winnerLabels) + ",LabelMaxVotes=" + labelMaxVotes;
            File.AppendAllText("results.txt", line + Environment.NewLine);
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
         return;

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
         pakiraGenerator2 = new(bestSplitLogic.GetBestSplitClusteringJensenShannon);
         pakiraGenerator2 = new(bestSplitLogic.GetBestSplitClusteringShannon);
         //pakiraGenerator2 = new(bestSplitLogic.GetBestSplitClusteringHybrid);

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
