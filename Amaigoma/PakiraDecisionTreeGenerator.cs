using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
      public static readonly int UNKNOWN_CLASS_INDEX = -1; // ncrunch: no coverage
      public readonly int randomSeed = new Random().Next(); // ncrunch: no coverage
      private readonly Random RandomSource;
      private readonly Func<IReadOnlyList<int>, TanukiETL, (int featureIndex, double splitThreshold)> BestSplit;

      public PakiraDecisionTreeGenerator()
      {
         RandomSource = new(randomSeed);
         BestSplit = GetBestSplit;
      }

      public PakiraDecisionTreeGenerator(Func<IReadOnlyList<int>, TanukiETL, (int featureIndex, double splitThreshold)> bestSplit)
      {
         BestSplit = bestSplit;
      }

      public int UnknownLabelValue { get; private set; } = UNKNOWN_CLASS_INDEX;

      public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiETL tanukiETL)
      {
         if (!pakiraDecisionTreeModel.Tree.Nodes().Any())
         {
            // Build initial tree with only one leaf
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(new PakiraTree(UnknownLabelValue));
         }

         PakiraTreeWalker pakiraTreeWalker = new(pakiraDecisionTreeModel.Tree, tanukiETL);

         foreach (int id in ids)
         {
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(pakiraTreeWalker.PredictLeaf(id).id, id);
         }

         pakiraDecisionTreeModel = BuildTree(pakiraDecisionTreeModel, tanukiETL);

         return pakiraDecisionTreeModel;
      }

      private static bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
      {
         return inputValue <= threshold;
      }

      private int PrepareTreeLeavelabel(IEnumerable<int> dataSamples, TanukiETL tanukiETL)
      {
         if (dataSamples.Any())
         {
            int labelValue = tanukiETL.TanukiLabelExtractor(dataSamples.First());

            if (!dataSamples.Any(x => tanukiETL.TanukiLabelExtractor(x) != labelValue))
            {
               return labelValue;
            }
         }

         return UnknownLabelValue;
      }

      private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel, TanukiETL tanukiETL)
      {
         ImmutableList<int> retrainLeaves = [];

         // Identify all the leaves to retrain
         foreach (int leaf in pakiraDecisionTreeModel.Tree.Leaves().Where(leaf =>
         {
            foreach (int id in pakiraDecisionTreeModel.DataSamples(leaf.id))
            {
               if (tanukiETL.TanukiLabelExtractor(id) != leaf.labelValue)
               {
                  return true;
               }
            }

            return false;
         }).Select(leaf => leaf.id))
         {
            retrainLeaves = retrainLeaves.Add(leaf);
         }

         while (!retrainLeaves.IsEmpty)
         {
            int leafId = retrainLeaves[0];
            retrainLeaves = retrainLeaves.RemoveAt(0);
            ImmutableList<int> ids = pakiraDecisionTreeModel.DataSamples(leafId);

            (int featureIndex, double splitThreshold) = BestSplit(ids, tanukiETL);
            ILookup<bool, int> updatedDataSamples = pakiraDecisionTreeModel.DataSamples(leafId).ToLookup(id => ThresholdCompareLessThanOrEqual(tanukiETL.TanukiDataTransformer(id, featureIndex), splitThreshold));
            int leftLabel = PrepareTreeLeavelabel(updatedDataSamples[true], tanukiETL);
            int rightLabel = PrepareTreeLeavelabel(updatedDataSamples[false], tanukiETL);

            (PakiraTree tree, int leftLeafId, int rightLeafId) = pakiraDecisionTreeModel.Tree.ReplaceLeaf(leafId, featureIndex, splitThreshold, leftLabel, rightLabel);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(tree);

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataSample(leafId);

            // TODO Add clear error message for each Shouldly call
            updatedDataSamples[true].ShouldNotBeEmpty();
            updatedDataSamples[key: false].ShouldNotBeEmpty();

            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(leftLeafId, updatedDataSamples[true]);
            pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataSample(rightLeafId, updatedDataSamples[false]);

            if (leftLabel == UnknownLabelValue)
            {
               retrainLeaves = retrainLeaves.Add(leftLeafId);
            }

            if (rightLabel == UnknownLabelValue)
            {
               retrainLeaves = retrainLeaves.Add(rightLeafId);
            }
         }

         return pakiraDecisionTreeModel;
      }

      private (int featureIndex, double splitThreshold) GetBestSplit(IReadOnlyList<int> ids, TanukiETL tanukiETL)
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

         return (bestFeature, bestFeatureSplit);
      }
   }
}