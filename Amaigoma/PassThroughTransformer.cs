using System.Collections.Generic;

namespace Amaigoma
{
   public class PassThroughTransformer
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