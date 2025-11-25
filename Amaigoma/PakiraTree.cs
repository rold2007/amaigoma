global using BinaryTreeNode = (int id, int featureIndex, double splitThreshold, int leftNodeIndex, int rightNodeIndex);
global using BinaryTreeLeaf = (int id, int labelValue);

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using BinaryTreeNodeInternal = (int featureIndex, double splitThreshold, int leftNodeIndex, int rightNodeIndex);

// TODO Add support for Microsoft Orleans (https://learn.microsoft.com/en-us/dotnet/orleans/) to the project
namespace Amaigoma
{
   struct BinaryTreeLeafInternal
   {
      public int labelValue;
   }

   // TODO Rename Pakira to something more significative like BinaryTree or DecisionTree
   public sealed record PakiraTree // ncrunch: no coverage
   {
      private readonly ImmutableDictionary<int, BinaryTreeNodeInternal> nodes;
      private readonly ImmutableDictionary<int, BinaryTreeLeafInternal> leaves;

      public PakiraTree()
      {
         nodes = [];
         leaves = [];
      }

      public PakiraTree(int label)
      {
         nodes = [];
         leaves = ImmutableDictionary<int, BinaryTreeLeafInternal>.Empty.Add(0, new BinaryTreeLeafInternal { labelValue = label });
      }

      private PakiraTree(ImmutableDictionary<int, BinaryTreeNodeInternal> nodes, ImmutableDictionary<int, BinaryTreeLeafInternal> leaves)
      {
         this.nodes = nodes;
         this.leaves = leaves;
      }

      private int NextId
      {
         get
         {
            return nodes.Count + leaves.Count;
         }
      }

      public BinaryTreeNode? Node(int nodeIndex)
      {
         if (nodes.TryGetValue(nodeIndex, out BinaryTreeNodeInternal binaryTreeNode))
         {
            return new BinaryTreeNode(nodeIndex, binaryTreeNode.featureIndex, binaryTreeNode.splitThreshold, binaryTreeNode.leftNodeIndex, binaryTreeNode.rightNodeIndex);
         }
         else
         {
            return null;
         }
      }

      public BinaryTreeLeaf Leaf(int leafIndex)
      {
         return new BinaryTreeLeaf(leafIndex, leaves[leafIndex].labelValue);
      }

      public IEnumerable<BinaryTreeNode> Nodes()
      {
         return nodes.Select(x => new BinaryTreeNode(x.Key, x.Value.featureIndex, x.Value.splitThreshold, x.Value.leftNodeIndex, x.Value.rightNodeIndex)).AsEnumerable();
      }

      public IEnumerable<BinaryTreeLeaf> Leaves()
      {
         return leaves.Select(x => new BinaryTreeLeaf(x.Key, x.Value.labelValue)).AsEnumerable();
      }

      public (PakiraTree tree, int leftLeafId, int rightLeafId) ReplaceLeaf(int nodeId, int featureIndex, double splitThreshold, int leftLabel, int rightLabel)
      {
         ImmutableDictionary<int, BinaryTreeLeafInternal> updatedLeaves = leaves;
         ImmutableDictionary<int, BinaryTreeNodeInternal> updatedNodes = nodes;
         BinaryTreeNodeInternal newNode = (featureIndex, splitThreshold, NextId, NextId + 1);

         updatedLeaves = updatedLeaves.Add(newNode.leftNodeIndex, new BinaryTreeLeafInternal { labelValue = leftLabel /*, parentNodeIndex = nodeId*/ });
         updatedLeaves = updatedLeaves.Add(newNode.rightNodeIndex, new BinaryTreeLeafInternal { labelValue = rightLabel /*, parentNodeIndex = nodeId*/ });
         updatedLeaves = updatedLeaves.Remove(nodeId);

         updatedNodes = updatedNodes.Add(nodeId, newNode);

         return (new PakiraTree(updatedNodes, updatedLeaves), newNode.leftNodeIndex, newNode.rightNodeIndex);
      }
   }
}