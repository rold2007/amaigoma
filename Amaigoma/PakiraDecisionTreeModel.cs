namespace Amaigoma
{
   using MathNet.Numerics.LinearAlgebra;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;

   /// <summary>A data Model for the decision tree.</summary>
   public class PakiraDecisionTreeModel
   {
      public PakiraTree Tree { get; set; }

      public TanukiTransformers DataTransformers { get; set; }

      public Matrix<double> DataDistributionSamples { get; set; }
      public ImmutableList<SabotenCache> DataDistributionSamplesCache { get; set; }

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel()
      {
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

         v = v.Prefetch(col, DataTransformers);

         return WalkNode(v, (v[col] < node.Threshold) ? Tree.GetLeftNode(node) : Tree.GetRightNode(node));
      }
   }
}
