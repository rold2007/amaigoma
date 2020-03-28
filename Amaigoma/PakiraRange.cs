namespace Amaigoma
{
   using System;
   using System.Collections.Generic;
   using System.Text;

   /// <summary>A range.</summary>
   public class PakiraRange
   {
      public PakiraRange()
      {
         Min = -1;
         Max = 1;
      }

      /// <summary>Gets or sets the minimum.</summary>
      /// <value>The minimum value.</value>
      public double Min { get; set; }

      /// <summary>Gets or sets the maximum.</summary>
      /// <value>The maximum value.</value>
      public double Max { get; set; }

      /// <summary>Returns True if the value is between the current range.</summary>
      /// <param name="d">The double to process.</param>
      /// <returns>Bool.</returns>
      public bool Test(double d)
      {
         return d >= Min && d <= Max;
      }

      /// <summary>Constructor taking min and max value to create Range.</summary>
      /// <param name="min">The minimum.</param>
      /// <param name="max">The maximum.</param>
      public PakiraRange(double min, double max)
      {
         Min = min;
         Max = max;
      }

      /// <summary>Constructor taking only minimum value and creating slightly greated max (1e-05).</summary>
      /// <param name="min">The minimum.</param>
      public PakiraRange(double min) : this(min, min + 0.00001)
      {
      }

      /// <summary>Returns a string that represents the current object.</summary>
      /// <returns>A string that represents the current object.</returns>
      public override string ToString()
      {
         return string.Format("[{0}, {1})", Min, Max);
      }

      #region Operators

      public static implicit operator PakiraRange((double, double) xy)
      {
         return new PakiraRange(xy.Item1, xy.Item2);
      }

      #endregion
   }
}
