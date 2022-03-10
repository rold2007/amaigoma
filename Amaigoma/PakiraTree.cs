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

      private readonly ImmutableDictionary<IPakiraNode, IPakiraNode> leftNodes;
      private readonly ImmutableDictionary<IPakiraNode, IPakiraNode> rightNodes;
      private readonly ImmutableDictionary<IPakiraNode, IPakiraNode> parentNodes;

      private PakiraTree()
      {
         leftNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty;
         rightNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty;
         parentNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty;
      }

      private PakiraTree(IPakiraNode root, ImmutableDictionary<IPakiraNode, IPakiraNode> leftNodes, ImmutableDictionary<IPakiraNode, IPakiraNode> rightNodes, ImmutableDictionary<IPakiraNode, IPakiraNode> parentNodes)
      {
         Root = root;
         this.leftNodes = leftNodes;
         this.rightNodes = rightNodes;
         this.parentNodes = parentNodes;
      }

      public IPakiraNode Root { get; }

      public static PakiraTree Empty
      {
         get
         {
            return PakiraTree.empty;
         }
      }

      public PakiraTree ReplaceLeaf(PakiraLeaf leaf, PakiraTree pakiraTree)
      {
         IPakiraNode leafParent = GetParentNode(leaf);

         // The current pakira tree only has a leaf as root
         if (leafParent == null)
         {
            leaf.ShouldBe(Root);

            return pakiraTree;
         }
         else
         {
            ImmutableDictionary<IPakiraNode, IPakiraNode> updatedLeftNodes;
            ImmutableDictionary<IPakiraNode, IPakiraNode> updatedRightNodes;
            ImmutableDictionary<IPakiraNode, IPakiraNode> updatedParentNodes;

            if (leftNodes.Contains(leafParent, leaf))
            {
               updatedLeftNodes = leftNodes.Remove(leafParent);
               updatedRightNodes = rightNodes;
               updatedLeftNodes = updatedLeftNodes.Add(leafParent, pakiraTree.Root);
            }
            else
            {
               rightNodes.ShouldContainKeyAndValue(leafParent, leaf);

               updatedLeftNodes = leftNodes;
               updatedRightNodes = rightNodes.Remove(leafParent);
               updatedRightNodes = updatedRightNodes.Add(leafParent, pakiraTree.Root);
            }

            updatedLeftNodes = updatedLeftNodes.AddRange(pakiraTree.leftNodes);
            updatedRightNodes = updatedRightNodes.AddRange(pakiraTree.rightNodes);
            updatedParentNodes = parentNodes.Remove(leaf);
            updatedParentNodes = updatedParentNodes.Add(pakiraTree.Root, leafParent);
            updatedParentNodes = updatedParentNodes.AddRange(pakiraTree.parentNodes);

            return new PakiraTree(Root, updatedLeftNodes, updatedRightNodes, updatedParentNodes);
         }
      }

      public PakiraTree AddNode(PakiraNode node, PakiraLeaf leftChildLeaf, PakiraLeaf rightChildLeaf)
      {
         this.ShouldBeSameAs(Empty);
         node.ShouldNotBeNull();
         leftChildLeaf.ShouldNotBeNull();
         rightChildLeaf.ShouldNotBeNull();

         return new PakiraTree(node,
            leftNodes.Add(node, leftChildLeaf),
            rightNodes.Add(node, rightChildLeaf),
            parentNodes.Add(leftChildLeaf, node).Add(rightChildLeaf, node));
      }

      public PakiraTree AddLeaf(PakiraLeaf leaf)
      {
         this.ShouldBeSameAs(Empty);

         return new PakiraTree(leaf,
            ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty,
            ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty,
            ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty.Add(leaf, null));
      }

      public IPakiraNode GetLeftNodeSafe(IPakiraNode node)
      {
         IPakiraNode leftNode;

         leftNodes.TryGetValue(node, out leftNode);

         return leftNode;
      }

      public IPakiraNode GetLeftNode(IPakiraNode node)
      {
         return leftNodes[node];
      }

      public IPakiraNode GetRightNode(IPakiraNode node)
      {
         return rightNodes[node];
      }

      public IEnumerable<IPakiraNode> GetNodes()
      {
         ImmutableList<IPakiraNode> allNodes = ImmutableList<IPakiraNode>.Empty;

         allNodes = allNodes.Add(Root);
         allNodes = allNodes.AddRange(leftNodes.Values);
         allNodes = allNodes.AddRange(rightNodes.Values);

         return allNodes;
      }

      public IPakiraNode GetParentNode(IPakiraNode node)
      {
         return parentNodes[node];
      }

      public bool IsLeaf(IPakiraNode node)
      {
         if (leftNodes.ContainsKey(node))
         {
            rightNodes.ContainsKey(node).ShouldBeTrue();
            return false;
         }
         else
         {
            return true;
         }
      }
   }
}