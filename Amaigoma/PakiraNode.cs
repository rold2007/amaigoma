using Shouldly;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Amaigoma
{
   public sealed record PakiraNode // ncrunch: no coverage
   {
      public PakiraNode(int column, double threshold)
      {
         column.ShouldBeGreaterThanOrEqualTo(0);

         Column = column;
         Threshold = threshold;
      }

      public int Column { get; }

      public double Threshold { get; }
   }

   public sealed record PakiraLeaf // ncrunch: no coverage
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
