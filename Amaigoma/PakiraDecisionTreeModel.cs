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
      }
      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public double Predict(Vector<double> y)
      {
         return WalkNode(DataTransformers(y), Tree.Root);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private double WalkNode(IList<double> v, IPakiraNode node)
      {
         if (node.IsLeaf)
            return node.Value;

         // Get the index of the feature for this node.
         var col = node.Column;

         if (v[col] < node.Threshold)
         {
            return WalkNode(v, Tree.GetLeftNode(node));
         }
         else
         {
            return WalkNode(v, Tree.GetRightNode(node));
         }
      }
   }
}
