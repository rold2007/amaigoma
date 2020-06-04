namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   /// <summary>
   /// Class Tree.
   /// </summary>
   public class PakiraTree
   {
      /// <summary>
      /// Gets or sets the root.
      /// </summary>
      /// <value>The root.</value>
      public PakiraNode Root { get; set; }

      private readonly Dictionary<int, PakiraNode> nodes;

      /// <summary>
      /// Initializes a new PakiraTree.
      /// </summary>
      public PakiraTree()
      {
         nodes = new Dictionary<int, PakiraNode>();
      }

      /// <summary>
      /// Adds the specified PakiraNode to the current Graph.
      /// </summary>
      /// <param name="node">PakiraNode object to add.</param>
      public void AddNode(PakiraNode node)
      {
         nodes[node.Id] = node;
      }

      /// <summary>
      /// Adds the enumerable of PakiraNode objects to the current Graph.
      /// </summary>
      /// <param name="nodes">Collection of PakiraNode objects to add.</param>
      public void AddNodes(IEnumerable<PakiraNode> nodes)
      {
         foreach (var node in nodes)
            this.AddNode(node);
      }

      /// <summary>
      /// Gets the PakiraNode associated with the specified identifier.
      /// </summary>
      /// <param name="id">Identifier of the PakiraNode to return.</param>
      /// <returns>PakiraNode</returns>
      public PakiraNode GetNode(int id)
      {
         return this[id];
      }

      /// <summary>
      /// Returns True if the specified node exists in the graph.
      /// </summary>
      /// <param name="node">PakiraNode to check exists.</param>
      /// <returns></returns>
      public bool ContainsNode(PakiraNode node)
      {
         return nodes.ContainsKey(node.Id);
      }

      /// <summary>
      /// Gets the PakiraNode by the specified Id.
      /// </summary>
      /// <param name="id">The key of the specified PakiraNode to return.</param>
      /// <returns>PakiraNode.</returns>
      public PakiraNode this[int id]
      {
         get
         {
            if (nodes.ContainsKey(id))
               return nodes[id];
            else
               throw new InvalidOperationException($"Node {id} does not exist!");
         }
      }

      /// <summary>
      /// Returns all PakiraNode objects in the current graph.
      /// </summary>
      /// <returns></returns>
      public IEnumerable<PakiraNode> GetNodes()
      {
         foreach (var node in nodes)
            yield return node.Value;
      }

      public void Clear()
      {
         nodes.Clear();
      }
   }
}
