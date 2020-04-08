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

      /// <summary>Constructor taking min and max value to create Range.</summary>
      /// <param name="min">The minimum.</param>
      /// <param name="max">The maximum.</param>
      public PakiraRange(double min, double max)
      {
         Min = min;
         Max = max;
      }

      /// <summary>Returns a string that represents the current object.</summary>
      /// <returns>A string that represents the current object.</returns>
      // ncrunch: no coverage start
      public override string ToString()
      {
         return string.Format("[{0}, {1})", Min, Max);
      }
      // ncrunch: no coverage end
   }
}
