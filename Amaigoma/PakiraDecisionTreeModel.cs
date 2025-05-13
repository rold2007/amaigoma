using Shouldly;
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
}
