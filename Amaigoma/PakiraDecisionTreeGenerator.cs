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
         T[] elements = [.. source];
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
      private Func<IEnumerable<int>, TanukiETL, Tuple<int, double>> BestSplit;

      public PakiraDecisionTreeGenerator()
      {
         RandomSource = new(randomSeed);
         BestSplit = GetBestSplit;
      }

      public PakiraDecisionTreeGenerator(Func<IEnumerable<int>, TanukiETL, Tuple<int, double>> bestSplit)
      {
         BestSplit = bestSplit;
      }

      public int UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         PakiraDecisionTreeModel returnPakiraDecisionTreeModel = pakiraDecisionTreeModel;

         if (returnPakiraDecisionTreeModel.Tree.Root == null)
         {
            returnPakiraDecisionTreeModel = BuildInitialTree(returnPakiraDecisionTreeModel, ids, tanukiETL);
         }
         else
         {
            PakiraTreeWalker pakiraTreeWalker = new(returnPakiraDecisionTreeModel.Tree, tanukiETL);

            foreach (int id in ids)
            {
               PakiraLeaf pakiraLeafResult = pakiraTreeWalker.PredictLeaf(id);

               returnPakiraDecisionTreeModel = returnPakiraDecisionTreeModel.AddDataSample(pakiraLeafResult, [id]);
            }
         }

         returnPakiraDecisionTreeModel = BuildTree(returnPakiraDecisionTreeModel, tanukiETL);

         return returnPakiraDecisionTreeModel;
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
         Tuple<int, double> tuple = BestSplit(ids, tanukiETL);

         return new PakiraNode(tuple.Item1, tuple.Item2);
      }

      private PakiraLeafResult[] PrepareLeaves(int featureIndex, double threshold, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         PakiraLeafResult[] pakiraLeavesResult = new PakiraLeafResult[2];

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            bool theKey = (leafIndex == 0);

            pakiraLeavesResult[leafIndex] = new PakiraLeafResult
            {
               ids = [.. ids.Where(id =>
                                       {
                                          return ThresholdCompareLessThanOrEqual(tanukiETL.TanukiDataTransformer(id, featureIndex), threshold) == theKey;
                                       })]
            };
         }

         for (int leafIndex = 0; leafIndex < 2; leafIndex++)
         {
            if (pakiraLeavesResult[leafIndex].ids.Count > 0)
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
         ImmutableStack<ProcessLeaf> processLeaves = [];
         ImmutableHashSet<PakiraLeaf> multipleLabelsLeaves = [];

         // Identify all the leaves to retrain
         foreach (KeyValuePair<PakiraNode, PakiraLeaf> pakiraNodeLeaf in pakiraDecisionTreeModel.Tree.GetLeaves().Where(pakiraNodeLeaf =>
         {
            if (pakiraNodeLeaf.Value.LabelValues.Count() != 1)
            {
               return true;
            }

            int uniqueLabel = 0;
            bool uniqueLabelFound = false;

            foreach (int id in pakiraDecisionTreeModel.DataSamples(pakiraNodeLeaf.Value))
            {
               int currentLabel = tanukiETL.TanukiLabelExtractor(id);

                  if (uniqueLabelFound && uniqueLabel != currentLabel)
                  {
                     multipleLabelsLeaves = multipleLabelsLeaves.Add(pakiraNodeLeaf.Value);
                     return true;
                  }

                  uniqueLabel = currentLabel;
                  uniqueLabelFound = true;
            }

            if (uniqueLabelFound)
            {
               return uniqueLabel != pakiraNodeLeaf.Value.LabelValues.First();
            }
            else
            {
               return false;
            }
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
               ImmutableHashSet<int> labels = [.. ids.Select(id => tanukiETL.TanukiLabelExtractor(id))];
               PakiraLeaf updatedLeaf = new(labels);

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
         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         IEnumerable<int> randomFeatureIndices = Enumerable.Range(0, tanukiETL.TanukiFeatureCount).Shuffle(RandomSource);

         foreach (int featureIndex in randomFeatureIndices)
         {
            Tuple<double, ImmutableHashSet<double>> minimumValues = new(double.MaxValue, []);
            Tuple<double, ImmutableHashSet<double>> maximumValues = new(double.MinValue, []);

            foreach (int id in ids)
            {
               double dataSampleValue = tanukiETL.TanukiDataTransformer(id, featureIndex);

               if (dataSampleValue <= minimumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (dataSampleValue < minimumValues.Item1) ? [] : minimumValues.Item2;

                  minimumValues = new Tuple<double, ImmutableHashSet<double>>(dataSampleValue, labels.Add(tanukiETL.TanukiLabelExtractor(id)));
               }

               if (dataSampleValue >= maximumValues.Item1)
               {
                  ImmutableHashSet<double> labels = (dataSampleValue > maximumValues.Item1) ? [] : maximumValues.Item2;

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