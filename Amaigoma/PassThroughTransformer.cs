using System.Collections.Generic;

namespace Amaigoma
{
   // TODO Rename class to something else than "Transformer"
   public record PassThroughTransformer // ncrunch: no coverage
   {
      public PassThroughTransformer()
      {
      }

      public IEnumerable<double> ConvertAll(IEnumerable<double> list)
      {
         return list;
      }
   }
}