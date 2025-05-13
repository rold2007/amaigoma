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
         return WalkNode(id).Item1;
      }

      private Tuple<PakiraLeaf, SabotenCache> WalkNode(int id)
      {
         PakiraNode node = Tree.Root;
         SabotenCache sabotenCache = TanukiETL.TanukiSabotenCacheExtractor(id);
         bool loadSabotenCache = false;

         do
         {
            // Get the index of the feature for this node.
            int col = node.Column;

            if (!sabotenCache.CacheHit(col))
            {
               sabotenCache = sabotenCache.Prefetch(TanukiETL, id, col);
               loadSabotenCache = true;
            }

            PakiraNode subNode;

            if (sabotenCache[col] <= node.Threshold)
            {
               subNode = Tree.GetLeftNodeSafe(node);

               if (subNode == null)
               {
                  if (loadSabotenCache)
                  {
                     TanukiETL.TanukiSabotenCacheLoad(id, sabotenCache);
                  }

                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetLeftLeaf(node), sabotenCache);
               }
            }
            else
            {
               subNode = Tree.GetRightNodeSafe(node);

               if (subNode == null)
               {
                  if (loadSabotenCache)
                  {
                     TanukiETL.TanukiSabotenCacheLoad(id, sabotenCache);
                  }

                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetRightLeaf(node), sabotenCache);
               }
            }

            node = subNode;
         }
         while (true);
      }
   }
}
