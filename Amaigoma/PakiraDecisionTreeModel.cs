﻿using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public sealed record PakiraDecisionTreePredictionResult // ncrunch: no coverage
   {
      public PakiraLeaf PakiraLeaf { get; }
      public SabotenCache SabotenCache { get; }

      public PakiraDecisionTreePredictionResult(PakiraLeaf pakiraLeaf, SabotenCache sabotenCache)
      {
         PakiraLeaf = pakiraLeaf;
         SabotenCache = sabotenCache;
      }
   }

   public sealed record PakiraDecisionTreeModel // ncrunch: no coverage
   {
      private static readonly PassThroughTransformer DefaultDataExtractor = new(); // ncrunch: no coverage
      private static readonly PassThroughTransformer DefaultDataTransformer = new(); // ncrunch: no coverage

      public PakiraTree Tree { get; } = new();

      private DataTransformer TanukiExtractor { get; }
      private TanukiTransformers TanukiTransformers { get; }

      private ImmutableDictionary<PakiraLeaf, TrainDataCache> LeafTrainDataCache { get; } = ImmutableDictionary<PakiraLeaf, TrainDataCache>.Empty;

      // TODO Replace dataSample parameter by TrainDataCache. But we need a way to make sure we have at least one sample in it, otherwise TanukiTransformers will crash.
      public PakiraDecisionTreeModel(IEnumerable<double> dataSample) : this(new TanukiTransformers(new DataTransformer(DefaultDataTransformer.ConvertAll), dataSample))
      {
      }

      // TODO Replace the "new DataTransformer" by a static member
      public PakiraDecisionTreeModel(TanukiTransformers tanukiTransformers) : this(tanukiTransformers, DefaultDataExtractor.ConvertAll)
      {
      }

      // TODO To follow the ETL order, better to invert these parameters (extractor vs transformer)
      public PakiraDecisionTreeModel(TanukiTransformers tanukiTransformers, DataTransformer dataExtractor)
      {
         TanukiTransformers = tanukiTransformers;
         // UNDONE Really needed or only inside TanukiTransformer?
         TanukiExtractor = dataExtractor;
      }

      private PakiraDecisionTreeModel(PakiraTree tree, TanukiTransformers tanukiTransformers, ImmutableDictionary<PakiraLeaf, TrainDataCache> leafTrainDataCache)
      {
         Tree = tree;
         TanukiTransformers = tanukiTransformers;
         LeafTrainDataCache = leafTrainDataCache;
      }

      public PakiraDecisionTreeModel UpdateTree(PakiraTree tree)
      {
         return new PakiraDecisionTreeModel(tree, TanukiTransformers, LeafTrainDataCache);
      }

      public PakiraDecisionTreeModel AddTrainDataCache(PakiraLeaf pakiraLeaf, TrainDataCache trainDataCache)
      {
         if (LeafTrainDataCache.TryGetValue(pakiraLeaf, out TrainDataCache leafTrainDataCache))
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.SetItem(pakiraLeaf, leafTrainDataCache.AddSamples(trainDataCache)));
         }
         else
         {
            return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.Add(pakiraLeaf, trainDataCache));
         }
      }

      public TrainDataCache TrainDataCache(PakiraLeaf pakiraLeaf)
      {
         return LeafTrainDataCache[pakiraLeaf];
      }

      public PakiraDecisionTreeModel RemoveTrainDataCache(PakiraLeaf pakiraLeaf)
      {
         LeafTrainDataCache.ContainsKey(pakiraLeaf).ShouldBeTrue();

         return new PakiraDecisionTreeModel(Tree, TanukiTransformers, LeafTrainDataCache.Remove(pakiraLeaf));
      }

      public IEnumerable<int> FeatureIndices()
      {
         return Enumerable.Range(0, TanukiTransformers.TotalOutputSamples);
      }

      public TrainDataCache PrefetchAll(TrainDataCache trainDataCache)
      {
         return trainDataCache.PrefetchAll(TanukiTransformers);
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraLeaf PredictLeaf(IEnumerable<double> y)
      {
         return WalkNode(new SabotenCache(y), Tree).Item1;
      }

      /// <summary>Predicts the given y coordinate.</summary>
      /// <param name="y">The Vector to process.</param>
      /// <returns>A node.</returns>
      public PakiraDecisionTreePredictionResult PredictLeaf(SabotenCache sabotenCache)
      {
         Tuple<PakiraLeaf, SabotenCache> walkNodeResult = WalkNode(sabotenCache, Tree);

         return new PakiraDecisionTreePredictionResult(walkNodeResult.Item1, walkNodeResult.Item2);
      }

      private Tuple<PakiraLeaf, SabotenCache> WalkNode(SabotenCache sabotenCache, PakiraTree tree)
      {
         return WalkNode(sabotenCache, tree.Root);
      }

      private Tuple<PakiraLeaf, SabotenCache> WalkNode(SabotenCache v, PakiraNode node)
      {
         do
         {
            // Get the index of the feature for this node.
            int col = node.Column;

            // UNDONE Apply TanukiExtractor here
            v = v.Prefetch(col, TanukiTransformers);

            PakiraNode subNode;

            if (v[col] <= node.Threshold)
            {
               subNode = Tree.GetLeftNodeSafe(node);

               if (subNode == null)
               {
                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetLeftLeaf(node), v);
               }
            }
            else
            {
               subNode = Tree.GetRightNodeSafe(node);

               if (subNode == null)
               {
                  return new Tuple<PakiraLeaf, SabotenCache>(Tree.GetRightLeaf(node), v);
               }
            }

            node = subNode;
         }
         while (true);
      }
   }
}
