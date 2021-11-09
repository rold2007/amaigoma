namespace Amaigoma
{
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

   using DataTransformer = System.Converter<System.Collections.Generic.IList<double>, System.Collections.Generic.IList<double>>;

   /// <summary>A data Model for the decision tree.</summary>
   public sealed record PakiraDecisionTreeModel
   {
      static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraTree Tree { get; } = PakiraTree.Empty;

      private TanukiTransformers TanukiTransformers { get; }

      private ImmutableDictionary<PakiraLeaf, ImmutableList<SabotenCache>> LeafDataDistributionSamplesCache { get; } = ImmutableDictionary<PakiraLeaf, ImmutableList<SabotenCache>>.Empty;
      private ImmutableDictionary<PakiraLeaf, TrainDataCache> LeafTrainDataCache { get; } = ImmutableDictionary<PakiraLeaf, TrainDataCache>.Empty;

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel(IList<double> dataSample) : this(PakiraTree.Empty, new DataTransformer(DefaultDataTransformer.ConvertAll), dataSample)
      {
      }

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel(DataTransformer dataTransformers, IList<double> dataSample) : this(PakiraTree.Empty, dataTransformers, dataSample)
      {
      }

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel(PakiraTree tree, DataTransformer dataTransformers, IList<double> dataSample)
      {
         Tree = tree;
         TanukiTransformers = new TanukiTransformers(dataTransformers, dataSample);
      }

      /// <summary>Default constructor.</summary>
      private PakiraDecisionTreeModel(PakiraTree tree, TanukiTransformers tanukiTransformers, ImmutableDictionary<PakiraLeaf, ImmutableList<SabotenCache>> leafDataDistributionSamplesCache, ImmutableDictionary<PakiraLeaf, TrainDataCache> leafTrainDataCache)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
         LeafDataDistributionSamplesCache = leafDataDistributionSamplesCache;
         LeafTrainDataCache = leafTrainDataCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, TanukiTransformers, LeafDataDistributionSamplesCache, LeafTrainDataCache);
      }

      public PakiraDecisionTreeModel AddDataDistributionSamplesCache(PakiraLeaf pakiraLeaf, ImmutableList<SabotenCache> dataDistributionSamplesCache)
      {
         ImmutableList<SabotenCache> leafDataDistributionSamplesCache;

         if (LeafDataDistributionSamplesCache.TryGetValue(pakiraLeaf, out leafDataDistributionSamplesCache))
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache.SetItem(pakiraLeaf, leafDataDistributionSamplesCache.AddRange(dataDistributionSamplesCache)), LeafTrainDataCache);
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache.Add(pakiraLeaf, dataDistributionSamplesCache), LeafTrainDataCache);
         }
      }

      public PakiraDecisionTreeModel AddTrainDataCache(PakiraLeaf pakiraLeaf, TrainDataCache trainDataCache)
      {
         TrainDataCache leafTrainDataCache;

         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.AddSamples(trainDataCache)));
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache, LeafTrainDataCache.Add(pakiraLeaf, trainDataCache));
         }
      }

      public ImmutableList<SabotenCache> DataDistributionSamplesCache(PakiraLeaf pakiraLeaf)
      {
         return LeafDataDistributionSamplesCache[pakiraLeaf];
      }

      public TrainDataCache TrainDataCache(PakiraLeaf pakiraLeaf)
      {
         return LeafTrainDataCache[pakiraLeaf];
      }

      public PakiraDecisionTreeModel RemoveDataDistributionSamplesCache(PakiraLeaf pakiraLeaf)
      {
         LeafDataDistributionSamplesCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache.Remove(pakiraLeaf), LeafTrainDataCache);
      }

      public PakiraDecisionTreeModel RemoveTrainDataCache(PakiraLeaf pakiraLeaf)
      {
         LeafTrainDataCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache, LeafTrainDataCache.Remove(pakiraLeaf));
      }

      public IEnumerable<int> FeatureIndices()
      {
         return Enumerable.Range(0, TanukiTransformers.TotalOutputSamples);
      }

      public SabotenCache Prefetch(SabotenCache dataSample, int featureIndex)
      {
         return dataSample.Prefetch(featureIndex, TanukiTransformers);
      }

      public ImmutableList<SabotenCache> Prefetch(ImmutableList<SabotenCache> dataSamples, int featureIndex)
      {
         return dataSamples.Prefetch(featureIndex, TanukiTransformers).ToImmutableList();
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public double Predict(IList<double> y)
      {
         return PredictNode(y).Value;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public double Predict(SabotenCache sabotenCache)
      {
         return PredictNode(sabotenCache).Value;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraLeaf PredictNode(IList<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree.Root) as PakiraLeaf;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraLeaf PredictNode(SabotenCache sabotenCache)
      {
         return WalkNode(sabotenCache, Tree.Root) as PakiraLeaf;
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private IPakiraNode WalkNode(SabotenCache v, IPakiraNode node)
      {
         if (node.IsLeaf)
            return node;

         // Get the index of the feature for this node.
         var col = node.Column;

         v = v.Prefetch(col, TanukiTransformers);

         return WalkNode(v, (v[col] < node.Threshold) ? Tree.GetLeftNode(node) : Tree.GetRightNode(node));
      }
   }
}
