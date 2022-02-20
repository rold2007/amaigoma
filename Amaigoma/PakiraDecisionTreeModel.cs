namespace Amaigoma
{
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   using DataTransformer = System.Converter<System.Collections.Generic.IList<double>, System.Collections.Generic.IList<double>>;

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

      public PakiraDecisionTreeModel(IList<double> dataSample) : this(PakiraTree.Empty, new DataTransformer(DefaultDataTransformer.ConvertAll), dataSample)
      {
      }

      public PakiraDecisionTreeModel(DataTransformer dataTransformers, IList<double> dataSample) : this(PakiraTree.Empty, dataTransformers, dataSample)
      {
      }

      public PakiraDecisionTreeModel(PakiraTree tree, DataTransformer dataTransformers, IList<double> dataSample)
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
         TrainDataCache leafTrainDataCache;

         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out leafTrainDataCache))
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

      public ImmutableList<SabotenCache> Prefetch(IEnumerable<SabotenCache> dataSamples, int featureIndex)
      {
         return dataSamples.Prefetch(featureIndex, TanukiTransformers).ToImmutableList();
      }

      public ImmutableList<SabotenCache> PrefetchAll(IEnumerable<SabotenCache> dataSamples)
      {
         return dataSamples.PrefetchAll(TanukiTransformers).ToImmutableList();
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraLeaf PredictNode(IList<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree.Root).Item1 as PakiraLeaf;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraDecisionTreePredictionResult PredictNode(SabotenCache sabotenCache)
      {
         Tuple<IPakiraNode, SabotenCache> walkNodeResult = WalkNode(sabotenCache, Tree.Root);

         return new PakiraDecisionTreePredictionResult(walkNodeResult.Item1 as PakiraLeaf, walkNodeResult.Item2);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private Tuple<IPakiraNode, SabotenCache> WalkNode(SabotenCache v, IPakiraNode node)
      {
         if (node.IsLeaf)
            return new Tuple<IPakiraNode, SabotenCache>(node, v);

         // Get the index of the feature for this node.
         int col = node.Column;

         v = v.Prefetch(col, TanukiTransformers);

         return WalkNode(v, (v[col] <= node.Threshold) ? Tree.GetLeftNode(node) : Tree.GetRightNode(node));
      }
   }
}
