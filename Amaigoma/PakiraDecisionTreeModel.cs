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

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel() : this(new List<double>() { 0.0 })
      {
         
      }

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
      private PakiraDecisionTreeModel(PakiraTree tree, TanukiTransformers tanukiTransformers, ImmutableDictionary<PakiraLeaf, ImmutableList<SabotenCache>> leafDataDistributionSamplesCache)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
         LeafDataDistributionSamplesCache = leafDataDistributionSamplesCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, TanukiTransformers, LeafDataDistributionSamplesCache);
      }

      public PakiraDecisionTreeModel AddDataDistributionSamplesCache(PakiraLeaf pakiraLeaf, ImmutableList<SabotenCache> dataDistributionSamplesCache)
      {
         LeafDataDistributionSamplesCache.ContainsKey(pakiraLeaf).ShouldBeFalse();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache.Add(pakiraLeaf, dataDistributionSamplesCache));
      }

      public PakiraDecisionTreeModel RemoveDataDistributionSamplesCache(PakiraLeaf pakiraLeaf)
      {
         LeafDataDistributionSamplesCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafDataDistributionSamplesCache.Remove(pakiraLeaf));
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
      public IPakiraNode PredictNode(IList<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree.Root);
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public IPakiraNode PredictNode(SabotenCache sabotenCache)
      {
         return WalkNode(sabotenCache, Tree.Root);
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
