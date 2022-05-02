using Shouldly;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed class PakiraTree
   {
      private static readonly PakiraTree empty = new PakiraTree();
      private readonly ImmutableDictionary<PakiraNode, PakiraNode> leftNodes;
      private readonly ImmutableDictionary<PakiraNode, PakiraNode> rightNodes;
      private readonly ImmutableDictionary<PakiraNode, PakiraLeaf> leftLeaves;
      private readonly ImmutableDictionary<PakiraNode, PakiraLeaf> rightLeaves;

      private sealed record PakiraNodeComparer : IEqualityComparer<PakiraNode>
      {
         private static readonly PakiraNodeComparer instance = new PakiraNodeComparer();

         public static PakiraNodeComparer Instance
         {
            get
            {
               return instance;
            }
         }

         public bool Equals(PakiraNode x, PakiraNode y)
         {
            // Return reference comparison to prevent duplicate keys when their properties have the same values.
            return ReferenceEquals(x, y);
         }

         public int GetHashCode(PakiraNode obj)
         {
            return obj.GetHashCode();
         }
      }

      private PakiraTree()
      {
         Root = null;
         leftNodes = ImmutableDictionary<PakiraNode, PakiraNode>.Empty.WithComparers(PakiraNodeComparer.Instance);
         rightNodes = ImmutableDictionary<PakiraNode, PakiraNode>.Empty.WithComparers(PakiraNodeComparer.Instance);
         leftLeaves = ImmutableDictionary<PakiraNode, PakiraLeaf>.Empty.WithComparers(PakiraNodeComparer.Instance);
         rightLeaves = ImmutableDictionary<PakiraNode, PakiraLeaf>.Empty.WithComparers(PakiraNodeComparer.Instance);
      }

      private PakiraTree(PakiraNode root, ImmutableDictionary<PakiraNode, PakiraNode> leftNodes, ImmutableDictionary<PakiraNode, PakiraNode> rightNodes, ImmutableDictionary<PakiraNode, PakiraLeaf> leftLeaves, ImmutableDictionary<PakiraNode, PakiraLeaf> rightLeaves)
      {
         root.ShouldNotBeNull();

         Root = root;
         this.leftNodes = leftNodes;
         this.rightNodes = rightNodes;
         this.leftLeaves = leftLeaves;
         this.rightLeaves = rightLeaves;
      }

      public PakiraNode Root { get; }

      public static PakiraTree Empty
      {
         get
         {
            return PakiraTree.empty;
         }
      }

      public PakiraTree ReplaceLeaf(PakiraNode parentNode, PakiraLeaf leaf, PakiraTree pakiraTree)
      {
         if (Root == null)
         {
            return pakiraTree;
         }
         else
         {
            ImmutableDictionary<PakiraNode, PakiraNode> updatedLeftNodes;
            ImmutableDictionary<PakiraNode, PakiraNode> updatedRightNodes;
            ImmutableDictionary<PakiraNode, PakiraLeaf> updatedLeftLeaves;
            ImmutableDictionary<PakiraNode, PakiraLeaf> updatedRightLeaves;

            if (leftLeaves.Contains(parentNode, leaf))
            {
               updatedLeftNodes = leftNodes.Add(parentNode, pakiraTree.Root);
               updatedRightNodes = rightNodes;
               updatedLeftLeaves = leftLeaves.Remove(parentNode);
               updatedRightLeaves = rightLeaves;
            }
            else
            {
               rightLeaves.ShouldContainKeyAndValue(parentNode, leaf);

               updatedLeftNodes = leftNodes;
               updatedRightNodes = rightNodes.Add(parentNode, pakiraTree.Root);
               updatedLeftLeaves = leftLeaves;
               updatedRightLeaves = rightLeaves.Remove(parentNode);
            }

            updatedLeftNodes = updatedLeftNodes.AddRange(pakiraTree.leftNodes);
            updatedRightNodes = updatedRightNodes.AddRange(pakiraTree.rightNodes);
            updatedLeftLeaves = updatedLeftLeaves.AddRange(pakiraTree.leftLeaves);
            updatedRightLeaves = updatedRightLeaves.AddRange(pakiraTree.rightLeaves);

            return new PakiraTree(Root, updatedLeftNodes, updatedRightNodes, updatedLeftLeaves, updatedRightLeaves);
         }
      }

      public PakiraTree AddNode(PakiraNode node, PakiraLeaf leftChildLeaf, PakiraLeaf rightChildLeaf)
      {
         this.ShouldBeSameAs(Empty);
         node.ShouldNotBeNull();
         leftChildLeaf.ShouldNotBeNull();
         rightChildLeaf.ShouldNotBeNull();

         return new PakiraTree(node,
            leftNodes,
            rightNodes,
            leftLeaves.Add(node, leftChildLeaf),
            rightLeaves.Add(node, rightChildLeaf));
      }

      public PakiraNode GetLeftNodeSafe(PakiraNode node)
      {
         PakiraNode leftNode;

         leftNodes.TryGetValue(node, out leftNode);

         return leftNode;
      }

      public PakiraNode GetRightNodeSafe(PakiraNode node)
      {
         PakiraNode rightNode;

         rightNodes.TryGetValue(node, out rightNode);

         return rightNode;
      }

      public PakiraLeaf GetLeftLeaf(PakiraNode node)
      {
         return leftLeaves[node];
      }

      public PakiraLeaf GetRightLeaf(PakiraNode node)
      {
         return rightLeaves[node];
      }

      public IEnumerable<PakiraNode> GetNodes()
      {
         ImmutableList<PakiraNode> allNodes = ImmutableList<PakiraNode>.Empty;

         allNodes = allNodes.Add(Root);
         allNodes = allNodes.AddRange(leftNodes.Values);
         allNodes = allNodes.AddRange(rightNodes.Values);

         return allNodes;
      }

      public IEnumerable<KeyValuePair<PakiraNode, PakiraLeaf>> GetLeaves()
      {
         return leftLeaves.Concat(rightLeaves).AsEnumerable();
      }
   }
}