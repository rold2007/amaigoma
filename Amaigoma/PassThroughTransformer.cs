using System.Collections.Generic;

namespace Amaigoma
{
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