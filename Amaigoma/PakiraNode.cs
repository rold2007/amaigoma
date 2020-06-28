using Shouldly;
using System;

namespace Amaigoma
{
   public interface IPakiraNode
   {
      bool IsLeaf { get; }
      double Value { get; }
      int Column { get; }
      double Threshold { get; }
   }

   public sealed class PakiraNode : IPakiraNode
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="PakiraNode"/> class.
      /// </summary>
      public PakiraNode(int column, double threshold)
      {
         column.ShouldBeGreaterThanOrEqualTo(0);

         this.Column = column;
         this.Threshold = threshold;
      }

      public bool IsLeaf
      {
         get
         {
            return false;
         }
      }

      /// <summary>Gets or sets the value.</summary>
      /// <value>The value.</value>
      public double Value
      {
         get
         {
            throw new InvalidOperationException();
         }
      }
      /// <summary>Gets or sets the column.</summary>
      /// <value>The column.</value>
      public int Column { get; }
      /// <summary>Gets or sets the threshold.</summary>
      /// <value>The threshold.</value>
      public double Threshold { get; }
   }

   public sealed class PakiraLeaf : IPakiraNode
   {
      public PakiraLeaf(double value)
      {
         this.Value = value;
      }

      public bool IsLeaf
      {
         get
         {
            return true;
         }
      }

      public double Value { get; }

      public int Column
      {
         get
         {
            throw new InvalidOperationException();
         }
      }

      public double Threshold
      {
         get
         {
            throw new InvalidOperationException();
         }
      }
   }
}
