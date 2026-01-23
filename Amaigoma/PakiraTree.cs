global using BinaryTreeLeaf = (int id, int labelValue);
global using BinaryTreeNode = (int id, int featureIndex, double splitThreshold, int leftNodeIndex, int rightNodeIndex);
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
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

         updatedLeaves = updatedLeaves.Add(newNode.leftNodeIndex, new BinaryTreeLeafInternal { labelValue = leftLabel });
         updatedLeaves = updatedLeaves.Add(newNode.rightNodeIndex, new BinaryTreeLeafInternal { labelValue = rightLabel });
         updatedLeaves = updatedLeaves.Remove(nodeId);

         updatedNodes = updatedNodes.Add(nodeId, newNode);

         return (new PakiraTree(updatedNodes, updatedLeaves), newNode.leftNodeIndex, newNode.rightNodeIndex);
      }

      public PakiraTree ReplaceLeafValues(ImmutableDictionary<int, int> leafIdETL)
      {
         ImmutableDictionary<int, BinaryTreeLeafInternal> updatedLeaves = leaves;

         foreach ((int id, BinaryTreeLeafInternal leaf) in leaves)
         {
            updatedLeaves = updatedLeaves.SetItem(id, new BinaryTreeLeafInternal { labelValue = leafIdETL[leaf.labelValue] });
         }

         return new PakiraTree(nodes, updatedLeaves);
      }

      public PakiraTree SwapCondition(int id)
      {
         BinaryTreeNodeInternal node = nodes[id];

         return new PakiraTree(nodes.SetItem(id, (node.featureIndex, node.splitThreshold, node.rightNodeIndex, node.leftNodeIndex)), leaves);
      }

      public ImmutableDictionary<int, int> NodesDepth()
      {
         ImmutableDictionary<int, int> depths = ImmutableDictionary<int, int>.Empty;

         ImmutableStack<(int, BinaryTreeNodeInternal)> nodesStack = ImmutableStack<(int, BinaryTreeNodeInternal)>.Empty;

         depths = depths.SetItem(0, 0);
         nodesStack = nodesStack.Push((0, nodes[0]));

         while (!nodesStack.IsEmpty)
         {
            (int id, BinaryTreeNodeInternal node) = nodesStack.Peek();

            nodesStack = nodesStack.Pop();

            //if (temp != NULL)
            {
               if (nodes.ContainsKey(node.leftNodeIndex))
               {
                  depths = depths.SetItem(node.leftNodeIndex, depths[id] + 1);
                  nodesStack = nodesStack.Push((node.leftNodeIndex, nodes[node.leftNodeIndex]));
               }

               if (nodes.ContainsKey(node.rightNodeIndex))
               {
                  depths = depths.SetItem(node.rightNodeIndex, depths[id] + 1);
                  nodesStack = nodesStack.Push((node.rightNodeIndex, nodes[node.rightNodeIndex]));
               }
            }
         }

         return depths;
      }
   }
}