namespace Amaigoma
{
   public class PakiraNode
   {
      static int _id = 0;
      /// <summary>
      /// Initializes a new instance of the <see cref="PakiraNode"/> class.
      /// </summary>
      public PakiraNode() { Id = ++_id; ChildId = new int[2]; }
      /// <summary>
      /// Gets or sets the identifier.
      /// </summary>
      /// <value>The identifier.</value>
      public int Id { get; private set; }
      /// <summary>if is a leaf.</summary>
      /// <value>true if this object is leaf, false if not.</value>
      public bool IsLeaf { get; set; }
      /// <summary>Gets or sets the value.</summary>
      /// <value>The value.</value>
      public double Value { get; set; }
      /// <summary>Gets or sets the column.</summary>
      /// <value>The column.</value>
      public int Column { get; set; }
      /// <summary>Gets or sets the name.</summary>
      /// <value>The name.</value>
      public string Name { get; set; }
      /// <summary>Gets or sets the gain.</summary>
      /// <value>The gain.</value>
      public double Gain { get; set; }
      /// <summary>Gets or sets the threshold.</summary>
      /// <value>The threshold.</value>
      public double Threshold { get; set; }
      /// <summary>Gets or sets the child ids.</summary>
      /// <value>The child ids.</value>
      public int[] ChildId { get; private set; }
   }
}
