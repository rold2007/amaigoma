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

   public sealed record PakiraDecisionTreeGenerator // ncrunch: no coverage
   {
      private sealed record PakiraLeafResult // ncrunch: no coverage
      {
         public PakiraLeaf pakiraLeaf;
         public ImmutableList<int> ids;
      }

      public static readonly int UNKNOWN_CLASS_INDEX = -1; // ncrunch: no coverage
      public readonly int randomSeed = new Random().Next(); // ncrunch: no coverage
      private readonly Random RandomSource;

      public PakiraDecisionTreeGenerator()
      {
         RandomSource = new(randomSeed);
      }

      public int UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         if (pakiraDecisionTreeModel.Tree.Root == null)
         {
            pakiraDecisionTreeModel = BuildInitialTree(pakiraDecisionTreeModel, ids, tanukiETL);
         }
         else
         {
            PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiETL);

            foreach (int id in ids)
            {
               // TODO Create a new PredictLeaf() which doesn't call Prefetch() to optimize this slightly
               PakiraLeaf pakiraLeafResult = pakiraTreeWalker.PredictLeaf(id);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraLeafResult, ImmutableList<int>.Empty.Add(id));
            }
         }

         return BuildTree(pakiraDecisionTreeModel, tanukiETL);
      }

      private static bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private struct ProcessLeaf
      {
         public ProcessLeaf(PakiraNode parentNode, PakiraLeaf leaf, ImmutableList<int> trainSamplesCache)
         {
            ParentNode = parentNode;
            Leaf = leaf;
            TrainSamplesCache = trainSamplesCache;
         }

         public PakiraNode ParentNode;
         public PakiraLeaf Leaf;
         public ImmutableList<int> TrainSamplesCache;
      };

      private PakiraNode PrepareNode(IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         Tuple<int, double> tuple = GetBestSplit(ids, tanukiETL);

         return new PakiraNode(tuple.Item1, tuple.Item2);
      }

      private PakiraLeafResult[] PrepareLeaves(int featureIndex, double threshold, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         PakiraLeafResult[] pakiraLeavesResult = new PakiraLeafResult[2];

         foreach (int id in ids)
         {
            tanukiETL.TanukiSabotenCacheExtractor(id).PrefetchLoad(tanukiETL, id, featureIndex);
         }

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            bool theKey = (leafIndex == 0);

            pakiraLeavesResult[leafIndex] = new PakiraLeafResult();

            pakiraLeavesResult[leafIndex].ids = ImmutableList<int>.Empty.AddRange(ids.Where(id =>
                                    {
                                       return ThresholdCompareLessThanOrEqual(tanukiETL.TanukiSabotenCacheExtractor(id)[featureIndex], threshold) == theKey;
                                    }));
         }

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            if (pakiraLeavesResult[leafIndex].ids.Count() > 0)
            {
               ImmutableHashSet<int> labels = ImmutableHashSet.CreateRange(pakiraLeavesResult[leafIndex].ids.Select(id =>
                        {
                           return tanukiETL.TanukiLabelExtractor(id);
                        }));

               pakiraLeavesResult[leafIndex].pakiraLeaf = new PakiraLeaf(labels);
            }
            else
            {
               // We don't have any training data for this node
               pakiraLeavesResult[leafIndex].pakiraLeaf = new PakiraLeaf(UnknownLabelValue);
            }
         }

         return pakiraLeavesResult;
      }

      private PakiraDecisionTreeModel BuildInitialTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         pakiraDecisionTreeModel.Tree.Root.ShouldBeNull();

         PakiraNode pakiraNode = PrepareNode(ids, tanukiETL);
         PakiraLeafResult[] pakiraLeavesResults = PrepareLeaves(pakiraNode.Column, pakiraNode.Threshold, ids, tanukiETL);

         pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.AddNode(pakiraNode, pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[1].pakiraLeaf));
         pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[0].ids);
         pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraLeavesResults[1].pakiraLeaf, pakiraLeavesResults[1].ids);

         return pakiraDecisionTreeModel;
      }

      private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, TanukiETL tanukiETL)
      {
         ImmutableStack<ProcessLeaf> processLeaves = ImmutableStack<ProcessLeaf>.Empty;
         ImmutableHashSet<PakiraLeaf> multipleLabelsLeaves = ImmutableHashSet<PakiraLeaf>.Empty;

         // Identify all the leaves to retrain
         foreach (KeyValuePair<PakiraNode, PakiraLeaf> pakiraNodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves().Where(pakiraNodeLeaf =>
         {
            // TODO This logic can certainly be simplified without using a HashSet since we exit early as soon as we have 2 items
            if (pakiraNodeLeaf.Value.LabelValues.Count() == 1)
            {
               ImmutableList<int> ids = pakiraDecisionTreeModel.DataSamples(pakiraNodeLeaf.Value);

               if (ids.Count > 0)
               {
                  ImmutableHashSet<int> uniqueLabels = ImmutableHashSet<int>.Empty;

                  foreach (int id in ids)
                  {
                     uniqueLabels = uniqueLabels.Add(tanukiETL.TanukiLabelExtractor(id));

                     if (uniqueLabels.Count > 1)
                     {
                        multipleLabelsLeaves = multipleLabelsLeaves.Add(pakiraNodeLeaf.Value);
                        return true;
                     }
                  }

                  uniqueLabels.Count().ShouldBe(1);
                  return uniqueLabels.First() != pakiraNodeLeaf.Value.LabelValues.First();
               }
            }
            else
            {
               return true;
            }

            return false;
         }))
         {
            processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNodeLeaf.Key, pakiraNodeLeaf.Value, pakiraDecisionTreeModel.DataSamples(pakiraNodeLeaf.Value)));
         }

         while (!processLeaves.IsEmpty)
         {
            processLeaves = processLeaves.Pop(out ProcessLeaf processLeaf);

            ImmutableList<int> ids = pakiraDecisionTreeModel.DataSamples(processLeaf.Leaf);

            if (processLeaf.Leaf.LabelValues.First() == UnknownLabelValue && !multipleLabelsLeaves.Contains(processLeaf.Leaf))
            {
               ImmutableHashSet<int> labels = ImmutableHashSet.CreateRange(ids.Select(id => tanukiETL.TanukiLabelExtractor(id)));
               PakiraLeaf updatedLeaf = new PakiraLeaf(labels);

               pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeaf(processLeaf.ParentNode, processLeaf.Leaf, updatedLeaf));
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataSample(processLeaf.Leaf);
               pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(updatedLeaf, ids);
            }
            else
            {
               PakiraNode pakiraNode = PrepareNode(ids, tanukiETL);

               PakiraLeafResult[] pakiraLeavesResults = PrepareLeaves(pakiraNode.Column, pakiraNode.Threshold, ids, tanukiETL);

               if ((pakiraLeavesResults[0].pakiraLeaf.LabelValues.First() != UnknownLabelValue) && pakiraLeavesResults[1].pakiraLeaf.LabelValues.First() != UnknownLabelValue)
               {
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(pakiraDecisionTreeModel.Tree.ReplaceLeaf(processLeaf.ParentNode, processLeaf.Leaf, new PakiraTree().AddNode(pakiraNode, pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[1].pakiraLeaf)));
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataSample(processLeaf.Leaf);

                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraLeavesResults[0].pakiraLeaf, pakiraLeavesResults[0].ids);
                  pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraLeavesResults[1].pakiraLeaf, pakiraLeavesResults[1].ids);

                  foreach (PakiraLeafResult pakiraLeafResult in pakiraLeavesResults.Where(pakiraLeafResult => (pakiraLeafResult.pakiraLeaf.LabelValues.Count() > 1)))
                  {
                     processLeaves = processLeaves.Push(new ProcessLeaf(pakiraNode, pakiraLeafResult.pakiraLeaf, pakiraDecisionTreeModel.DataSamples(pakiraLeafResult.pakiraLeaf)));
                  }
               }
            }
         }

         pakiraDecisionTreeModel.Tree.Root.ShouldNotBeNull();

         return pakiraDecisionTreeModel;
      }

      private Tuple<int, double> GetBestSplit(IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         int bestFeature = -1;
         double bestFeatureSplit = 128.0;

         // TODO Instead of shuffling randomly, it might make more sense to simply cycle through all available feature indices sequentially. or all data transformers sequentially
         // and then randomly within each transformer.
         IEnumerable<int> randomFeatureIndices = Enumerable.Range(0, tanukiETL.TanukiFeatureCount).Shuffle(RandomSource);

         foreach (int featureIndex in randomFeatureIndices)
         {
            Tuple<double, ImmutableHashSet<double>> minimumValues = new Tuple<double, ImmutableHashSet<double>>(double.MaxValue, ImmutableHashSet<double>.Empty);
            Tuple<double, ImmutableHashSet<double>> maximumValues = new Tuple<double, ImmutableHashSet<double>>(double.MinValue, ImmutableHashSet<double>.Empty);

            foreach (int id in ids)
            {
               SabotenCache sabotenCache = tanukiETL.TanukiSabotenCacheExtractor(id).PrefetchLoad(tanukiETL, id, featureIndex);

               double dataSampleValue = sabotenCache[featureIndex];

               if (dataSampleValue <= minimumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (dataSampleValue < minimumValues.Item1) ? ImmutableHashSet<double>.Empty : minimumValues.Item2;

                  minimumValues = new Tuple<double, ImmutableHashSet<double>>(dataSampleValue, labels.Add(tanukiETL.TanukiLabelExtractor(id)));
               }

               if (dataSampleValue >= maximumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (dataSampleValue > maximumValues.Item1) ? ImmutableHashSet<double>.Empty : maximumValues.Item2;

                  maximumValues = new Tuple<double, ImmutableHashSet<double>>(dataSampleValue, labels.Add(tanukiETL.TanukiLabelExtractor(id)));
               }
            }

            double score = maximumValues.Item1 - minimumValues.Item1;

            // Accept quickly any feature which splits some data in two
            bool quickAccept = (score > 0) && ((minimumValues.Item2.Count != maximumValues.Item2.Count) || (!minimumValues.Item2.SymmetricExcept(maximumValues.Item2).IsEmpty));
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