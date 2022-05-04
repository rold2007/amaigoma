using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = System.Converter<IEnumerable<double>, IEnumerable<double>>;

   public sealed record PakiraDecisionTreePredictionResult
   {
      public PakiraLeaf PakiraLeaf { get; }
      public SabotenCache SabotenCache { get; }

      public PakiraDecisionTreePredictionResult(PakiraLeaf pakiraLeaf, SabotenCache sabotenCache)
      {
         PakiraLeaf = pakiraLeaf;
         SabotenCache = sabotenCache;
      }
   }

   /// <summary>A data Model for the decision tree.</summary>
   public sealed record PakiraDecisionTreeModel
   {
      static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraTree Tree { get; } = PakiraTree.Empty;

      private TanukiTransformers TanukiTransformers { get; }

      private ImmutableDictionary<PakiraLeaf, TrainDataCache> LeafTrainDataCache { get; } = ImmutableDictionary<PakiraLeaf, TrainDataCache>.Empty;

      public ImmutableList<double> DataDistributionSamplesMean { get; } = ImmutableList<double>.Empty;
      public ImmutableList<double> DataDistributionSamplesInvertedStandardDeviation { get; } = ImmutableList<double>.Empty;

      public PakiraDecisionTreeModel(IEnumerable<double> dataSample) : this(PakiraTree.Empty, new DataTransformer(DefaultDataTransformer.ConvertAll), dataSample)
      {
      }

      public PakiraDecisionTreeModel(DataTransformer dataTransformers, IEnumerable<double> dataSample) : this(PakiraTree.Empty, dataTransformers, dataSample)
      {
      }

      public PakiraDecisionTreeModel(PakiraTree tree, DataTransformer dataTransformers, IEnumerable<double> dataSample)
      {
         Tree = tree;
         TanukiTransformers = new TanukiTransformers(dataTransformers, dataSample);
      }

      private PakiraDecisionTreeModel(PakiraTree tree, TanukiTransformers tanukiTransformers, ImmutableDictionary<PakiraLeaf, TrainDataCache> leafTrainDataCache, ImmutableList<double> dataDistributionSamplesMean, ImmutableList<double> dataDistributionSamplesInvertedStandardDeviation)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
         LeafTrainDataCache = leafTrainDataCache;
         DataDistributionSamplesMean = dataDistributionSamplesMean;
         DataDistributionSamplesInvertedStandardDeviation = dataDistributionSamplesInvertedStandardDeviation;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, TanukiTransformers, LeafTrainDataCache, DataDistributionSamplesMean, DataDistributionSamplesInvertedStandardDeviation);
      }

      public PakiraDecisionTreeModel AddTrainDataCache(PakiraLeaf pakiraLeaf, TrainDataCache trainDataCache)
      {
         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out TrainDataCache leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.AddSamples(trainDataCache)), DataDistributionSamplesMean, DataDistributionSamplesInvertedStandardDeviation);
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.Add(pakiraLeaf, trainDataCache), DataDistributionSamplesMean, DataDistributionSamplesInvertedStandardDeviation);
         }
      }

      public TrainDataCache TrainDataCache(PakiraLeaf pakiraLeaf)
      {
         return LeafTrainDataCache[pakiraLeaf];
      }

      public PakiraDecisionTreeModel RemoveTrainDataCache(PakiraLeaf pakiraLeaf)
      {
         LeafTrainDataCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.Remove(pakiraLeaf), DataDistributionSamplesMean, DataDistributionSamplesInvertedStandardDeviation);
      }

      public PakiraDecisionTreeModel DataDistributionSamplesStatistics(ImmutableList<double> dataDistributionSamplesMean, ImmutableList<double> dataDistributionSamplesInvertedStandardDeviation)
      {
         dataDistributionSamplesMean.ShouldNotBeEmpty();
         dataDistributionSamplesInvertedStandardDeviation.ShouldNotBeEmpty();
         DataDistributionSamplesMean.ShouldBeEmpty("Not sure yet if this case should be handled or not.");
         DataDistributionSamplesInvertedStandardDeviation.ShouldBeEmpty("Not sure yet if this case should be handled or not.");
         dataDistributionSamplesMean.Count.ShouldBe(TanukiTransformers.TotalOutputSamples);
         dataDistributionSamplesInvertedStandardDeviation.Count.ShouldBe(TanukiTransformers.TotalOutputSamples);

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache, dataDistributionSamplesMean, dataDistributionSamplesInvertedStandardDeviation);
      }

      public IEnumerable<int> FeatureIndices()
      {
         return Enumerable.Range(0, TanukiTransformers.TotalOutputSamples);
      }

      public ImmutableList<SabotenCache> PrefetchAll(IEnumerable<SabotenCache> dataSamples)
      {
         return dataSamples.PrefetchAll(TanukiTransformers).ToImmutableList();
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraLeaf PredictLeaf(IEnumerable<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree).Item1;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraDecisionTreePredictionResult PredictLeaf(SabotenCache sabotenCache)
      {
         Tuple<PakiraLeaf, SabotenCache> walkNodeResult = WalkNode(sabotenCache, Tree);

         return new PakiraDecisionTreePredictionResult(walkNodeResult.Item1, walkNodeResult.Item2);
      }

      private Tuple<PakiraLeaf, SabotenCache> WalkNode(SabotenCache sabotenCache, PakiraTree tree)
      {
         return WalkNode(sabotenCache, tree.Root);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private Tuple<PakiraLeaf, SabotenCache> WalkNode(SabotenCache v, PakiraNode node)
      {
         do
         {
            // Get the index of the feature for this node.
            int col = node.Column;

            v = v.Prefetch(col, TanukiTransformers);

            PakiraNode subNode;

            if (v[col] <= node.Threshold)
            {
               subNode = Tree.GetLeftNodeSafe(node);

               if (subNode == null)
               {
                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetLeftLeaf(node), v);
               }
            }
            else
            {
               subNode = Tree.GetRightNodeSafe(node);

               if (subNode == null)
               {
                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetRightLeaf(node), v);
               }
            }

            node = subNode;
         }
         while (true);
      }
   }
}
