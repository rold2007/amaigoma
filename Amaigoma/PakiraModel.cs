namespace Amaigoma
{
   using numl.Math.LinearAlgebra;
   using numl.Supervised.DecisionTree;
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using System.Text;

   public class PakiraModel : PakiraDecisionTreeModel
   {
      /// <summary>Default constructor.</summary>
      public PakiraModel() : base()
      {
         Tree = new numl.Data.Tree();
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A double.</returns>
      public new Node Predict(Vector y)
      {
         return WalkNode(y, (Node)Tree.Root);
      }

      /// <summary>Walk node.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="v">The Vector to process.</param>
      /// <param name="node">The node.</param>
      /// <returns>A double.</returns>
      private Node WalkNode(Vector v, Node node)
      {
         if (node.IsLeaf)
            return node;

         // Get the index of the feature for this node.
         var col = node.Column;
         if (col == -1)
            throw new InvalidOperationException("Invalid Feature encountered during node walk!");

         var edges = Tree.GetOutEdges(node).ToArray();
         for (int i = 0; i < edges.Length; i++)
         {
            Edge edge = (Edge)edges[i];
            if (edge.Discrete && v[col] == edge.Min)
               return WalkNode(v, (Node)Tree.GetVertex(edge.ChildId));
            if (!edge.Discrete && v[col] >= edge.Min && v[col] < edge.Max)
               return WalkNode(v, (Node)Tree.GetVertex(edge.ChildId));
         }

         throw new InvalidOperationException(String.Format("Unable to match split value {0} for feature {1}\nConsider setting a Hint in order to avoid this error.", v[col],col));
      }
   }
}
