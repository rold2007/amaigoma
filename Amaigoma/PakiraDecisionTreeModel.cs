namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;

   using DataTransformer = System.Converter<System.Collections.Generic.IList<double>, System.Collections.Generic.IList<double>>;

   /// <summary>A data Model for the decision tree.</summary>
   public sealed record PakiraDecisionTreeModel
   {
      static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();

      public PakiraTree Tree { get; } = PakiraTree.Empty;

      public TanukiTransformers TanukiTransformers { get; }

      public ImmutableList<SabotenCache> DataDistributionSamplesCache { get; }

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
      private PakiraDecisionTreeModel(PakiraTree tree, TanukiTransformers tanukiTransformers, ImmutableList<SabotenCache> dataDistributionSamplesCache)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
         DataDistributionSamplesCache = dataDistributionSamplesCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, TanukiTransformers, DataDistributionSamplesCache);
      }

      public PakiraDecisionTreeModel UpdateDataDistributionSamplesCache(ImmutableList<SabotenCache> dataDistributionSamplesCache)
      {
         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, dataDistributionSamplesCache);
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
