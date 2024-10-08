﻿using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public static class IEnumerableExtensions
   {
      // TODO This is totally inefficient. Calling ToArray on the enumerable makes us lose all the benefits of an enumerable. This method could be transferred to PakiraDecisionTreeModel in order to have accces to the feature indices count. It may even replace FeatureIndices() altogether
      // Obtained from https://stackoverflow.com/a/1287572/263228
      public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
      {
         T[] elements = source.ToArray();
         for (int i = elements.Length - 1; i >= 0; i--)
         {
            // Swap element "i" with a random earlier element it (or itself)
            // ... except we don't really need to swap it fully, as we can
            // return it immediately, and afterwards it's irrelevant.
            int swapIndex = rng.Next(i + 1);
            yield return elements[swapIndex];
            elements[swapIndex] = elements[i];
         }
      }
   }

   // TODO Rename this to remove the 'Train'
   public sealed record TrainDataCache // ncrunch: no coverage
   {
      public ImmutableList<SabotenCache> Samples { get; } = ImmutableList<SabotenCache>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainDataCache()
      {
      }

      public TrainDataCache(SabotenCache sample, double label)
      {
         Samples = Samples.Add(sample);
         Labels = Labels.Add(label);
      }

      public TrainDataCache(ImmutableList<SabotenCache> samples, ImmutableList<double> labels)
      {
         labels.Count.ShouldBe(samples.Count);

         Samples = samples;
         Labels = labels;
      }

      public TrainDataCache AddSample(IEnumerable<double> data, double label)
      {
         return AddSamples(new TrainDataCache(new SabotenCache(data), label));
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

   public sealed record PakiraDecisionTreeGenerator // ncrunch: no coverage
   {
      private sealed record PakiraLeafResult // ncrunch: no coverage
      {
         public PakiraLeaf pakiraLeaf;
         public ImmutableList<SabotenCache> slice;
         public ImmutableList<double> ySlice;
         public ImmutableList<Guid> guid;
      }

      public static readonly int UNKNOWN_CLASS_INDEX = -1; // ncrunch: no coverage
      public static readonly int randomSeed = new Random().Next(); // ncrunch: no coverage
      private readonly Random RandomSource = new(randomSeed);

      public PakiraDecisionTreeGenerator()
      {
      }

      public double UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainDataCache trainDataCache)
      {
         if (pakiraDecisionTreeModel.Tree.Root == null)
         {
            pakiraDecisionTreeModel = BuildInitialTree(pakiraDecisionTreeModel, trainDataCache);
         }
         else
         {
            for (int trainSampleIndex = 0; trainSampleIndex < trainDataCache.Samples.Count; trainSampleIndex++)
            {
               // TODO Create a new PredictLeaf() which doesn't call Prefetch() to optimize this slightly
               PakiraDecisionTreePredictionResult pakiraDecisionTreePredictionResult = pakiraDecisionTreeModel.PredictLeaf(trainDataCache.Samples[trainSampleIndex]);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraDecisionTreePredictionResult.PakiraLeaf, new TrainDataCache(pakiraDecisionTreePredictionResult.SabotenCache, trainDataCache.Labels[trainSampleIndex]));
            }
         }

         return BuildTree(pakiraDecisionTreeModel);
      }

      private static bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
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

         // Identify all the leaves to retrain
         foreach (KeyValuePair<PakiraNode, PakiraLeaf> pakiraNodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves().Where(pakiraNodeLeaf =>
         {
            ImmutableList<double> labels = pakiraDecisionTreeModel.TrainDataCache(pakiraNodeLeaf.Value).Labels;

            if (labels.Count == 1)
            {
               pakiraNodeLeaf.Value.LabelValues.Count().ShouldBe(1);
               return labels[0] != pakiraNodeLeaf.Value.LabelValue;
            }
            else
            {
               return (labels.Distinct().Count() > 1);
            }
         }))
         {
            processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNodeLeaf.Key, pakiraNodeLeaf.Value, pakiraDecisionTreeModel.TrainDataCache(pakiraNodeLeaf.Value)));
         }

         while (!processLeaves.IsEmpty)
         {
            processLeaves = processLeaves.Pop(out ProcessLeaf processLeaf);

            TrainDataCache processNodeTrainSamplesCache = processLeaf.TrainSamplesCache;

            if (processLeaf.Leaf.LabelValue == UnknownLabelValue && processNodeTrainSamplesCache.Labels.Distinct().Count() == 1)
            {
               PakiraLeaf updatedLeaf = new PakiraLeaf(processNodeTrainSamplesCache.Labels);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeaf(processLeaf.ParentNode, processLeaf.Leaf, updatedLeaf));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processLeaf.Leaf);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(updatedLeaf, processNodeTrainSamplesCache);
            }
            else
            {
               PakiraNode pakiraNode = PrepareNode(pakiraDecisionTreeModel, processNodeTrainSamplesCache);

               PakiraLeafResult[] pakiraLeavesResults = PrepareLeaves(pakiraNode.Column, pakiraNode.Threshold, processNodeTrainSamplesCache);

               if (pakiraLeavesResults[0].pakiraLeaf.LabelValue != UnknownLabelValue && pakiraLeavesResults[1].pakiraLeaf.LabelValue != UnknownLabelValue)
               {
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeaf(processLeaf.ParentNode, processLeaf.Leaf, new PakiraTree().AddNode(pakiraNode, pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[1].pakiraLeaf)));
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processLeaf.Leaf);
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[0].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[0].slice, pakiraLeavesResults[0].ySlice));
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeavesResults[1].pakiraLeaf, new TrainDataCache(pakiraLeavesResults[1].slice, pakiraLeavesResults[1].ySlice));

                  foreach (PakiraLeafResult pakiraLeafResult in pakiraLeavesResults.Where(pakiraLeafResult => (pakiraLeafResult.pakiraLeaf.LabelValues.Count() > 1)))
                  {
                     processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNode, pakiraLeafResult.pakiraLeaf, pakiraDecisionTreeModel.TrainDataCache(pakiraLeafResult.pakiraLeaf)));
                  }
               }
            }
         }

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         return pakiraDecisionTreeModel;
      }

      private Tuple<int, double> GetBestSplit(TrainDataCache processNodeTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
      {
         ImmutableList<SabotenCache> extractedTrainSamplesCache = processNodeTrainSamplesCache.Samples;

         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO Instead of shuffling randomly, it might make more sense to simply cycle through all available feature indices sequentially
         IEnumerable<int> randomFeatureIndices = pakiraDecisionTreeModel.FeatureIndices().Shuffle(RandomSource);

         foreach (int featureIndex in randomFeatureIndices)
         {
            Tuple<double, ImmutableHashSet<double>> minimumValues = new Tuple<double, ImmutableHashSet<double>>(double.MaxValue, ImmutableHashSet<double>.Empty);
            Tuple<double, ImmutableHashSet<double>> maximumValues = new Tuple<double, ImmutableHashSet<double>>(double.MinValue, ImmutableHashSet<double>.Empty);

            for (int i = 0; i < extractedTrainSamplesCache.Count; i++)
            {
               SabotenCache trainSample = extractedTrainSamplesCache[i];
               double trainLabel = processNodeTrainSamplesCache.Labels[i];
               double trainSampleValue = trainSample[featureIndex];

               if (trainSampleValue <= minimumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (trainSampleValue < minimumValues.Item1) ? ImmutableHashSet<double>.Empty : minimumValues.Item2;

                  minimumValues = new Tuple<double, ImmutableHashSet<double>>(trainSampleValue, labels.Add(trainLabel));
               }

               if (trainSampleValue >= maximumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (trainSampleValue > maximumValues.Item1) ? ImmutableHashSet<double>.Empty : maximumValues.Item2;

                  maximumValues = new Tuple<double, ImmutableHashSet<double>>(trainSampleValue, labels.Add(trainLabel));
               }
            }

            double score = maximumValues.Item1 - minimumValues.Item1;

            // Accept quickly any feature which splits some data in two
            bool quickAccept = ((score > 0) && ((minimumValues.Item2.Count != maximumValues.Item2.Count) || (!minimumValues.Item2.SymmetricExcept(maximumValues.Item2).IsEmpty)));
            bool updateBestFeature = (bestFeature == -1) || quickAccept;

            if (updateBestFeature)
            {
               bestFeature = featureIndex;
               bestFeatureSplit = minimumValues.Item1 + score / 2.0;
            }

            if (quickAccept)
            {
               break;
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         return new Tuple<int, double>(bestFeature, bestFeatureSplit);
      }
   }
}