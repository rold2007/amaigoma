using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Amaigoma
{
   public sealed record PakiraDecisionTreeModel // ncrunch: no coverage
   {
      public PakiraTree Tree { get; } = new();

      private ImmutableDictionary<PakiraLeaf, ImmutableList<int>> LeafTrainDataCache { get; } = ImmutableDictionary<PakiraLeaf, ImmutableList<int>>.Empty;

      public PakiraDecisionTreeModel()
      {
      }

      private PakiraDecisionTreeModel(PakiraTree tree, ImmutableDictionary<PakiraLeaf, ImmutableList<int>> leafTrainDataCache)
      {
         Tree = tree;
         LeafTrainDataCache = leafTrainDataCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, LeafTrainDataCache);
      }

      public PakiraDecisionTreeModel AddDataSample(PakiraLeaf pakiraLeaf, ImmutableList<int> ids)
      {
         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out ImmutableList<int> leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.AddRange(ids)));
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.Add(pakiraLeaf, ids));
         }
      }

      public ImmutableList<int> DataSamples(PakiraLeaf pakiraLeaf)
      {
         return LeafTrainDataCache[pakiraLeaf];
      }

      public PakiraDecisionTreeModel RemoveDataSample(PakiraLeaf pakiraLeaf)
      {
         LeafTrainDataCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.Remove(pakiraLeaf));
      }
   }

   // TODO Move this to a new file.
   public sealed record PakiraTreeWalker // ncrunch: no coverage
   {
      private PakiraTree Tree { get; }
      private TanukiTransformers TanukiTransformers { get; }

      public PakiraTreeWalker(PakiraTree tree, TanukiTransformers tanukiTransformers)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
      }

      public PakiraLeaf PredictLeaf(int id)
      {
         return WalkNode(id).Item1;
      }

      private Tuple<PakiraLeaf, SabotenCache> WalkNode(int id)
      {
         PakiraNode node = Tree.Root;
         IEnumerable<double> dataSample = null;
         SabotenCache sabotenCache = TanukiTransformers.TanukiSabotenCacheExtractor(id);

         do
         {
            // Get the index of the feature for this node.
            int col = node.Column;

            if (!sabotenCache.CacheHit(col))
            {
               if (dataSample == null)
               {
                  dataSample = TanukiTransformers.TanukiDataExtractor(id);
               }

               sabotenCache = sabotenCache.Prefetch(TanukiTransformers, dataSample, col);
            }

            PakiraNode subNode;

            if (sabotenCache[col] <= node.Threshold)
            {
               subNode = Tree.GetLeftNodeSafe(node);

               if (subNode == null)
               {
                  TanukiTransformers.TanukiSabotenCacheLoad(id, sabotenCache);

                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetLeftLeaf(node), sabotenCache);
               }
            }
            else
            {
               subNode = Tree.GetRightNodeSafe(node);

               if (subNode == null)
               {
                  TanukiTransformers.TanukiSabotenCacheLoad(id, sabotenCache);

                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetRightLeaf(node), sabotenCache);
               }
            }

            node = subNode;
         }
         while (true);
      }
   }
}
