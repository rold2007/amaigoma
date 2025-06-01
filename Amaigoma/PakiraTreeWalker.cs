using System;

namespace Amaigoma
{
   public sealed record PakiraTreeWalker // ncrunch: no coverage
   {
      private PakiraTree Tree { get; }
      private TanukiETL TanukiETL { get; }

      public PakiraTreeWalker(PakiraTree tree, TanukiETL tanukiETL)
      {
         Tree = tree;
         TanukiETL = tanukiETL;
      }

      public PakiraLeaf PredictLeaf(int id)
      {
         return WalkNode(id);
      }

      private PakiraLeaf WalkNode(int id)
      {
         PakiraNode node = Tree.Root;

         do
         {
            // Get the index of the feature for this node.
            int col = node.Column;

            double columnValue = TanukiETL.TanukiDataTransformer(id, col);

            PakiraNode subNode;

            if (columnValue <= node.Threshold)
            {
               subNode = Tree.GetLeftNodeSafe(node);

               if (subNode == null)
               {
                  return Tree.GetLeftLeaf(node);
               }
            }
            else
            {
               subNode = Tree.GetRightNodeSafe(node);

               if (subNode == null)
               {
                  return Tree.GetRightLeaf(node);
               }
            }

            node = subNode;
         }
         while (true);
      }
   }
}
