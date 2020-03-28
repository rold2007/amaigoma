﻿namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using System.Text;
   using System.Runtime.Serialization;

   public class PakiraNode
   {
      static int _id = 0;
      /// <summary>
      /// Initializes a new instance of the <see cref="PakiraNode"/> class.
      /// </summary>
      public PakiraNode() { Id = ++_id; }
      /// <summary>
      /// Gets or sets the identifier.
      /// </summary>
      /// <value>The identifier.</value>
      public int Id { get; set; }

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

      public override int GetHashCode()
      {
         return base.GetHashCode();
      }
      /// <summary>
      /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
      /// </summary>
      /// <param name="obj">The object to compare with the current object.</param>
      /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
      public override bool Equals(object obj)
      {
         if (obj is PakiraNode)
         {
            return ((PakiraNode)obj).Column == Column &&
                   ((PakiraNode)obj).Gain == Gain &&
                   ((PakiraNode)obj).Id == Id &&
                   ((PakiraNode)obj).IsLeaf == IsLeaf &&
                   ((PakiraNode)obj).Name == Name &&
                   ((PakiraNode)obj).Value == Value;
         }
         else
            return false;
      }
   }

   public class PakiraEdge
   {
      /// <summary>Gets or sets the child identifier.</summary>
      /// <value>The child identifier.</value>
      public int ChildId { get; set; }
      /// <summary> Gets or sets the parent identifier.</summary>
      /// <value>The parent identifier.</value>
      public int ParentId { get; set; }
      /// <summary>Gets or sets the minimum.</summary>
      /// <value>The minimum value.</value>
      public double Min { get; set; }
      /// <summary>Gets or sets the maximum.</summary>
      /// <value>The maximum value.</value>
      public double Max { get; set; }
      /// <summary>Gets or sets a value indicating whether the discrete.</summary>
      /// <value>true if discrete, false if not.</value>
      public bool Discrete { get; set; }
      /// <summary>Gets or sets the label.</summary>
      /// <value>The label.</value>
      public string Label { get; set; }
   }
}
