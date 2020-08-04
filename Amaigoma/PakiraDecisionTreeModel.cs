namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   /// <summary>A data Model for the decision tree.</summary>
   public class PakiraDecisionTreeModel
   {
      public PakiraTree Tree { get; set; }

      public TanukiTransformers DataTransformers { get; set; }

      /// <summary>Default constructor.</summary>
      public PakiraDecisionTreeModel()
      {
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public double Predict(IList<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree.Root);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private double WalkNode(SabotenCache v, IPakiraNode node)
      {
         if (node.IsLeaf)
            return node.Value;

         // Get the index of the feature for this node.
         var col = node.Column;

         v = v.Prefetch(col, DataTransformers);

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
