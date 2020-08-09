﻿namespace Amaigoma
{
   using Shouldly;
   using System.Collections.Generic;
   using System.Collections.Immutable;

   public sealed class PakiraTree
   {
      private static readonly PakiraTree empty = new PakiraTree();

      private readonly ImmutableDictionary<IPakiraNode, IPakiraNode> leftNodes;
      private readonly ImmutableDictionary<IPakiraNode, IPakiraNode> rightNodes;

      private PakiraTree()
      {
         leftNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty;
         rightNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty;
      }

      private PakiraTree(IPakiraNode root, ImmutableDictionary<IPakiraNode, IPakiraNode> leftNodes, ImmutableDictionary<IPakiraNode, IPakiraNode> rightNodes)
      {
         Root = root;
         this.leftNodes = leftNodes;
         this.rightNodes = rightNodes;
      }

      public IPakiraNode Root { get; }

      public static PakiraTree Empty
      {
         get
         {
            return PakiraTree.empty;
         }
      }

      public PakiraTree AddNode(PakiraNode node, PakiraTree leftChildTree, PakiraTree rightChildTree)
      {
         this.ShouldBeSameAs(empty);

         return new PakiraTree(node,
            leftNodes.AddRange(leftChildTree.leftNodes).AddRange(rightChildTree.leftNodes).Add(node, leftChildTree.Root),
            rightNodes.AddRange(leftChildTree.rightNodes).AddRange(rightChildTree.rightNodes).Add(node, rightChildTree.Root));
      }

      public PakiraTree AddLeaf(PakiraLeaf leaf)
      {
         this.ShouldBeSameAs(empty);

         return new PakiraTree(leaf, ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty, ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty);
      }

      public IPakiraNode GetLeftNode(IPakiraNode node)
      {
         return leftNodes[node];
      }

      public IPakiraNode GetRightNode(IPakiraNode node)
      {
         return rightNodes[node];
      }

      public List<IPakiraNode> GetNodes()
      {
         List<IPakiraNode> allNodes = new List<IPakiraNode>();

         allNodes.Add(Root);
         allNodes.AddRange(leftNodes.Values);
         allNodes.AddRange(rightNodes.Values);

         return allNodes;
      }
   }
}