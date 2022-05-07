using Shouldly;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record PakiraNode
   {
      /// <summary>
      /// Initializes a new instance of the <see cref="PakiraNode"/> class.
      /// </summary>
      public PakiraNode(int column, double threshold)
      {
         column.ShouldBeGreaterThanOrEqualTo(0);

         Column = column;
         Threshold = threshold;
      }

      /// <summary>Gets or sets the column.</summary>
      /// <value>The column.</value>
      public int Column { get; }

      /// <summary>Gets or sets the threshold.</summary>
      /// <value>The threshold.</value>
      public double Threshold { get; }
   }

   public sealed record PakiraLeaf
   {
      private readonly ImmutableList<double> labelValues = ImmutableList<double>.Empty;

      public PakiraLeaf(double labelValue)
      {
         labelValues = labelValues.Add(labelValue);
      }

      public PakiraLeaf(IEnumerable<double> labelValues)
      {
         this.labelValues = this.labelValues.AddRange(labelValues);
      }

      public double LabelValue
      {
         get
         {
            return labelValues[0];
         }
      }

      public IEnumerable<double> LabelValues
      {
         get
         {
            return labelValues.AsEnumerable();
         }
      }
   }
}
