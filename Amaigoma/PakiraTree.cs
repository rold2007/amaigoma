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
      private readonly Dictionary<int, Dictionary<int, PakiraEdge>> edges;

      /// <summary>
      /// Initializes a new PakiraTree.
      /// </summary>
      public PakiraTree()
      {
         nodes = new Dictionary<int, PakiraNode>();
         edges = new Dictionary<int, Dictionary<int, PakiraEdge>>();
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
      /// Removes the specified node and its associated edges from the Graph.
      /// </summary>
      /// <param name="node">PakiraNode to remove.</param>
      public void RemoveNode(PakiraNode node)
      {
         // remove node
         nodes.Remove(node.Id);

         // remove associated edges
         if (edges.ContainsKey(node.Id))
            edges.Remove(node.Id);

         foreach (var key in edges.Keys)
            if (edges[key].ContainsKey(node.Id))
               edges[key].Remove(node.Id);
      }

      /// <summary>
      /// Inserts the Edge object to the Graph.
      /// <para>Connecting PakiraNode objects should already be present in the graph before attempting to add a connection.</para>
      /// </summary>
      /// <param name="edge">PakiraEdge object to add.</param>
      public void AddEdge(PakiraEdge edge)
      {
         if (nodes.ContainsKey(edge.ParentId) && nodes.ContainsKey(edge.ChildId))
            edges.AddOrUpdate(edge.ParentId, edge.ChildId, edge);
         else
            throw new InvalidOperationException("Invalid node index specified in edge");
      }

      /// <summary>
      /// Inserts the enumerable of Edge objects to the Graph.
      /// <para>Connecting PakiraNode objects should already be present in the graph before attempting to add a connection.</para>
      /// </summary>
      /// <param name="edges">Collection of PakiraEdge objects to add.</param>
      public void AddEdges(IEnumerable<PakiraEdge> edges)
      {
         foreach (var edge in edges)
            this.AddEdge(edge);
      }

      /// <summary>
      /// Removes the Edge object from the graph.
      /// </summary>
      /// <param name="edge">PakiraEdge object to remove.</param>
      public void RemoveEdge(PakiraEdge edge)
      {
         edges[edge.ParentId].Remove(edge.ChildId);
      }

      /// <summary>
      /// Gets the efferent or outbound connections for the specified PakiraNode object. 
      /// </summary>
      /// <param name="v">PakiraNode object to return edges for.</param>
      /// <returns>IEnumerable&lt;PakiraEdge&gt;</returns>
      public IEnumerable<PakiraEdge> GetOutEdges(PakiraNode v)
      {
         foreach (var edges in edges[v.Id])
            yield return edges.Value;
      }

      /// <summary>
      /// Gets the afferent or inbound connections for the specified PakiraNode object.
      /// </summary>
      /// <param name="node">PakiraNode object to return edges for.</param>
      /// <returns>IEnumerable&lt;PakiraEdge&gt;</returns>
      public IEnumerable<PakiraEdge> GetInEdges(PakiraNode node)
      {
         foreach (var edges in edges)
            foreach (var e in edges.Value)
               if (e.Value.ChildId == node.Id)
                  yield return e.Value;
      }

      /// <summary>
      /// Gets the child nodes for the specified PakiraNode object.
      /// </summary>
      /// <param name="node">PakiraNode object to return child nodes for.</param>
      /// <returns>IEnumerable&lt;PakiraNode&gt;</returns>
      public IEnumerable<PakiraNode> GetChildren(PakiraNode node)
      {
         foreach (var edges in GetOutEdges(node))
            yield return nodes[edges.ChildId];
      }

      /// <summary>
      /// Gets the parent nodes for the specified PakiraNode object.
      /// </summary>
      /// <param name="node">PakiraNode object to return parent nodes for.</param>
      /// <returns>IEnumerable&lt;PakiraNode&gt;</returns>
      public IEnumerable<PakiraNode> GetParents(PakiraNode node)
      {
         foreach (var edges in GetInEdges(node))
            yield return nodes[edges.ParentId];
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

      /// <summary>
      /// Returns all PakiraEdge objects in the current graph.
      /// </summary>
      /// <returns></returns>
      public IEnumerable<PakiraEdge> GetEdges()
      {
         foreach (var edges in edges)
            foreach (var e in edges.Value)
               yield return e.Value;
      }

      public void Clear()
      {
         nodes.Clear();
         edges.Clear();
      }
   }
}
