namespace Amaigoma
{
   using System;
   using System.Collections.Generic;

   public class PakiraGraph
   {
      private readonly Dictionary<int, PakiraNode> _vertices;
      private readonly Dictionary<int, Dictionary<int, PakiraEdge>> _edges;

      /// <summary>
      /// Initializes a new PakiraGraph.
      /// </summary>
      public PakiraGraph()
      {
         _vertices = new Dictionary<int, PakiraNode>();
         _edges = new Dictionary<int, Dictionary<int, PakiraEdge>>();
      }

      /// <summary>
      /// Adds the specified PakiraNode to the current Graph.
      /// </summary>
      /// <param name="v">PakiraNode object to add.</param>
      public void AddVertex(PakiraNode v)
      {
         _vertices[v.Id] = v;
      }

      /// <summary>
      /// Adds the enumerable of PakiraNode objects to the current Graph.
      /// </summary>
      /// <param name="vertices">Collection of PakiraNode objects to add.</param>
      public void AddVertices(IEnumerable<PakiraNode> vertices)
      {
         foreach (var vertex in vertices)
            this.AddVertex(vertex);
      }

      /// <summary>
      /// Gets the PakiraNode associated with the specified identifier.
      /// </summary>
      /// <param name="id">Identifier of the PakiraNode to return.</param>
      /// <returns>PakiraNode</returns>
      public PakiraNode GetVertex(int id)
      {
         return this[id];
      }

      /// <summary>
      /// Returns True if the specified vertex exists in the graph.
      /// </summary>
      /// <param name="v">PakiraNode to check exists.</param>
      /// <returns></returns>
      public bool ContainsVertex(PakiraNode v)
      {
         return _vertices.ContainsKey(v.Id);
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
            if (_vertices.ContainsKey(id))
               return _vertices[id];
            else
               throw new InvalidOperationException($"Vertex {id} does not exist!");
         }
      }

      /// <summary>
      /// Removes the specified Vertex and its associated edges from the Graph.
      /// </summary>
      /// <param name="v">PakiraNode to remove.</param>
      public void RemoveVertex(PakiraNode v)
      {
         // remove vertex
         _vertices.Remove(v.Id);

         // remove associated edges
         if (_edges.ContainsKey(v.Id))
            _edges.Remove(v.Id);

         foreach (var key in _edges.Keys)
            if (_edges[key].ContainsKey(v.Id))
               _edges[key].Remove(v.Id);
      }

      /// <summary>
      /// Inserts the Edge object to the Graph.
      /// <para>Connecting PakiraNode objects should already be present in the graph before attempting to add a connection.</para>
      /// </summary>
      /// <param name="edge">PakiraEdge object to add.</param>
      public void AddEdge(PakiraEdge edge)
      {
         if (_vertices.ContainsKey(edge.ParentId) && _vertices.ContainsKey(edge.ChildId))
            _edges.AddOrUpdate(edge.ParentId, edge.ChildId, edge);
         else
            throw new InvalidOperationException("Invalid vertex index specified in edge");
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
         _edges[edge.ParentId].Remove(edge.ChildId);
      }

      /// <summary>
      /// Gets the efferent or outbound connections for the specified PakiraNode object. 
      /// </summary>
      /// <param name="v">PakiraNode object to return edges for.</param>
      /// <returns>IEnumerable&lt;PakiraEdge&gt;</returns>
      public IEnumerable<PakiraEdge> GetOutEdges(PakiraNode v)
      {
         foreach (var edges in _edges[v.Id])
            yield return edges.Value;
      }

      /// <summary>
      /// Gets the afferent or inbound connections for the specified PakiraNode object. 
      /// </summary>
      /// <param name="v">PakiraNode object to return edges for.</param>
      /// <returns>IEnumerable&lt;PakiraEdge&gt;</returns>
      public IEnumerable<PakiraEdge> GetInEdges(PakiraNode v)
      {
         foreach (var edges in _edges)
            foreach (var e in edges.Value)
               if (e.Value.ChildId == v.Id)
                  yield return e.Value;
      }

      /// <summary>
      /// Gets the child vertices for the specified PakiraNode object. 
      /// </summary>
      /// <param name="v">PakiraNode object to return child vertices for.</param>
      /// <returns>IEnumerable&lt;PakiraNode&gt;</returns>
      public IEnumerable<PakiraNode> GetChildren(PakiraNode v)
      {
         foreach (var edges in GetOutEdges(v))
            yield return _vertices[edges.ChildId];
      }

      /// <summary>
      /// Gets the parent vertices for the specified PakiraNode object. 
      /// </summary>
      /// <param name="v">PakiraNode object to return parent vertices for.</param>
      /// <returns>IEnumerable&lt;PakiraNode&gt;</returns>
      public IEnumerable<PakiraNode> GetParents(PakiraNode v)
      {
         foreach (var edges in GetInEdges(v))
            yield return _vertices[edges.ParentId];
      }

      /// <summary>
      /// Returns all PakiraNode objects in the current graph.
      /// </summary>
      /// <returns></returns>
      public IEnumerable<PakiraNode> GetVertices()
      {
         foreach (var vertices in _vertices)
            yield return vertices.Value;
      }

      /// <summary>
      /// Returns all PakiraEdge objects in the current graph.
      /// </summary>
      /// <returns></returns>
      public IEnumerable<PakiraEdge> GetEdges()
      {
         foreach (var edges in _edges)
            foreach (var e in edges.Value)
               yield return e.Value;
      }

      public void Clear()
      {
         _vertices.Clear();
         _edges.Clear();
      }
   }
}
