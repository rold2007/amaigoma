using Shouldly;
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

   public sealed record TrainData
   {
      public ImmutableList<List<double>> Samples { get; } = ImmutableList<List<double>>.Empty;
      public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

      public TrainData()
      {
      }

      public TrainData(ImmutableList<List<double>> samples, ImmutableList<double> labels)
      {
         Samples = samples;
         Labels = labels;
      }

      public TrainData AddSample(IEnumerable<double> sample, double label)
      {
         List<double> listSample = sample.ToList();

         if (!Samples.IsEmpty)
         {
            listSample.Count.ShouldBe(Samples[0].Count);
         }

         return new TrainData(Samples.Add(listSample), Labels.Add(label));
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

      public static readonly int UNKNOWN_CLASS_INDEX = -1;
      public static readonly int randomSeed = new Random().Next();
      private readonly Random RandomSource = new(randomSeed);

      public PakiraDecisionTreeGenerator()
      {
      }

      public double UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      // TODO Need to also support an interface with SabotenCache instead of TrainData because sometimes we want to train on data which is already prefetched
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
            TrainDataCache trainDataCache = new(trainSamplesCache, immutableTrainLabels);

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

         foreach (KeyValuePair<PakiraNode, PakiraLeaf> pakiraNodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves().Where(pakiraNodeLeaf => (pakiraDecisionTreeModel.TrainDataCache(pakiraNodeLeaf.Value).Labels.Distinct().Count() > 1)))
         {
            processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNodeLeaf.Key, pakiraNodeLeaf.Value, pakiraDecisionTreeModel.TrainDataCache(pakiraNodeLeaf.Value)));
         }

         while (!processLeaves.IsEmpty)
         {
            processLeaves = processLeaves.Pop(out ProcessLeaf processLeaf);

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

         double bestScore = -1.0;
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

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

            if (score > bestScore)
            {
               if ((bestFeature == -1) || (minimumValues.Item2.Count != maximumValues.Item2.Count) || (!minimumValues.Item2.SymmetricExcept(maximumValues.Item2).IsEmpty))
               {
                  bestScore = score;
                  bestFeature = featureIndex;
                  bestFeatureSplit = minimumValues.Item1 + score / 2.0;

                  // UNDONE Need to investigate why this break; is causing fails in the tests
                  //break;
               }
            }
         }

         bestFeature.ShouldBeGreaterThanOrEqualTo(0);

         return new Tuple<int, double>(bestFeature, bestFeatureSplit);
      }
   }
}