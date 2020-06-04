namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using MathNet.Numerics.LinearAlgebra;

   /// <summary>A data Model for the decision tree.</summary>
   public class PakiraDecisionTreeModel
   {
      public PakiraTree Tree { get; set; }
      public Converter<IList<double>, IList<double>> DataTransformers { get; set; }

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel()
      {
         Tree = new PakiraTree();
      }
      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public double Predict(Vector<double> y)
      {
         return WalkNode(DataTransformers(y), (PakiraNode)Tree.Root);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private double WalkNode(IList<double> v, PakiraNode node)
      {
         if (node.IsLeaf)
            return node.Value;

         // Get the index of the feature for this node.
         var col = node.Column;

         int childIndex = (v[col] < node.Threshold) ? 0 : 1;

         return WalkNode(v, (PakiraNode)Tree.GetNode(node.ChildId[childIndex]));
      }
   }
}
