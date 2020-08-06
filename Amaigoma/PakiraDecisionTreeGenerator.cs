﻿namespace Amaigoma
{
   using MathNet.Numerics.Distributions;
   using MathNet.Numerics.LinearAlgebra;
   using MathNet.Numerics.Statistics;
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   public class PakiraDecisionTreeGenerator
   {
      static public int UNKNOWN_CLASS_INDEX = -1;
      static public int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
      static private int MINIMUM_SAMPLE_COUNT = 1000;
      private PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
      }

      public int MinimumSampleCount { get; set; }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels)
      {
         Generate(pakiraDecisionTreeModel, trainSamples, trainLabels, DefaultDataTransformer.ConvertAll);
      }

      public void Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<IList<double>> trainSamples, IList<double> trainLabels, Converter<IList<double>, IList<double>> dataTransformers)
      {
         ContinuousUniform continuousUniform = new ContinuousUniform(0, 256);
         IList<double> trainSample = trainSamples.ElementAt(0);
         int featureCount = trainSample.Count();
         bool generateMoreData = true;
         int dataDistributionSamplesCount = MinimumSampleCount * 3;
         IEnumerable<SabotenCache> trainSamplesCache = trainSamples.Select(d => new SabotenCache(d));
         TanukiTransformers theTransformers = new TanukiTransformers(dataTransformers, trainSample);

         while (generateMoreData)
         {
            Matrix<double> dataDistributionSamples = Matrix<double>.Build.Dense(dataDistributionSamplesCount, featureCount, (i, j) => continuousUniform.Sample());
            IEnumerable<SabotenCache> dataDistributionSamplesCache = dataDistributionSamples.EnumerateRows().Select(d => new SabotenCache(d));

            generateMoreData = false;

            pakiraDecisionTreeModel.Tree = BuildTree(trainSamplesCache, trainLabels, dataDistributionSamplesCache, theTransformers);

            generateMoreData = pakiraDecisionTreeModel.Tree.GetNodes().Any(pakiraNode => (pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX));

            dataDistributionSamplesCount *= 2;
         }

         pakiraDecisionTreeModel.DataTransformers = theTransformers;
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      static private bool ThresholdCompareGreater(double inputValue, double threshold)
      {
         return inputValue > threshold;
      }

      private PakiraTree BuildTree(IEnumerable<SabotenCache> trainSamplesCache, IEnumerable<double> trainLabels, IEnumerable<SabotenCache> dataDistributionSamplesCache, TanukiTransformers theTransformers)
      {
         IEnumerable<SabotenCache> extractedDataDistributionSamplesCache = dataDistributionSamplesCache.Take(MinimumSampleCount);

         int extractedDataDistributionSamplesCount = extractedDataDistributionSamplesCache.Count();

         if (extractedDataDistributionSamplesCount < MinimumSampleCount)
         {
            return PakiraTree.Empty.AddLeaf(new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX));
         }

         Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, trainSamplesCache, theTransformers);
         int bestFeatureIndex = tuple.Item1;
         double gain = tuple.Item2;
         double threshold = tuple.Item3;
         IEnumerable<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item4;
         IEnumerable<SabotenCache> bestSplitTrainSamplesCache = tuple.Item5;

         IEnumerable<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(dataDistributionSamplesCache.Skip(MinimumSampleCount));

         Func<double, double, bool>[] compareFunctions = { ThresholdCompareLessThanOrEqual, ThresholdCompareGreater };

         PakiraTree[] children = new PakiraTree[2];

         for (int i = 0; i < compareFunctions.Length; i++)
         {
            concatenatedDataDistributionSamples = concatenatedDataDistributionSamples.Prefetch(bestFeatureIndex, theTransformers);

            IEnumerable<SabotenCache> sampleSliceCache = concatenatedDataDistributionSamples.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold));

            IEnumerable<SabotenCache> slice = bestSplitTrainSamplesCache.Where(column => compareFunctions[i](column[bestFeatureIndex], threshold));
            PakiraTree child;

            if (slice.Count() > 0)
            {
               IEnumerable<double> ySlice = trainLabels.Where(
               (trainLabel, trainLabelIndex) =>
               {
                  double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                  return compareFunctions[i](trainSample, threshold);
               }
               );

               int labelCount = ySlice.Distinct().Count();

               // only one answer, set leaf
               if (labelCount == 1)
               {
                  child = PakiraTree.Empty.AddLeaf(new PakiraLeaf(ySlice.First()));
               }
               // otherwise continue to build tree
               else
               {
                  child = BuildTree(slice, ySlice, sampleSliceCache, theTransformers);
               }
            }
            else
            {
               // We don't have any training data for this node
               child = PakiraTree.Empty.AddLeaf(new PakiraLeaf(UNKNOWN_CLASS_INDEX));
            }

            children[i] = child;
         }

         PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);
         PakiraTree tree = PakiraTree.Empty.AddNode(node, children[0], children[1]);

         return tree;
      }

      private Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>> GetBestSplit(IEnumerable<SabotenCache> extractedDataDistributionSamplesCache, IEnumerable<SabotenCache> extractedTrainSamplesCache, TanukiTransformers theTransformers)
      {
         double[] gains = new double[theTransformers.TotalOutputSamples];
         ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();

         for (int featureIndex = 0; featureIndex < theTransformers.TotalOutputSamples; featureIndex++)
         {
            double gain = 1.0;

            extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCacheList.Prefetch(featureIndex, theTransformers).ToImmutableList();

            ImmutableList<double> featureDataDistributionSample = extractedDataDistributionSamplesCacheList.Select<SabotenCache, double>(sample =>
            {
               return sample[featureIndex];
            }
            ).ToImmutableList();

            extractedTrainSamplesCache = extractedTrainSamplesCache.Prefetch(featureIndex, theTransformers);

            foreach (SabotenCache trainSample in extractedTrainSamplesCache)
            {
               double trainSampleValue = trainSample[featureIndex];
               double quantileRank = featureDataDistributionSample.QuantileRank(trainSampleValue, RankDefinition.Default);

               // Keep the sample farthest in the data distribution
               gain = Math.Min(gain, Math.Min(quantileRank, 1.0 - quantileRank));
            }

            gains[featureIndex] = gain;
         }

         double bestGain = 1.0;
         int bestFeature = -1;

         for (int featureIndex = 0; featureIndex < theTransformers.TotalOutputSamples; featureIndex++)
         {
            double gain = gains[featureIndex];

            if (gain < bestGain)
            {
               bestGain = gain;
               bestFeature = featureIndex;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         double bestFeatureAverage = extractedDataDistributionSamplesCacheList.Select(sample => sample[bestFeature]).Mean();

         return new Tuple<int, double, double, IEnumerable<SabotenCache>, IEnumerable<SabotenCache>>(bestFeature, bestGain, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
      }
   }
}