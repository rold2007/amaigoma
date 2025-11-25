using Shouldly;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Amaigoma
{
   public sealed record PakiraDecisionTreeModel // ncrunch: no coverage
   {
      public PakiraTree Tree { get; } = new();

      private ImmutableDictionary<int, ImmutableList<int>> LeafTrainDataCache { get; } = [];

      public PakiraDecisionTreeModel()
      {
      }

      private PakiraDecisionTreeModel(PakiraTree tree, ImmutableDictionary<int, ImmutableList<int>> leafTrainDataCache)
      {
         Tree = tree;
         LeafTrainDataCache = leafTrainDataCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, LeafTrainDataCache);
      }

      public PakiraDecisionTreeModel AddDataSample(int pakiraLeaf, int id)
      {
         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out ImmutableList<int> leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.Add(id)));
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.Add(pakiraLeaf, [id]));
         }
      }

      public PakiraDecisionTreeModel AddDataSample(int pakiraLeaf, IEnumerable<int> ids)
      {
         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out ImmutableList<int> leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.AddRange(ids)));
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.Add(pakiraLeaf, [.. ids]));
         }
      }

      public ImmutableList<int> DataSamples(int pakiraLeaf)
      {
         return LeafTrainDataCache[pakiraLeaf];
      }

      public PakiraDecisionTreeModel RemoveDataSample(int pakiraLeaf)
      {
         LeafTrainDataCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, LeafTrainDataCache.Remove(pakiraLeaf));
      }
   }
}
