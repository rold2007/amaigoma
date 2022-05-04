﻿using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Statistics;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record TrainData
   {
      public ImmutableList<ImmutableList<double>> Samples { get; } = ImmutableList<ImmutableList<double>>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainData()
      {
      }

      public TrainData(ImmutableList<ImmutableList<double>> samples, ImmutableList<double> labels)
      {
         Samples = samples;
         Labels = labels;
      }

      public TrainData AddSample(IEnumerable<double> sample, double label)
      {
         ImmutableList<double> immutableSample = sample.ToImmutableList();

         if (!Samples.IsEmpty)
         {
            immutableSample.Count.ShouldBe(Samples[0].Count);
         }

         return new TrainData(Samples.Add(immutableSample), Labels.Add(label));
      }
   }

   public sealed record TrainDataCache
   {
      public ImmutableList<SabotenCache> Samples { get; } = ImmutableList<SabotenCache>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainDataCache(ImmutableList<SabotenCache> samples, ImmutableList<double> labels)
      {
         Samples = samples;
         Labels = labels;
      }

      public TrainDataCache AddSamples(TrainDataCache samples)
      {
         samples.Samples.Count.ShouldBeGreaterThan(0);
         samples.Labels.Count.ShouldBeGreaterThan(0);

         if (!Samples.IsEmpty)
         {
            samples.Samples[0].Data.Count().ShouldBe(Samples[0].Data.Count());
         }

         return new TrainDataCache(Samples.AddRange(samples.Samples), Labels.AddRange(samples.Labels));
      }
   }

   public class PakiraDecisionTreeGenerator
   {
      private sealed record PakiraLeafResult
      {
         public PakiraLeaf pakiraLeaf;
         public ImmutableList<SabotenCache> slice;
         public ImmutableList<double> ySlice;
      }

      static public readonly int UNKNOWN_CLASS_INDEX = -1;
      static public readonly int randomSeed = new Random().Next();
      static private readonly int MINIMUM_SAMPLE_COUNT = 1000;
      static private readonly double DEFAULT_CERTAINTY_SCORE = 2.0;
      static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();
      private readonly Random RandomSource = new Random(randomSeed);
      private readonly DiscreteUniform discreteUniform;

      public PakiraDecisionTreeGenerator()
      {
         MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
         CertaintyScore = DEFAULT_CERTAINTY_SCORE;
         discreteUniform = new DiscreteUniform(0, 255, RandomSource);
      }

      public int MinimumSampleCount { get; set; }

      public double CertaintyScore { get; set; }

      public double UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainData trainData)
      {
         ImmutableList<SabotenCache> trainSamplesCache = pakiraDecisionTreeModel.PrefetchAll(trainData.Samples.Select(d => new SabotenCache(d)));
         ImmutableList<double> immutableTrainLabels = trainData.Labels;

         foreach (SabotenCache sabotenCache in trainSamplesCache)
         {
            foreach (int featureIndex in pakiraDecisionTreeModel.FeatureIndices())
            {
               double trainSampleValue = sabotenCache[featureIndex];

               trainSampleValue.ShouldBeGreaterThanOrEqualTo(0.0);
               trainSampleValue.ShouldBeLessThanOrEqualTo(255.0);
            }
         }

         if (pakiraDecisionTreeModel.Tree.Root == null)
         {
            TrainDataCache trainDataCache = new TrainDataCache(trainSamplesCache, immutableTrainLabels);

            pakiraDecisionTreeModel = InitializeDistributionSamples(pakiraDecisionTreeModel, trainData);

            pakiraDecisionTreeModel = BuildInitialTree(pakiraDecisionTreeModel, trainDataCache);
         }
         else
         {
            for (int trainSampleIndex = 0; trainSampleIndex < trainSamplesCache.Count; trainSampleIndex++)
            {
               PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(trainSamplesCache[trainSampleIndex]);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraDecisionTreePredictionResult.PakiraLeaf, new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(pakiraDecisionTreePredictionResult.SabotenCache), ImmutableList<double>.Empty.Add(immutableTrainLabels[trainSampleIndex])));
            }
         }

         return BuildTree(pakiraDecisionTreeModel);
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private struct ProcessLeaf
      {
         public ProcessLeaf(PakiraNode parentNode, PakiraLeaf leaf, TrainDataCache trainSamplesCache)
         {
            ParentNode = parentNode;
            Leaf = leaf;
            TrainSamplesCache = trainSamplesCache;
         }

         public PakiraNode ParentNode;
         public PakiraLeaf Leaf;
         public TrainDataCache TrainSamplesCache;
      };

      private PakiraDecisionTreeModel InitializeDistributionSamples(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainData trainData)
      {
         ImmutableList<double> trainSample = trainData.Samples[0];
         int featureCount = trainSample.Count();
         ImmutableList<SabotenCache> distributionSamples = ImmutableList<SabotenCache>.Empty;

         for (int i = 0; i < MinimumSampleCount; i++)
         {
            SabotenCache newSampleCache = new SabotenCache(DenseVector.Create(featureCount, (dataIndex) =>
            {
               return discreteUniform.Sample();
            }
          ));
            distributionSamples = distributionSamples.Add(newSampleCache);
         }

         distributionSamples = pakiraDecisionTreeModel.PrefetchAll(distributionSamples);

         ImmutableList<double> dataDistributionSamplesMean = ImmutableList<double>.Empty;
         ImmutableList<double> dataDistributionSamplesInvertedStandardDeviation = ImmutableList<double>.Empty;

         foreach (int featureIndex in pakiraDecisionTreeModel.FeatureIndices())
         {
            ImmutableList<double> featureDataDistributionSample = distributionSamples.Select<SabotenCache, double>(sample =>
            {
               return sample[featureIndex];
            }
            ).ToImmutableList();

            Tuple<double, double> featureDataDistributionSampleMeanStandardDeviation = featureDataDistributionSample.MeanStandardDeviation();
            double featureDataDistributionSampleMean = featureDataDistributionSampleMeanStandardDeviation.Item1;
            double featureDataDistributionSampleStandardDeviation = featureDataDistributionSampleMeanStandardDeviation.Item2;
            double invertedFeatureDataDistributionSampleStandardDeviation = 1 / featureDataDistributionSampleStandardDeviation;

            dataDistributionSamplesMean = dataDistributionSamplesMean.Add(featureDataDistributionSampleMean);
            dataDistributionSamplesInvertedStandardDeviation = dataDistributionSamplesInvertedStandardDeviation.Add(invertedFeatureDataDistributionSampleStandardDeviation);
         }

         pakiraDecisionTreeModel = pakiraDecisionTreeModel.DataDistributionSamplesStatistics(dataDistributionSamplesMean, dataDistributionSamplesInvertedStandardDeviation);

         return pakiraDecisionTreeModel;
      }

      private PakiraNode PrepareNode(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainDataCache trainDataCache)
      {
         Tuple<int, double> tuple = GetBestSplit(trainDataCache, pakiraDecisionTreeModel);

         return new PakiraNode(tuple.Item1, tuple.Item2);
      }

      private PakiraLeafResult[] PrepareLeaves(int featureIndex, double threshold, TrainDataCache trainDataCache)
      {
         PakiraLeafResult[] pakiraLeavesResult = new PakiraLeafResult[2];

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            bool theKey = (leafIndex == 0);

            pakiraLeavesResult[leafIndex] = new PakiraLeafResult();

            pakiraLeavesResult[leafIndex].slice = trainDataCache.Samples.Where(column => ThresholdCompareLessThanOrEqual(column[featureIndex], threshold) == theKey).ToImmutableList();

            pakiraLeavesResult[leafIndex].ySlice = trainDataCache.Labels.Where(
                        (trainLabel, trainLabelIndex) =>
                        {
                           double trainSample = trainDataCache.Samples[trainLabelIndex][featureIndex];

                           return ThresholdCompareLessThanOrEqual(trainSample, threshold) == theKey;
                        }
                        ).ToImmutableList();
         }

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            if (pakiraLeavesResult[leafIndex].slice.Count() > 0)
            {
               ImmutableList<double> distinctValues = pakiraLeavesResult[leafIndex].ySlice.Distinct().ToImmutableList();

               // only one answer, set leaf
               if (distinctValues.Count == 1)
               {
                  double leafValue = pakiraLeavesResult[leafIndex].ySlice.First();

                  pakiraLeavesResult[leafIndex].pakiraLeaf = new PakiraLeaf(leafValue);
               }
               // otherwise continue to build tree
               else
               {
                  pakiraLeavesResult[leafIndex].pakiraLeaf = new PakiraLeaf(distinctValues);
               }
            }
            else
            {
               // We don't have any training data for this node
               pakiraLeavesResult[leafIndex].pakiraLeaf = new PakiraLeaf(UnknownLabelValue);
            }
         }

         return pakiraLeavesResult;
      }

      private PakiraDecisionTreeModel BuildInitialTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainDataCache trainDataCache)
      {
         pakiraDecisionTreeModel.Tree.Root.ShouldBeNull();

         PakiraNode pakiraNode = PrepareNode(pakiraDecisionTreeModel, trainDataCache);
         PakiraLeafResult[] pakiraLeavesResults = PrepareLeaves(pakiraNode.Column, pakiraNode.Threshold, trainDataCache);

         pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.AddNode(pakiraNode, pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[1].pakiraLeaf));
         pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[0].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[0].slice, pakiraLeavesResults[0].ySlice));
         pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[1].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[1].slice, pakiraLeavesResults[1].ySlice));

         return pakiraDecisionTreeModel;
      }

      private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableStack<ProcessLeaf> processLeaves = ImmutableStack<ProcessLeaf>.Empty;

         foreach (KeyValuePair<PakiraNode, PakiraLeaf> pakiraNodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves().Where(pakiraNodeLeaf => (pakiraNodeLeaf.Value.LabelValues.Count() > 1)))
         {
            processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNodeLeaf.Key, pakiraNodeLeaf.Value, pakiraDecisionTreeModel.TrainDataCache(pakiraNodeLeaf.Value)));
         }

         while (!processLeaves.IsEmpty)
         {
            ProcessLeaf processLeaf;

            processLeaves = processLeaves.Pop(out processLeaf);

            TrainDataCache processNodeTrainSamplesCache = processLeaf.TrainSamplesCache;
            PakiraNode pakiraNode = PrepareNode(pakiraDecisionTreeModel, processNodeTrainSamplesCache);

            PakiraLeafResult[] pakiraLeavesResults = PrepareLeaves(pakiraNode.Column, pakiraNode.Threshold, processNodeTrainSamplesCache);

            if (pakiraLeavesResults[0].pakiraLeaf.LabelValue != UnknownLabelValue && pakiraLeavesResults[1].pakiraLeaf.LabelValue != UnknownLabelValue)
            {
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeaf(processLeaf.ParentNode, processLeaf.Leaf, PakiraTree.Empty.AddNode(pakiraNode, pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[1].pakiraLeaf)));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processLeaf.Leaf);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[0].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[0].slice, pakiraLeavesResults[0].ySlice));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[1].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[1].slice, pakiraLeavesResults[1].ySlice));

               foreach (PakiraLeafResult pakiraLeafResult in pakiraLeavesResults.Where(pakiraLeafResult => (pakiraLeafResult.pakiraLeaf.LabelValues.Count() > 1)))
               {
                  processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNode, pakiraLeafResult.pakiraLeaf, pakiraDecisionTreeModel.TrainDataCache(pakiraLeafResult.pakiraLeaf)));
               }
            }
         }

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         return pakiraDecisionTreeModel;
      }

      private Tuple<int, double> GetBestSplit(TrainDataCache processNodeTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableList<SabotenCache> extractedTrainSamplesCache = processNodeTrainSamplesCache.Samples;

         double bestScore = double.MinValue;
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         foreach (int featureIndex in pakiraDecisionTreeModel.FeatureIndices())
         {
            double featureDataDistributionSampleMean = pakiraDecisionTreeModel.DataDistributionSamplesMean[featureIndex];
            double invertedFeatureDataDistributionSampleStandardDeviation = pakiraDecisionTreeModel.DataDistributionSamplesInvertedStandardDeviation[featureIndex];

            ImmutableDictionary<double, Tuple<double, double>> minimumLabelScores = ImmutableDictionary<double, Tuple<double, double>>.Empty;
            ImmutableDictionary<double, Tuple<double, double>> maximumLabelScores = ImmutableDictionary<double, Tuple<double, double>>.Empty;

            for (int i = 0; i < extractedTrainSamplesCache.Count; i++)
            {
               SabotenCache trainSample = extractedTrainSamplesCache[i];
               double trainLabel = processNodeTrainSamplesCache.Labels[i];
               double trainSampleValue = trainSample[featureIndex];

               double score = (trainSampleValue - featureDataDistributionSampleMean) * invertedFeatureDataDistributionSampleStandardDeviation;
               Tuple<double, double> currentMinimumPotentialScoreValue;

               if (minimumLabelScores.TryGetValue(trainLabel, out currentMinimumPotentialScoreValue))
               {
                  if (score < currentMinimumPotentialScoreValue.Item1)
                  {
                     minimumLabelScores = minimumLabelScores.SetItem(trainLabel, new Tuple<double, double>(score, trainSampleValue));
                  }
                  else if (score > maximumLabelScores[trainLabel].Item1)
                  {
                     maximumLabelScores = maximumLabelScores.SetItem(trainLabel, new Tuple<double, double>(score, trainSampleValue));
                  }
               }
               else
               {
                  minimumLabelScores = minimumLabelScores.SetItem(trainLabel, new Tuple<double, double>(score, trainSampleValue));
                  maximumLabelScores = maximumLabelScores.SetItem(trainLabel, new Tuple<double, double>(score, trainSampleValue));
               }
            }

            if (minimumLabelScores.Count() == extractedTrainSamplesCache.Count())
            {
               minimumLabelScores = ImmutableDictionary<double, Tuple<double, double>>.Empty.AddRange(minimumLabelScores.Take(1));
            }

            foreach (KeyValuePair<double, Tuple<double, double>> labelMinimumPotentialScorePair in minimumLabelScores)
            {
               double minimumPotentialScore = labelMinimumPotentialScorePair.Value.Item1;

               foreach (KeyValuePair<double, Tuple<double, double>> labelMaximumPotentialScorePair in maximumLabelScores.Where((labelMaximumLabelScores) => labelMaximumLabelScores.Key != labelMinimumPotentialScorePair.Key))
               {
                  double maximumPotentialScore = labelMaximumPotentialScorePair.Value.Item1;
                  double score = Math.Abs(maximumPotentialScore - minimumPotentialScore);

                  if (score > bestScore)
                  {
                     bestScore = score;
                     bestFeature = featureIndex;
                     bestFeatureSplit = (labelMaximumPotentialScorePair.Value.Item2 + labelMinimumPotentialScorePair.Value.Item2) / 2.0;
                  }
               }
            }

            if (bestScore >= CertaintyScore)
            {
               break;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         return new Tuple<int, double>(bestFeature, bestFeatureSplit);
      }
   }
}