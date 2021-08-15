namespace Amaigoma
{
   using Shouldly;
   using System;
   using System.Collections.Generic;
   using System.Collections.Immutable;
   using System.Linq;

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

      private PakiraTree(IPakiraNode root, ImmutableDictionary<IPakiraNode, IPakiraNode> leftNodes, ImmutableDictionary<IPakiraNode, IPakiraNode> rightNodes)
      {
         Root = root;
         this.leftNodes = leftNodes;
         this.rightNodes = rightNodes;

         // Swap key and value to insert in parent nodes
         parentNodes = ImmutableDictionary<IPakiraNode, IPakiraNode>.Empty.AddRange(this.leftNodes.Select(node => new KeyValuePair<IPakiraNode, IPakiraNode>(node.Value, node.Key)));
         parentNodes = parentNodes.AddRange(this.rightNodes.Select(node => new KeyValuePair<IPakiraNode, IPakiraNode>(node.Value, node.Key)));
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
         leftChildTree.ShouldNotBe(PakiraTree.Empty);
         rightChildTree.ShouldNotBe(PakiraTree.Empty);

         return new PakiraTree(node,
            leftNodes.AddRange(leftChildTree.leftNodes).AddRange(rightChildTree.leftNodes).Add(node, leftChildTree.Root),
            rightNodes.AddRange(leftChildTree.rightNodes).AddRange(rightChildTree.rightNodes).Add(node, rightChildTree.Root));
      }

      public PakiraTree ReplaceLeaf(PakiraLeaf leaf, PakiraTree pakiraTree)
      {
         ImmutableDictionary<IPakiraNode, IPakiraNode> updatedLeftNodes;
         ImmutableDictionary<IPakiraNode, IPakiraNode> updatedRightNodes;
         IPakiraNode leafParent = GetParentNode(leaf);
         IPakiraNode root = Root;

         // The current pakira tree only has a leaf as root
         if (leafParent == null)
         {
            leaf.ShouldBe(Root);

            return pakiraTree;
         }
         else
         {
            if (leftNodes.Contains(leafParent, leaf))
            {
               updatedLeftNodes = leftNodes.Remove(leafParent);
               updatedRightNodes = rightNodes;
               updatedLeftNodes = updatedLeftNodes.Add(leafParent, pakiraTree.Root);
            }
            else
            {
               rightNodes.ShouldContainKey(leafParent);

               updatedLeftNodes = leftNodes;
               updatedRightNodes = rightNodes.Remove(leafParent);
               updatedRightNodes = updatedRightNodes.Add(leafParent, pakiraTree.Root);
            }

            updatedLeftNodes = updatedLeftNodes.AddRange(pakiraTree.leftNodes);
            updatedRightNodes = updatedRightNodes.AddRange(pakiraTree.rightNodes);

            return new PakiraTree(root, updatedLeftNodes, updatedRightNodes);
         }
      }

      public PakiraTree AddNode(PakiraNode node, PakiraLeaf leftChildLeaf, PakiraLeaf rightChildLeaf)
      {
         this.ShouldBeSameAs(empty);
         leftChildLeaf.ShouldNotBeNull();
         rightChildLeaf.ShouldNotBeNull();

         return new PakiraTree(node,
            leftNodes.Add(node, leftChildLeaf),
            rightNodes.Add(node, rightChildLeaf));
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
         List<IPakiraNode> allNodes = new List<IPakiraNode>(1 + leftNodes.Count + rightNodes.Count);

         allNodes.Add(Root);
         allNodes.AddRange(leftNodes.Values);
         allNodes.AddRange(rightNodes.Values);

         return allNodes;
      }

      public IPakiraNode GetParentNode(IPakiraNode node)
      {
         IPakiraNode parentNode = null;

         if (parentNodes.TryGetValue(node, out parentNode))
         {
            return parentNode;
         }

         return null;
      }
   }
}