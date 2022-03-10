using MathNet.Numerics.Distributions;
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
      static public readonly int UNKNOWN_CLASS_INDEX = -1;
      static public readonly int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
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

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainData trainData)
      {
         ImmutableList<double> trainSample = trainData.Samples[0];
         int featureCount = trainSample.Count();
         bool buildNewTree = true;
         ImmutableList<SabotenCache> trainSamplesCache = pakiraDecisionTreeModel.PrefetchAll(trainData.Samples.Select(d => new SabotenCache(d)));
         ImmutableList<double> immutableTrainLabels = trainData.Labels;

         if (pakiraDecisionTreeModel.Tree.Root == null)
         {
            PakiraLeaf initialLeaf = new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(PakiraTree.Empty.AddLeaf(initialLeaf));

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
         }

         for (int trainSampleIndex = 0; trainSampleIndex < trainSamplesCache.Count; trainSampleIndex++)
         {
            PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictNode(trainSamplesCache[trainSampleIndex]);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraDecisionTreePredictionResult.PakiraLeaf, new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(pakiraDecisionTreePredictionResult.SabotenCache), ImmutableList<double>.Empty.Add(immutableTrainLabels[trainSampleIndex])));
         }

         ImmutableList<SabotenCache> randomSamplesCache = ImmutableList<SabotenCache>.Empty;

         while (buildNewTree)
         {
            pakiraDecisionTreeModel = BuildTree(pakiraDecisionTreeModel);

            buildNewTree = pakiraDecisionTreeModel.Tree.GetNodes().Count() <= 1;
         }

         return pakiraDecisionTreeModel;
      }

      static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private struct ProcessNode
      {
         public ProcessNode(IPakiraNode node, TrainDataCache trainSamplesCache, ImmutableList<SabotenCache> dataDistributionSamplesCache)
         {
            Node = node;
            TrainSamplesCache = trainSamplesCache;
            DataDistributionSamplesCache = dataDistributionSamplesCache;
         }

         public IPakiraNode Node;
         public TrainDataCache TrainSamplesCache;
         public ImmutableList<SabotenCache> DataDistributionSamplesCache;
      };

      private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableStack<ProcessNode> processNodes = ImmutableStack<ProcessNode>.Empty;
         PakiraTree pakiraTree = pakiraDecisionTreeModel.Tree;

         {
            foreach (PakiraLeaf pakiraLeaf in pakiraDecisionTreeModel.Tree.GetNodes().Where(pakiraNode => (pakiraTree.IsLeaf(pakiraNode) == true) && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX).Cast<PakiraLeaf>())
            {
               processNodes = processNodes.Push(new ProcessNode(pakiraLeaf, pakiraDecisionTreeModel.TrainDataCache(pakiraLeaf), ImmutableList<SabotenCache>.Empty));
            }
         }

         PakiraLeaf[] leaves = new PakiraLeaf[2];
         ImmutableList<SabotenCache>[] slice = new ImmutableList<SabotenCache>[2];
         ImmutableList<double>[] ySlice = new ImmutableList<double>[2];

         while (!processNodes.IsEmpty)
         {
            ProcessNode processNode;

            processNodes = processNodes.Pop(out processNode);

            TrainDataCache processNodeTrainSamplesCache = processNode.TrainSamplesCache;

            Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> tuple = GetBestSplit(processNodeTrainSamplesCache, pakiraDecisionTreeModel);
            int bestFeatureIndex = tuple.Item1;
            double threshold = tuple.Item2;
            ImmutableList<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item3;
            ImmutableList<SabotenCache> bestSplitTrainSamplesCache = tuple.Item4;

            PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);

            for (int leafIndex = 0; leafIndex < 2; leafIndex++)
            {
               bool theKey = (leafIndex == 0);

               slice[leafIndex] = bestSplitTrainSamplesCache.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

               ySlice[leafIndex] = processNodeTrainSamplesCache.Labels.Where(
                           (trainLabel, trainLabelIndex) =>
                           {
                              double trainSample = bestSplitTrainSamplesCache[trainLabelIndex][bestFeatureIndex];

                              return ThresholdCompareLessThanOrEqual(trainSample, threshold) == theKey;
                           }
                           ).ToImmutableList();
            }

            for (int leafIndex = 0; leafIndex < 2; leafIndex++)
            {
               if (slice[leafIndex].Count() > 0)
               {
                  int distinctLabelsCount = ySlice[leafIndex].Distinct().Count();

                  // only one answer, set leaf
                  if (distinctLabelsCount == 1)
                  {
                     double leafValue = ySlice[leafIndex].First();

                     leaves[leafIndex] = new PakiraLeaf(leafValue);
                  }
                  // otherwise continue to build tree
                  else
                  {
                     leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);

                     processNodes = processNodes.Push(new ProcessNode(leaves[leafIndex], new TrainDataCache(slice[leafIndex], ySlice[leafIndex]), ImmutableList<SabotenCache>.Empty));
                  }
               }
               else
               {
                  // We don't have any training data for this node
                  leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
               }
            }

            pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddNode(node, leaves[0], leaves[1]));
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processNode.Node as PakiraLeaf);
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[0], new TrainDataCache(slice[0], ySlice[0]));
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[1], new TrainDataCache(slice[1], ySlice[1]));
         }

         return pakiraDecisionTreeModel.UpdateTree(pakiraTree);
      }

      private Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> GetBestSplit(TrainDataCache processNodeTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
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

               trainSampleValue.ShouldBeGreaterThanOrEqualTo(0.0);
               trainSampleValue.ShouldBeLessThanOrEqualTo(255.0);

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

         return new Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>>(bestFeature, bestFeatureSplit, ImmutableList<SabotenCache>.Empty, extractedTrainSamplesCache);
      }
   }
}