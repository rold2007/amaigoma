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
      private readonly ImmutableList<int> labelValues = [];

      public PakiraLeaf(int labelValue)
      {
         labelValues = labelValues.Add(labelValue);
      }

      public PakiraLeaf(IEnumerable<int> labelValues)
      {
         this.labelValues = this.labelValues.AddRange(labelValues);
      }

      public IEnumerable<int> LabelValues
      {
         get
         {
            return labelValues.AsEnumerable();
         }
      }
   }
}
